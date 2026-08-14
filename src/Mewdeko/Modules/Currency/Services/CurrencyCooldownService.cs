using DataModel;
using LinqToDB;
using LinqToDB.Async;

namespace Mewdeko.Modules.Currency.Services;

/// <summary>
///     Enforces per-user cooldowns on currency actions.
/// </summary>
/// <remarks>
///     Cooldowns were previously derived by scanning a user's entire transaction history for entries
///     whose description matched a localized string. That reset every user's cooldown whenever a guild
///     changed language, and the underlying query grew without bound as the ledger did. Claims here are
///     a single conditional statement, so two commands sent at once cannot both succeed.
/// </remarks>
public class CurrencyCooldownService : INService
{
    /// <summary>
    ///     Cooldown key for the daily reward.
    /// </summary>
    public const string Daily = "daily";

    /// <summary>
    ///     Cooldown key for the work command.
    /// </summary>
    public const string Work = "work";

    /// <summary>
    ///     Cooldown key for the crime command.
    /// </summary>
    public const string Crime = "crime";

    /// <summary>
    ///     Cooldown key for robbery attempts.
    /// </summary>
    public const string Rob = "rob";

    /// <summary>
    ///     Cooldown key for currency transfers.
    /// </summary>
    public const string Pay = "pay";

    /// <summary>
    ///     Cooldown key shared by every wagering game.
    /// </summary>
    public const string Game = "game";

    /// <summary>
    ///     Cooldown key for bank interest accrual.
    /// </summary>
    public const string BankInterest = "bankinterest";

    private readonly IDataConnectionFactory dbFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CurrencyCooldownService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    public CurrencyCooldownService(IDataConnectionFactory dbFactory)
    {
        this.dbFactory = dbFactory;
    }

    /// <summary>
    ///     Attempts to claim a cooldown slot, marking it used if and only if it is ready.
    /// </summary>
    /// <param name="guildId">The guild the action happened in.</param>
    /// <param name="userId">The user performing the action.</param>
    /// <param name="key">The stable cooldown key, such as <see cref="Daily" />.</param>
    /// <param name="cooldown">How long must pass between uses. Zero or less always succeeds.</param>
    /// <param name="trackStreak">
    ///     Whether to maintain a consecutive-use counter. The streak survives as long as the next claim
    ///     lands within two cooldown periods, giving users a full extra window of grace.
    /// </param>
    /// <returns>
    ///     Whether the claim succeeded, how long remains if it did not, and the resulting streak length.
    /// </returns>
    public async Task<CooldownClaim> TryClaimAsync(ulong guildId, ulong userId, string key, TimeSpan cooldown,
        bool trackStreak = false)
    {
        var now = DateTime.UtcNow;

        if (cooldown <= TimeSpan.Zero)
        {
            await TouchAsync(guildId, userId, key, now, trackStreak, now);
            return new CooldownClaim(true, TimeSpan.Zero, 1);
        }

        var readyThreshold = now - cooldown;
        var streakThreshold = now - cooldown - cooldown;

        await using var db = await dbFactory.CreateConnectionAsync();

        var claimed = await db.CurrencyCooldowns
            .Where(x => x.GuildId == guildId && x.UserId == userId && x.CooldownKey == key)
            .Where(x => x.LastUsed <= readyThreshold)
            .Set(x => x.StreakCount, x => !trackStreak ? 0 : x.LastUsed > streakThreshold ? x.StreakCount + 1 : 1)
            .Set(x => x.LastUsed, now)
            .UpdateAsync();

        if (claimed > 0)
        {
            var streak = await db.CurrencyCooldowns
                .Where(x => x.GuildId == guildId && x.UserId == userId && x.CooldownKey == key)
                .Select(x => x.StreakCount)
                .FirstOrDefaultAsync();

            return new CooldownClaim(true, TimeSpan.Zero, streak);
        }

        var existing = await db.CurrencyCooldowns
            .Where(x => x.GuildId == guildId && x.UserId == userId && x.CooldownKey == key)
            .Select(x => (DateTime?)x.LastUsed)
            .FirstOrDefaultAsync();

        if (existing.HasValue)
        {
            var remaining = existing.Value + cooldown - now;
            return new CooldownClaim(false, remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, 0);
        }

        try
        {
            await db.InsertAsync(new CurrencyCooldown
            {
                GuildId = guildId,
                UserId = userId,
                CooldownKey = key,
                LastUsed = now,
                StreakCount = trackStreak ? 1 : 0,
                DateAdded = now
            });

            return new CooldownClaim(true, TimeSpan.Zero, 1);
        }
        catch (Exception)
        {
            return new CooldownClaim(false, cooldown, 0);
        }
    }

