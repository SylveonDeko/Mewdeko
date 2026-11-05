using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Database.DbContextStuff;

namespace Mewdeko.Modules.Currency.Services;

/// <summary>
///     Manages scheduled earning boosts: windows of time in which what a guild's members earn from chosen commands is
///     raised or lowered by a percent.
/// </summary>
public class EconomyBoostService : INService
{
    /// <summary>
    ///     The key that stands for every game at once.
    /// </summary>
    public const string AllGames = "games";

    /// <summary>
    ///     The earning commands a boost can target.
    /// </summary>
    public static readonly IReadOnlyList<string> EarningCommands =
    [
        CurrencyCooldownService.Daily, CurrencyCooldownService.Work, CurrencyCooldownService.Crime,
        CurrencyCooldownService.Rob
    ];

    /// <summary>
    ///     The game keys whose winnings a boost can target.
    /// </summary>
    public static readonly IReadOnlyList<string> GameCommands =
    [
        "coinflip", "highlow", "blackjack", "slots", "roulette", "rps", "war", "craps", "scratchcard",
        "russianroulette", "baccarat", "minesweeper", "lottery", "crash", "keno", "plinko", "wheelfortune",
        "spinwheel", "duckrace", "bingo", "memory", "horserace", "diceduel", "triviachain"
    ];

    /// <summary>
    ///     The command keys a boost can target.
    /// </summary>
    public static readonly IReadOnlyList<string> BoostableCommands =
        [..EarningCommands, AllGames, ..GameCommands];

    private readonly IDataConnectionFactory dbFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EconomyBoostService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    public EconomyBoostService(IDataConnectionFactory dbFactory)
    {
        this.dbFactory = dbFactory;
    }

