using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Threading;
using Discord.WebSocket;
using Mewdeko.Database.Extensions;
using Mewdeko.Modules.Gambling.Common;
using Mewdeko.Modules.Gambling.Common.WheelOfFortune;
using Mewdeko.Modules.Gambling.Connect4;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Modules.Gambling.Services;

public class GamblingService : INService
{
    private readonly Mewdeko _bot;
    private readonly IDataCache _cache;
    private readonly DiscordSocketClient _client;
    private readonly ICurrencyService _cs;
    private readonly DbService _db;

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
        {
            new Timer(_ =>
            {
                var config = _gss.Data;
                var maxDecay = config.Decay.MaxDecay;
                if (config.Decay.Percent is <= 0 or > 1 || maxDecay < 0)
                    return;

                using var uow = _db.GetDbContext();
                var lastCurrencyDecay = _cache.GetLastCurrencyDecay();

                if (DateTime.UtcNow - lastCurrencyDecay < TimeSpan.FromHours(config.Decay.HourInterval))
                    return;

                Log.Information($"Decaying users' currency - decay: {config.Decay.Percent * 100}% " +
                                $"| max: {maxDecay} " +
                                $"| threshold: {config.Decay.MinThreshold}");

                if (maxDecay == 0)
                    maxDecay = int.MaxValue;

                uow.Database.ExecuteSqlInterpolated($@"
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
            }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }
    }

    public ConcurrentDictionary<(ulong, ulong), RollDuelGame> Duels { get; } = new();
    public ConcurrentDictionary<ulong, Connect4Game> Connect4Games { get; } = new();

    public static bool GetVoted(ulong id)
    {
        var url = $"https://top.gg/api/bots/752236274261426212/check?userId={id}";
#pragma warning disable SYSLIB0014 // Type or member is obsolete
        var request = WebRequest.Create(url);
#pragma warning restore SYSLIB0014 // Type or member is obsolete
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
            cash = uow.DiscordUser.GetTotalCurrency();
            onePercent = uow.DiscordUser.GetTopOnePercentCurrency(_client.CurrentUser.Id);
            planted = uow.PlantedCurrency.AsQueryable().Sum(x => x.Amount);
            waifus = uow.WaifuInfo.GetTotalValue();
            bot = uow.DiscordUser.GetUserCurrency(_client.CurrentUser.Id);
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

    public Task<WheelOfFortuneGame.Result> WheelOfFortuneSpinAsync(ulong userId, long bet) => new WheelOfFortuneGame(userId, bet, _gss.Data, _cs).SpinAsync();

    public struct EconomyResult
    {
        public decimal Cash { get; set; }
        public decimal Planted { get; set; }
        public decimal Waifus { get; set; }
        public decimal OnePercent { get; set; }
        public long Bot { get; set; }
    }
}