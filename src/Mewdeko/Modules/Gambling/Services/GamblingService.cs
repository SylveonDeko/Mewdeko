using Mewdeko.Modules.Gambling.Common;
using Mewdeko.Modules.Gambling.Common.WheelOfFortune;
using Mewdeko.Modules.Gambling.Connect4;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Gambling.Services;

public class GamblingService : INService
{
    private readonly IDataCache _cache;
    private readonly DiscordSocketClient _client;
    private readonly ICurrencyService _cs;
    private readonly DbService _db;
    private readonly IBotCredentials _creds;
    private readonly HttpClient _httpClient;

    private readonly GamblingConfigService _gss;

    public GamblingService(DbService db, Mewdeko bot, ICurrencyService cs,
        DiscordSocketClient client, IDataCache cache, GamblingConfigService gss,
        IBotCredentials creds,
        HttpClient httpClient)
    {
        _db = db;
        _cs = cs;
        _client = client;
        _cache = cache;
        _gss = gss;
        _creds = creds;
        _httpClient = httpClient;

        if (bot.Client.ShardId == 0)
        {
            _ = new Timer(_ =>
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

    public async Task<bool> GetVoted(ulong id)
    {
        _httpClient.DefaultRequestHeaders.Add("Authorization", _creds.VotesToken);
        var tocheck = await _httpClient.GetStringAsync($"https://top.gg/api/bots/{_client.CurrentUser.Id}/check?userId={id}");
        return tocheck.Contains('1');
    }

    public async Task<EconomyResult> GetEconomy()
    {
        if (_cache.TryGetEconomy(out var data))
        {
            try
            {
                return JsonConvert.DeserializeObject<EconomyResult>(data);
            }
            catch
            {
                // ignored
            }
        }

        decimal cash;
        decimal onePercent;
        decimal planted;
        decimal waifus;
        long bot;

        await using (var uow = _db.GetDbContext())
        {
            cash = uow.DiscordUser.GetTotalCurrency();
            onePercent = uow.DiscordUser.GetTopOnePercentCurrency(_client.CurrentUser.Id);
            planted = uow.PlantedCurrency.AsQueryable().Sum(x => x.Amount);
            waifus = await uow.WaifuInfo.GetTotalValue();
            bot = await uow.DiscordUser.GetUserCurrency(_client.CurrentUser.Id);
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
        public decimal Cash { get; init; }
        public decimal Planted { get; init; }
        public decimal Waifus { get; init; }
        public decimal OnePercent { get; init; }
        public long Bot { get; init; }
    }
}