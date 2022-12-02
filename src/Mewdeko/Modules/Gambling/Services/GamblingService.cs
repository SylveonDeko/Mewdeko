using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Mewdeko.Modules.Gambling.Common;
using Mewdeko.Modules.Gambling.Common.WheelOfFortune;
using Mewdeko.Modules.Gambling.Connect4;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Modules.Gambling.Services;

public class GamblingService : INService
{
    private readonly IDataCache cache;
    private readonly DiscordSocketClient client;
    private readonly ICurrencyService cs;
    private readonly DbService db;
    private readonly IBotCredentials creds;
    private readonly HttpClient httpClient;

    private readonly GamblingConfigService gss;

    public GamblingService(DbService db, Mewdeko bot, ICurrencyService cs,
        DiscordSocketClient client, IDataCache cache, GamblingConfigService gss,
        IBotCredentials creds,
        HttpClient httpClient)
    {
        this.db = db;
        this.cs = cs;
        this.client = client;
        this.cache = cache;
        this.gss = gss;
        this.creds = creds;
        this.httpClient = httpClient;

        if (bot.Client.ShardId == 0)
        {
            _ = new Timer(_ =>
            {
                var config = this.gss.Data;
                var maxDecay = config.Decay.MaxDecay;
                if (config.Decay.Percent is <= 0 or > 1 || maxDecay < 0)
                    return;

                using var uow = this.db.GetDbContext();
                var lastCurrencyDecay = this.cache.GetLastCurrencyDecay();

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
WHERE CurrencyAmount > {config.Decay.MinThreshold} AND UserId!={this.client.CurrentUser.Id};");

                this.cache.SetLastCurrencyDecay();
                uow.SaveChanges();
            }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }
    }

    public ConcurrentDictionary<(ulong, ulong), RollDuelGame> Duels { get; } = new();
    public ConcurrentDictionary<ulong, Connect4Game> Connect4Games { get; } = new();

    public async Task<bool> GetVoted(ulong id)
    {
        httpClient.DefaultRequestHeaders.Add("Authorization", creds.VotesToken);
        var tocheck = await httpClient.GetStringAsync($"https://top.gg/api/bots/{client.CurrentUser.Id}/check?userId={id}");
        return tocheck.Contains('1');
    }

    public async Task<EconomyResult> GetEconomy()
    {
        if (cache.TryGetEconomy(out var data))
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

        await using (var uow = db.GetDbContext())
        {
            cash = uow.DiscordUser.GetTotalCurrency();
            onePercent = uow.DiscordUser.GetTopOnePercentCurrency(client.CurrentUser.Id);
            planted = uow.PlantedCurrency.AsQueryable().Sum(x => x.Amount);
            waifus = await uow.WaifuInfo.GetTotalValue();
            bot = await uow.DiscordUser.GetUserCurrency(client.CurrentUser.Id);
        }

        var result = new EconomyResult
        {
            Cash = cash,
            Planted = planted,
            Bot = bot,
            Waifus = waifus,
            OnePercent = onePercent
        };

        cache.SetEconomy(JsonConvert.SerializeObject(result));
        return result;
    }

    public Task<WheelOfFortuneGame.Result> WheelOfFortuneSpinAsync(ulong userId, long bet) => new WheelOfFortuneGame(userId, bet, gss.Data, cs).SpinAsync();

    public struct EconomyResult
    {
        public decimal Cash { get; init; }
        public decimal Planted { get; init; }
        public decimal Waifus { get; init; }
        public decimal OnePercent { get; init; }
        public long Bot { get; init; }
    }
}