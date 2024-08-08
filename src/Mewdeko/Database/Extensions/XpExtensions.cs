using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
/// Provides extension methods for querying and manipulating UserXpStats entities.
/// </summary>
public static class XpExtensions
{
    /// <summary>
    /// Retrieves or creates a UserXpStats entity for a specific user in a guild.
    /// </summary>
    /// <param name="set">The DbSet of UserXpStats entities to query.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the UserXpStats entity for the specified user and guild.</returns>
    public static async Task<UserXpStats> GetOrCreateUser(this DbSet<UserXpStats> set, ulong guildId, ulong userId)
    {
        var usr = await set.FirstOrDefaultAsyncEF(x => x.UserId == userId && x.GuildId == guildId);

        if (usr == null)
        {
            await set.AddAsync(usr = new UserXpStats
            {
                Xp = 0, UserId = userId, NotifyOnLevelUp = XpNotificationLocation.None, GuildId = guildId
            });
        }

        return usr;
    }

    /// <summary>
    /// Retrieves a paged list of UserXpStats entities for a specific guild, ordered by total XP.
    /// </summary>
    /// <param name="set">The DbSet of UserXpStats entities to query.</param>
    /// <param name="guildId">The ID of the guild to filter by.</param>
    /// <param name="page">The page number (zero-based) to retrieve.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a List of UserXpStats entities for the specified guild, ordered and paged.</returns>
    public static Task<List<UserXpStats>> GetUsersFor(this DbSet<UserXpStats> set, ulong guildId, int page) =>
        set.AsQueryable().AsNoTracking().Where(x => x.GuildId == guildId).OrderByDescending(x => x.Xp + x.AwardedXp)
            .Skip(page * 9).Take(9).ToListAsyncEF();

    /// <summary>
    /// Retrieves all UserXpStats entities for a specific guild, ordered by total XP.
    /// </summary>
    /// <param name="set">The DbSet of UserXpStats entities to query.</param>
    /// <param name="guildId">The ID of the guild to filter by.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a List of all UserXpStats entities for the specified guild, ordered by total XP.</returns>
    public static Task<List<UserXpStats>> GetTopUserXps(this DbSet<UserXpStats> set, ulong guildId) =>
        set.AsQueryable().AsNoTracking().Where(x => x.GuildId == guildId).OrderByDescending(x => x.Xp + x.AwardedXp)
            .ToListAsyncEF();

    /// <summary>
    /// Calculates the ranking of a user within a guild based on total XP.
    /// </summary>
    /// <param name="set">The DbSet of UserXpStats entities to query.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The ranking of the user within the guild.</returns>
    public static int GetUserGuildRanking(this DbSet<UserXpStats> set, ulong userId, ulong guildId) =>
        set.AsQueryable().AsNoTracking().Count(x =>
            x.GuildId == guildId
            && x.Xp + x.AwardedXp
            > set.AsQueryable().Where(y => y.UserId == userId && y.GuildId == guildId).Select(y => y.Xp + y.AwardedXp)
                .FirstOrDefault())
        + 1;

    /// <summary>
    /// Resets the XP for a specific user in a guild.
    /// </summary>
    /// <param name="set">The DbSet of UserXpStats entities to query.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="guildId">The ID of the guild.</param>
    public static void ResetGuildUserXp(this DbSet<UserXpStats> set, ulong userId, ulong guildId) =>
        set.Delete(x => x.UserId == userId && x.GuildId == guildId);

    /// <summary>
    /// Resets the XP for all users in a guild.
    /// </summary>
    /// <param name="set">The DbSet of UserXpStats entities to query.</param>
    /// <param name="guildId">The ID of the guild.</param>
    public static void ResetGuildXp(this DbSet<UserXpStats> set, ulong guildId) =>
        set.Delete(x => x.GuildId == guildId);
}