using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
/// Provides extension methods for querying MultiGreet entities.
/// </summary>
public static class MultiGreetExtensions
{
    /// <summary>
    /// Retrieves all MultiGreet entities for a specific guild.
    /// </summary>
    /// <param name="set">The DbSet of MultiGreet entities to query.</param>
    /// <param name="guildId">The ID of the guild to filter by.</param>
    /// <returns>An array of MultiGreet entities for the specified guild.</returns>
    public static MultiGreet[] GetAllGreets(this DbSet<MultiGreet> set, ulong guildId)
        => set.AsQueryable().Where(x => x.GuildId == guildId).ToArray();

    /// <summary>
    /// Retrieves all MultiGreet entities for a specific channel.
    /// </summary>
    /// <param name="set">The DbSet of MultiGreet entities to query.</param>
    /// <param name="channelId">The ID of the channel to filter by.</param>
    /// <returns>An array of MultiGreet entities for the specified channel.</returns>
    public static MultiGreet[] GetForChannel(this DbSet<MultiGreet> set, ulong channelId)
        => set.AsQueryable().Where(x => x.ChannelId == channelId).ToArray();
}