using LinqToDB.Async;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Currency.Models;

namespace Mewdeko.Modules.Currency.Services;

/// <summary>
///     Reports on the health of a guild's economy from the transaction ledger.
/// </summary>
/// <remarks>
///     The ledger already recorded every movement but nothing ever read it back, so payout rates were
///     tuned by guesswork. Because each row now carries a stable category and source, the same table
///     answers where currency comes from, where it goes, and whether a given game is actually paying out
///     at the rate it was designed to.
/// </remarks>
public class CurrencyAnalyticsService : INService
{
    private readonly ICurrencyService currencyService;
    private readonly IDataConnectionFactory dbFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CurrencyAnalyticsService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="currencyService">The currency service, used to resolve balances and ledger scope.</param>
    public CurrencyAnalyticsService(IDataConnectionFactory dbFactory, ICurrencyService currencyService)
    {
        this.dbFactory = dbFactory;
        this.currencyService = currencyService;
    }

    /// <summary>
    ///     Measures the current money supply and how concentrated it is.
    /// </summary>
    /// <param name="guildId">The guild to measure.</param>
    /// <returns>A snapshot of the guild's holdings.</returns>
    public async Task<EconomySnapshot> GetSnapshotAsync(ulong guildId)
    {
        var all = (await currencyService.GetAllUserBalancesAsync(guildId)).ToList();

        var balances = all
            .Where(x => x.NetWorth > 0)
            .Select(x => x.NetWorth)
            .OrderBy(x => x)
            .ToList();

        var snapshot = new EconomySnapshot
        {
            Holders = balances.Count, InWallets = all.Sum(x => x.Balance), InBanks = all.Sum(x => x.Bank)
        };

        snapshot.MoneySupply = snapshot.InWallets + snapshot.InBanks;

        if (balances.Count == 0)
            return snapshot;

        snapshot.Mean = snapshot.MoneySupply / balances.Count;
        snapshot.Median = balances[balances.Count / 2];
        snapshot.Gini = CalculateGini(balances);

        var topTenCount = Math.Max(1, balances.Count / 10);
        var topTenTotal = balances.Skip(balances.Count - topTenCount).Sum();
        snapshot.TopTenPercentShare = snapshot.MoneySupply <= 0 ? 0 : (double)topTenTotal / snapshot.MoneySupply;

        return snapshot;
    }

    /// <summary>
    ///     Breaks down currency creation and destruction by category over a window.
    /// </summary>
    /// <param name="guildId">The guild to report on.</param>
    /// <param name="window">How far back to look.</param>
    /// <returns>One bucket per category that saw activity, largest net effect first.</returns>
    public async Task<IReadOnlyList<FlowBucket>> GetFlowAsync(ulong guildId, TimeSpan window)
    {
        var scopeId = currencyService.ResolveLedgerGuildId(guildId);
        var since = DateTime.UtcNow - window;

        await using var db = await dbFactory.CreateConnectionAsync();

        var rows = await db.TransactionHistories
            .Where(x => x.GuildId == scopeId && x.DateAdded >= since)
            .GroupBy(x => x.Category)
            .Select(g => new
            {
                Category = g.Key,
                In = g.Where(x => x.Amount > 0).Sum(x => (long?)x.Amount) ?? 0,
                Out = g.Where(x => x.Amount < 0).Sum(x => (long?)x.Amount) ?? 0,
                Entries = g.Count()
            })
            .ToListAsync();

        return rows
            .Select(x => new FlowBucket(x.Category, x.In, -x.Out, x.Entries))
            .OrderByDescending(x => Math.Abs(x.Net))
            .ToList();
    }

