using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
/// Provides extension methods for querying Giveaways entities.
/// </summary>
public static class GiveawayExtensions
{
    /// <summary>
    /// Retrieves all Giveaways for a specific server.
    /// </summary>
    /// <param name="set">The DbSet of Giveaways entities to query.</param>
    /// <param name="serverId">The ID of the server to filter by.</param>
    /// <returns>A List of Giveaways entities for the specified server.</returns>
    public static List<Giveaways> GiveawaysForGuild(this DbSet<Giveaways> set, ulong serverId) =>
        set.AsNoTracking()
            .Where(x => x.ServerId == serverId).ToList();
}