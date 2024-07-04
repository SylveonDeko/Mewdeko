using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
/// Provides extension methods for querying SuggestionsModel entities.
/// </summary>
public static class SuggestionsExtensions
{
    /// <summary>
    /// Retrieves SuggestionsModel entities for a specific guild and suggestion ID.
    /// </summary>
    /// <param name="set">The DbSet of SuggestionsModel entities to query.</param>
    /// <param name="guildId">The ID of the guild to filter by.</param>
    /// <param name="sugid">The suggestion ID to filter by.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an array of SuggestionsModel entities matching the criteria.</returns>
    public static Task<SuggestionsModel[]> ForId(this DbSet<SuggestionsModel> set, ulong guildId, ulong sugid)
        => set.AsQueryable().Where(x => x.GuildId == guildId && x.SuggestionId == sugid).ToArrayAsyncEF();

    /// <summary>
    /// Retrieves SuggestionsModel entities for a specific guild and user.
    /// </summary>
    /// <param name="set">The DbSet of SuggestionsModel entities to query.</param>
    /// <param name="guildId">The ID of the guild to filter by.</param>
    /// <param name="userId">The ID of the user to filter by.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an array of SuggestionsModel entities matching the criteria.</returns>
    public static Task<SuggestionsModel[]> ForUser(this DbSet<SuggestionsModel> set, ulong guildId, ulong userId)
        => set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId).ToArrayAsyncEF();
}