using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
///     Provides extension methods for querying Highlights and HighlightSettings entities.
/// </summary>
public static class HighlightExtensions
{
    /// <summary>
    ///     Retrieves all Highlights for a specific user in a guild.
    /// </summary>
    /// <param name="set">The DbSet of Highlights entities to query.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a List of Highlights for the
    ///     specified user and guild.
    /// </returns>
    public static async Task<List<Highlights>> ForUser(this DbSet<Highlights> set, ulong guildId, ulong userId)
    {
        return await set.AsQueryable().Where(x => x.UserId == userId && x.GuildId == guildId).ToListAsyncEF();
    }

    /// <summary>
    ///     Retrieves all HighlightSettings for a specific user in a guild.
    /// </summary>
    /// <param name="set">The DbSet of HighlightSettings entities to query.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a List of HighlightSettings for
    ///     the specified user and guild.
    /// </returns>
    public static async Task<List<HighlightSettings>> ForUser(this DbSet<HighlightSettings> set, ulong guildId,
        ulong userId)
    {
        return await set.AsQueryable().Where(x => x.UserId == userId && x.GuildId == guildId).ToListAsyncEF();
    }

    /// <summary>
    ///     Retrieves all HighlightSettings.
    /// </summary>
    /// <param name="set">The DbSet of HighlightSettings entities to query.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a List of all HighlightSettings.</returns>
    public static async Task<List<HighlightSettings>> AllHighlightSettings(this DbSet<HighlightSettings> set)
    {
        return await set.AsQueryable().ToListAsyncEF();
    }

    /// <summary>
    ///     Retrieves all Highlights.
    /// </summary>
    /// <param name="set">The DbSet of Highlights entities to query.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a List of all Highlights.</returns>
    public static async Task<List<Highlights>> AllHighlights(this DbSet<Highlights> set)
    {
        return await set.AsQueryable().ToListAsyncEF();
    }
}