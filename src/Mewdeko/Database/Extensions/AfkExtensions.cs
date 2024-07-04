using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
/// Provides extension methods for querying Afk entities.
/// </summary>
public static class AfkExtensions
{
    /// <summary>
    /// Retrieves all Afk entries for a specific guild.
    /// </summary>
    /// <param name="set">The DbSet of Afk entities to query.</param>
    /// <param name="guildId">The ID of the guild to filter by.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains an array of Afk entities for the specified guild.
    /// </returns>
    public static Task<Afk[]> ForGuild(this DbSet<Afk> set, ulong guildId) =>
        set
            .AsQueryable()
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .ToArrayAsyncEF();

    /// <summary>
    /// Retrieves all Afk entries from the database.
    /// </summary>
    /// <param name="set">The DbSet of Afk entities to query.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains an array of all Afk entities in the database.
    /// </returns>
    public static Task<Afk[]> GetAll(this DbSet<Afk> set) =>
        set.AsQueryable().AsNoTracking().ToArrayAsyncEF();
}