    /// <summary>
    ///     Gets a guild's boosts, soonest first.
    /// </summary>
    /// <param name="guildId">The guild to list.</param>
    /// <returns>The guild's boosts.</returns>
    public async Task<IReadOnlyList<EconomyBoost>> GetBoostsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.EconomyBoosts
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.StartsAt)
            .ToListAsync();
    }

    /// <summary>
    ///     Creates a boost.
    /// </summary>
    /// <param name="boost">The boost to add. Its guild must already be set.</param>
    /// <returns>The created boost, or <see langword="null" /> if the name is already taken.</returns>
    public async Task<EconomyBoost?> AddBoostAsync(EconomyBoost boost)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        if (await db.EconomyBoosts.AnyAsync(x =>
                x.GuildId == boost.GuildId && x.Name.ToLower() == boost.Name.ToLower()))
            return null;

        boost.DateAdded = DateTime.UtcNow;
        boost.Id = await db.InsertWithInt32IdentityAsync(boost);
        return boost;
    }

    /// <summary>
    ///     Deletes a boost.
    /// </summary>
    /// <param name="guildId">The guild the boost belongs to.</param>
    /// <param name="name">The boost name.</param>
    /// <returns><see langword="true" /> if a boost was removed.</returns>
    public async Task<bool> RemoveBoostAsync(ulong guildId, string name)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.EconomyBoosts
            .Where(x => x.GuildId == guildId && x.Name.ToLower() == name.ToLower())
            .DeleteAsync() > 0;
    }

    /// <summary>
    ///     Applies a change to an existing boost.
    /// </summary>
    /// <param name="guildId">The guild the boost belongs to.</param>
    /// <param name="name">The boost name.</param>
    /// <param name="mutate">An action applying the desired changes.</param>
    /// <returns>The updated boost, or <see langword="null" /> if no such boost exists.</returns>
    public async Task<EconomyBoost?> UpdateBoostAsync(ulong guildId, string name, Action<EconomyBoost> mutate)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var boost = await db.EconomyBoosts
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Name.ToLower() == name.ToLower());

        if (boost is null)
            return null;

        mutate(boost);
        await db.UpdateAsync(boost);
        return boost;
    }

    /// <summary>
    ///     Adds the active boosts to an amount about to be paid out.
    /// </summary>
    /// <param name="guildId">The guild the earning happens in.</param>
    /// <param name="command">The command key being paid for.</param>
    /// <param name="amount">The amount before any boost.</param>
    /// <returns>The amount after boosts, never below zero.</returns>
    public async Task<long> ApplyAsync(ulong guildId, string command, long amount)
    {
        var percent = await GetActivePercentAsync(guildId, command, DateTime.UtcNow);

        if (percent == 0)
            return amount;

        return Math.Max(0, (long)Math.Round(amount * (100 + (double)percent) / 100, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    ///     Sums the percent change of every boost active for a command at a moment in time.
    /// </summary>
    /// <param name="guildId">The guild to check.</param>
    /// <param name="command">The command key.</param>
    /// <param name="now">The moment to check, in UTC.</param>
    /// <returns>The combined percent change.</returns>
    public async Task<int> GetActivePercentAsync(ulong guildId, string command, DateTime now)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var boosts = await db.EconomyBoosts
            .Where(x => x.GuildId == guildId && x.Enabled)
            .ToListAsync();

        return boosts
            .Where(x => AppliesTo(x, command) && IsActive(x, now))
            .Sum(x => x.PercentChange);
    }

    /// <summary>
    ///     Checks whether a boost targets a command.
    /// </summary>
    /// <param name="boost">The boost.</param>
    /// <param name="command">The command key.</param>
    /// <returns><see langword="true" /> if the boost covers the command.</returns>
    public static bool AppliesTo(EconomyBoost boost, string command)
    {
        if (string.IsNullOrWhiteSpace(boost.Commands))
            return true;

        var listed = boost.Commands
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return listed.Contains(command, StringComparer.OrdinalIgnoreCase) ||
               (listed.Contains(AllGames, StringComparer.OrdinalIgnoreCase) &&
                GameCommands.Contains(command, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Adds the active boosts to the winnings of a game, never to the stake that comes back with them.
    /// </summary>
    /// <param name="guildId">The guild the game was played in.</param>
    /// <param name="gameKey">The game key.</param>
    /// <param name="stake">The amount that was wagered.</param>
    /// <param name="totalReturn">The full amount about to be paid out, stake included.</param>
    /// <returns>The total to pay out. Losses and pushes are returned unchanged.</returns>
    public async Task<long> ApplyToWinningsAsync(ulong guildId, string gameKey, long stake, long totalReturn)
    {
        var profit = totalReturn - Math.Max(0, stake);

        return profit <= 0 ? totalReturn : totalReturn - profit + await ApplyAsync(guildId, gameKey, profit);
    }

    /// <summary>
    ///     Checks whether a boost's window is open at a moment in time.
    /// </summary>
    /// <param name="boost">The boost.</param>
    /// <param name="now">The moment to check, in UTC.</param>
    /// <returns><see langword="true" /> if the window is open.</returns>
    public static bool IsActive(EconomyBoost boost, DateTime now)
    {
        var start = GetLatestStart(boost, now);

        return start.HasValue && now < start.Value.AddMinutes(boost.DurationMinutes);
    }

    /// <summary>
    ///     Gets the start of the most recent window that has opened by a moment in time.
    /// </summary>
    /// <param name="boost">The boost.</param>
    /// <param name="now">The moment to check, in UTC.</param>
    /// <returns>The window start, or <see langword="null" /> if none has opened yet.</returns>
    public static DateTime? GetLatestStart(EconomyBoost boost, DateTime now)
    {
        if (now < boost.StartsAt)
            return null;

        var timeOfDay = boost.StartsAt.TimeOfDay;

        switch ((EconomyBoostSchedule)boost.ScheduleType)
        {
            case EconomyBoostSchedule.Daily:
            {
                var candidate = now.Date + timeOfDay;
                return candidate > now ? candidate.AddDays(-1) : candidate;
            }
            case EconomyBoostSchedule.Weekly:
            {
                var back = ((int)now.DayOfWeek - (int)boost.StartsAt.DayOfWeek + 7) % 7;
                var candidate = now.Date.AddDays(-back) + timeOfDay;
                return candidate > now ? candidate.AddDays(-7) : candidate;
            }
            case EconomyBoostSchedule.Monthly:
            {
                var candidate = MonthlyStart(boost.StartsAt.Day, now.Year, now.Month, timeOfDay);

                if (candidate <= now)
                    return candidate;

                var previous = now.Date.AddDays(-now.Day);
                return MonthlyStart(boost.StartsAt.Day, previous.Year, previous.Month, timeOfDay);
            }
            default:
                return boost.StartsAt;
        }
    }

    private static DateTime MonthlyStart(int day, int year, int month, TimeSpan timeOfDay)
    {
        return new DateTime(year, month, Math.Min(day, DateTime.DaysInMonth(year, month))) + timeOfDay;
    }
}
