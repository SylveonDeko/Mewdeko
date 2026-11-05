using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Npgsql;

namespace Mewdeko.Modules.Games.Services;

/// <summary>
///     One member's best roll of a month.
/// </summary>
/// <param name="UserId">The member's user ID.</param>
/// <param name="BestRoll">Their highest roll that month.</param>
/// <param name="Rolls">How many days they rolled that month.</param>
public record DailyDiceStanding(ulong UserId, int BestRoll, int Rolls);

/// <summary>
///     Runs the daily dice game: one roll per member per day, with the best roll of the month winning.
/// </summary>
/// <param name="dbFactory">The database connection factory.</param>
public class DailyDiceService(IDataConnectionFactory dbFactory) : INService
{
    /// <summary>
    ///     The highest number a roll can reach.
    /// </summary>
    public const int Sides = 100;

    /// <summary>
    ///     Rolls for a member unless they already rolled today.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member's user ID.</param>
    /// <returns>Whether this was a new roll, and the roll for today.</returns>
    public async Task<(bool Rolled, int Roll)> RollAsync(ulong guildId, ulong userId)
    {
        var today = DateTime.UtcNow.Date;
        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = await db.DailyDiceRolls
            .Where(x => x.GuildId == guildId && x.UserId == userId && x.RollDate == today)
            .Select(x => (int?)x.Roll)
            .FirstOrDefaultAsync();
        if (existing is { } previous)
            return (false, previous);

        var roll = Random.Shared.Next(1, Sides + 1);
        try
        {
            await db.InsertAsync(new DailyDiceRoll
            {
                GuildId = guildId, UserId = userId, RollDate = today, Roll = roll, RolledAt = DateTime.UtcNow
            });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            var stored = await db.DailyDiceRolls
                .Where(x => x.GuildId == guildId && x.UserId == userId && x.RollDate == today)
                .Select(x => x.Roll)
                .FirstAsync();
            return (false, stored);
        }

        return (true, roll);
    }

    /// <summary>
    ///     Gets the standings for a month: best roll first, earlier rolls winning ties.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="monthStart">The first day of the month, in UTC.</param>
    /// <param name="limit">The most members to return.</param>
    /// <returns>The standings.</returns>
    public async Task<List<DailyDiceStanding>> GetStandingsAsync(ulong guildId, DateTime monthStart, int limit = 10)
    {
        var next = monthStart.AddMonths(1);
        await using var db = await dbFactory.CreateConnectionAsync();

        var rows = await db.DailyDiceRolls
            .Where(x => x.GuildId == guildId && x.RollDate >= monthStart && x.RollDate < next)
            .Select(x => new
            {
                x.UserId, x.Roll, x.RolledAt
            })
            .ToListAsync();

        return rows
            .GroupBy(x => x.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Best = g.Max(x => x.Roll),
                Rolls = g.Count(),
                First = g.Where(x => x.Roll == g.Max(y => y.Roll)).Min(x => x.RolledAt)
            })
            .OrderByDescending(x => x.Best)
            .ThenBy(x => x.First)
            .Take(limit)
            .Select(x => new DailyDiceStanding(x.UserId, x.Best, x.Rolls))
            .ToList();
    }
}