    /// <summary>
    ///     Measures what each game actually returned to players, against what it took in.
    /// </summary>
    /// <param name="guildId">The guild to report on.</param>
    /// <param name="window">How far back to look.</param>
    /// <returns>One entry per game that saw play, most wagered first.</returns>
    public async Task<IReadOnlyList<GamePerformance>> GetGamePerformanceAsync(ulong guildId, TimeSpan window)
    {
        var scopeId = currencyService.ResolveLedgerGuildId(guildId);
        var since = DateTime.UtcNow - window;
        var bet = nameof(CurrencyCategory.GameBet);
        var payout = nameof(CurrencyCategory.GamePayout);

        await using var db = await dbFactory.CreateConnectionAsync();

        var rows = await db.TransactionHistories
            .Where(x => x.GuildId == scopeId
                        && x.DateAdded >= since
                        && x.Source != null
                        && (x.Category == bet || x.Category == payout))
            .GroupBy(x => x.Source)
            .Select(g => new
            {
                Game = g.Key,
                Wagered = g.Where(x => x.Category == bet).Sum(x => (long?)x.Amount) ?? 0,
                Returned = g.Where(x => x.Category == payout).Sum(x => (long?)x.Amount) ?? 0,
                Plays = g.Count(x => x.Category == bet),
                Players = g.Select(x => x.UserId).Distinct().Count()
            })
            .ToListAsync();

        return rows
            .Select(x => new GamePerformance(x.Game ?? "unknown", -x.Wagered, x.Returned, x.Plays, x.Players))
            .OrderByDescending(x => x.Wagered)
            .ToList();
    }

    /// <summary>
    ///     Reports the daily net change in the money supply.
    /// </summary>
    /// <remarks>
    ///     Summing every ledger amount works as a supply delta because transfers between users write
    ///     matching entries on both sides, so only genuine creation and destruction survive the sum.
    /// </remarks>
    /// <param name="guildId">The guild to report on.</param>
    /// <param name="days">How many days back to report.</param>
    /// <returns>One point per day that saw activity, oldest first.</returns>
    public async Task<IReadOnlyList<SupplyPoint>> GetSupplyHistoryAsync(ulong guildId, int days)
    {
        var scopeId = currencyService.ResolveLedgerGuildId(guildId);
        var since = DateTime.UtcNow.Date.AddDays(-days);

        await using var db = await dbFactory.CreateConnectionAsync();

        var rows = await db.TransactionHistories
            .Where(x => x.GuildId == scopeId && x.DateAdded >= since)
            .GroupBy(x => x.DateAdded!.Value.Date)
            .Select(g => new
            {
                Date = g.Key, Net = g.Sum(x => (long?)x.Amount) ?? 0
            })
            .ToListAsync();

        return rows
            .Select(x => new SupplyPoint(x.Date, x.Net))
            .OrderBy(x => x.Date)
            .ToList();
    }

    /// <summary>
    ///     Sums the currency destroyed as transfer tax over a window.
    /// </summary>
    /// <remarks>
    ///     Tax gets no ledger row of its own, deliberately: it is the gap between what senders paid and
    ///     what recipients received, and writing it separately would double-count it in every supply figure.
    /// </remarks>
    /// <param name="guildId">The guild to report on.</param>
    /// <param name="window">How far back to look.</param>
    /// <returns>The total tax destroyed.</returns>
    public async Task<long> GetTransferTaxAsync(ulong guildId, TimeSpan window)
    {
        var scopeId = currencyService.ResolveLedgerGuildId(guildId);
        var since = DateTime.UtcNow - window;
        var sent = nameof(CurrencyCategory.PaySent);
        var received = nameof(CurrencyCategory.PayReceived);

        await using var db = await dbFactory.CreateConnectionAsync();

        var net = await db.TransactionHistories
            .Where(x => x.GuildId == scopeId
                        && x.DateAdded >= since
                        && (x.Category == sent || x.Category == received))
            .SumAsync(x => (long?)x.Amount) ?? 0;

        return net < 0 ? -net : 0;
    }

    /// <summary>
    ///     Computes the Gini coefficient of a distribution.
    /// </summary>
    /// <param name="sortedValues">Holdings sorted ascending.</param>
    /// <returns>A value from 0 for perfect equality to near 1 for total concentration.</returns>
    private static double CalculateGini(IReadOnlyList<long> sortedValues)
    {
        var n = sortedValues.Count;

        if (n <= 1)
            return 0;

        double total = 0;
        double weighted = 0;

        for (var i = 0; i < n; i++)
        {
            total += sortedValues[i];
            weighted += (double)(i + 1) * sortedValues[i];
        }

        if (total <= 0)
            return 0;

        return (2 * weighted / (n * total)) - ((double)(n + 1) / n);
    }
}