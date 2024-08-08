using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
/// Provides extension methods for querying Confessions entities.
/// </summary>
public static class ConfessionExtensions
{
    /// <summary>
    /// Retrieves all Confession entries for a specific guild.
    /// </summary>
    /// <param name="set">The DbSet of Confessions entities to query.</param>
    /// <param name="guildId">The ID of the guild to filter by.</param>
    /// <returns>
    /// A List of Confessions entities for the specified guild.
    /// </returns>
    public static List<Confessions> ForGuild(this DbSet<Confessions> set, ulong guildId) =>
        set.AsNoTracking().Where(x => x.GuildId == guildId).ToList();
}