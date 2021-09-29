using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;
using Mewdeko.Core.Modules.Gambling.Common;
using Mewdeko.Core.Modules.Gambling.Services;
using Mewdeko.Services;
using Mewdeko.Modules.Gambling.Common.Connect4;
using Mewdeko.Modules.Gambling.Common.WheelOfFortune;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Modules.Gambling.Services
{
    public class GamblingService : INService
    {
        private readonly Mewdeko _bot;
        private readonly IDataCache _cache;
        private readonly DiscordSocketClient _client;
        private readonly ICurrencyService _cs;
        private readonly DbService _db;

        private readonly Timer _decayTimer;
        private readonly GamblingConfigService _gss;

        public GamblingService(DbService db, Mewdeko bot, ICurrencyService cs,
            DiscordSocketClient client, IDataCache cache, GamblingConfigService gss)
        {
            _db = db;
            _cs = cs;
            _bot = bot;
            _client = client;
            _cache = cache;
            _gss = gss;

            if (_bot.Client.ShardId == 0)
                _decayTimer = new Timer(_ =>
                {
                    var config = _gss.Data;
                    var maxDecay = config.Decay.MaxDecay;
                    if (config.Decay.Percent <= 0 || config.Decay.Percent > 1 || maxDecay < 0)
                        return;

                    using (var uow = _db.GetDbContext())
                    {
                        var lastCurrencyDecay = _cache.GetLastCurrencyDecay();

                        if (DateTime.UtcNow - lastCurrencyDecay < TimeSpan.FromHours(config.Decay.HourInterval))
                            return;

                        Log.Information($"Decaying users' currency - decay: {config.Decay.Percent * 100}% " +
                                        $"| max: {maxDecay} " +
                                        $"| threshold: {config.Decay.MinThreshold}");

                        if (maxDecay == 0)
                            maxDecay = int.MaxValue;

                        uow._context.Database.ExecuteSqlInterpolated($@"
UPDATE DiscordUser
SET CurrencyAmount=
    CASE WHEN
    {maxDecay} > ROUND(CurrencyAmount * {config.Decay.Percent} - 0.5)
    THEN
    CurrencyAmount - ROUND(CurrencyAmount * {config.Decay.Percent} - 0.5)
    ELSE
    CurrencyAmount - {maxDecay}
    END
WHERE CurrencyAmount > {config.Decay.MinThreshold} AND UserId!={_client.CurrentUser.Id};");

                        _cache.SetLastCurrencyDecay();
                        uow.SaveChanges();
                    }
                }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            //using (var uow = _db.UnitOfWork)
            //{
            //    //refund all of the currency users had at stake in gambling games
            //    //at the time bot was restarted

            //    var stakes = uow._context.Set<Stake>()
            //        .ToArray();

            //    var userIds = stakes.Select(x => x.UserId).ToArray();
            //    var reasons = stakes.Select(x => "Stake-" + x.Source).ToArray();
            //    var amounts = stakes.Select(x => x.Amount).ToArray();

            //    _cs.AddBulkAsync(userIds, reasons, amounts, gamble: true).ConfigureAwait(false);

            //    foreach (var s in stakes)
            //    {
            //        _cs.AddAsync(s.UserId, "Stake-" + s.Source, s.Amount, gamble: true)
            //            .GetAwaiter()
            //            .GetResult();
            //    }

            //    uow._context.Set<Stake>().RemoveRange(stakes);
            //    uow.Complete();
            //    Log.Information("Refunded {0} users' stakes.", stakes.Length);
            //}
        }

        public ConcurrentDictionary<(ulong, ulong), RollDuelGame> Duels { get; } = new();
        public ConcurrentDictionary<ulong, Connect4Game> Connect4Games { get; } = new();

        public bool GetVoted(ulong Id)
        {
            var url = $"https://top.gg/api/bots/752236274261426212/check?userId={Id}";

            var request = WebRequest.Create(url);
            request.Method = "GET";
            request.Headers.Add("Authorization",
                "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6Ijc1MjIzNjI3NDI2MTQyNjIxMiIsImJvdCI6dHJ1ZSwiaWF0IjoxNjA3Mzg3MDk4fQ.1VATJIr_WqRImXlx5hywaAV6BVk-V4NzybRo0e-E3T8");
            using var webResponse = request.GetResponse();
            using var webStream = webResponse.GetResponseStream();

            using var reader = new StreamReader(webStream);
            var data = reader.ReadToEnd();
            if (data.Contains("{\"voted\":1}"))
                return true;
            return false;
        }

        public EconomyResult GetEconomy()
        {
            if (_cache.TryGetEconomy(out var data))
                try
                {
                    return JsonConvert.DeserializeObject<EconomyResult>(data);
                }
                catch
                {
                }

            decimal cash;
            decimal onePercent;
            decimal planted;
            decimal waifus;
            long bot;

            using (var uow = _db.GetDbContext())
            {
                cash = uow.DiscordUsers.GetTotalCurrency();
                onePercent = uow.DiscordUsers.GetTopOnePercentCurrency(_client.CurrentUser.Id);
                planted = uow.PlantedCurrency.GetTotalPlanted();
                waifus = uow.Waifus.GetTotalValue();
                bot = uow.DiscordUsers.GetUserCurrency(_client.CurrentUser.Id);
            }

            var result = new EconomyResult
            {
                Cash = cash,
                Planted = planted,
                Bot = bot,
                Waifus = waifus,
                OnePercent = onePercent
            };

            _cache.SetEconomy(JsonConvert.SerializeObject(result));
            return result;
        }

        public Task<WheelOfFortuneGame.Result> WheelOfFortuneSpinAsync(ulong userId, long bet)
        {
            return new WheelOfFortuneGame(userId, bet, _gss.Data, _cs).SpinAsync();
        }

        public struct EconomyResult
        {
            public decimal Cash { get; set; }
            public decimal Planted { get; set; }
            public decimal Waifus { get; set; }
            public decimal OnePercent { get; set; }
            public long Bot { get; set; }
        }
    }
}