    /// <summary>
    ///     Reports how long remains on a cooldown without consuming it.
    /// </summary>
    /// <param name="guildId">The guild the action happens in.</param>
    /// <param name="userId">The user to check.</param>
    /// <param name="key">The stable cooldown key.</param>
    /// <param name="cooldown">How long must pass between uses.</param>
    /// <returns>The remaining time, or <see cref="TimeSpan.Zero" /> if the action is ready.</returns>
    public async Task<TimeSpan> GetRemainingAsync(ulong guildId, ulong userId, string key, TimeSpan cooldown)
    {
        if (cooldown <= TimeSpan.Zero)
            return TimeSpan.Zero;

        await using var db = await dbFactory.CreateConnectionAsync();

        var lastUsed = await db.CurrencyCooldowns
            .Where(x => x.GuildId == guildId && x.UserId == userId && x.CooldownKey == key)
            .Select(x => (DateTime?)x.LastUsed)
            .FirstOrDefaultAsync();

        if (!lastUsed.HasValue)
            return TimeSpan.Zero;

        var remaining = lastUsed.Value + cooldown - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    ///     Gets a user's current consecutive-use streak for a cooldown key.
    /// </summary>
    /// <param name="guildId">The guild to look in.</param>
    /// <param name="userId">The user to check.</param>
    /// <param name="key">The stable cooldown key.</param>
    /// <returns>The streak length, or zero if none is recorded.</returns>
    public async Task<int> GetStreakAsync(ulong guildId, ulong userId, string key)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.CurrencyCooldowns
            .Where(x => x.GuildId == guildId && x.UserId == userId && x.CooldownKey == key)
            .Select(x => x.StreakCount)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Clears a cooldown so the action becomes immediately available again.
    /// </summary>
    /// <param name="guildId">The guild to clear in.</param>
    /// <param name="userId">The user to clear for.</param>
    /// <param name="key">The stable cooldown key.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ResetAsync(ulong guildId, ulong userId, string key)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        await db.CurrencyCooldowns
            .Where(x => x.GuildId == guildId && x.UserId == userId && x.CooldownKey == key)
            .DeleteAsync();
    }

    private async Task TouchAsync(ulong guildId, ulong userId, string key, DateTime now, bool trackStreak,
        DateTime timestamp)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var updated = await db.CurrencyCooldowns
            .Where(x => x.GuildId == guildId && x.UserId == userId && x.CooldownKey == key)
            .Set(x => x.LastUsed, timestamp)
            .UpdateAsync();

        if (updated > 0)
            return;

        try
        {
            await db.InsertAsync(new CurrencyCooldown
            {
                GuildId = guildId,
                UserId = userId,
                CooldownKey = key,
                LastUsed = timestamp,
                StreakCount = trackStreak ? 1 : 0,
                DateAdded = now
            });
        }
        catch (Exception)
        {
        }
    }
}

/// <summary>
///     The outcome of an attempt to claim a cooldown slot.
/// </summary>
/// <param name="Success">Whether the cooldown was ready and has now been consumed.</param>
/// <param name="Remaining">How long remains before the next attempt can succeed.</param>
/// <param name="Streak">The consecutive-use count after this claim, when streaks are tracked.</param>
public readonly record struct CooldownClaim(bool Success, TimeSpan Remaining, int Streak);