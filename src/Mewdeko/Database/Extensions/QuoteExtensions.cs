using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
///     Provides extension methods for querying Quote entities.
/// </summary>
public static class QuoteExtensions
{
    /// <summary>
    ///     Retrieves all Quote entities for a specific guild.
    /// </summary>
    /// <param name="quotes">The DbSet of Quote entities to query.</param>
    /// <param name="guildId">The ID of the guild to filter by.</param>
    /// <returns>An IEnumerable of Quote entities for the specified guild.</returns>
    public static IEnumerable<Quote> GetForGuild(this DbSet<Quote> quotes, ulong guildId)
    {
        return quotes.AsQueryable().Where(x => x.GuildId == guildId);
    }

    /// <summary>
    ///     Retrieves a group of Quote entities for a specific guild, ordered and paged.
    /// </summary>
    /// <param name="quotes">The DbSet of Quote entities to query.</param>
    /// <param name="guildId">The ID of the guild to filter by.</param>
    /// <param name="page">The page number (zero-based) to retrieve.</param>
    /// <param name="order">The order type to apply to the results.</param>
    /// <returns>An IEnumerable of Quote entities for the specified guild, ordered and paged.</returns>
    public static IEnumerable<Quote> GetGroup(this DbSet<Quote> quotes, ulong guildId, int page, OrderType order)
    {
        var q = quotes.AsQueryable().Where(x => x.GuildId == guildId);
        if (order == OrderType.Keyword)
            q = q.OrderBy(x => x.Keyword);
        else
            q = q.OrderBy(x => x.Id);

        return q.Skip(15 * page).Take(15).ToArray();
    }

    /// <summary>
    ///     Retrieves a random Quote entity for a specific guild and keyword.
    /// </summary>
    /// <param name="quotes">The DbSet of Quote entities to query.</param>
    /// <param name="guildId">The ID of the guild to filter by.</param>
    /// <param name="keyword">The keyword to filter by.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a random Quote entity matching the
    ///     criteria, or null if none found.
    /// </returns>
    public static async Task<Quote> GetRandomQuoteByKeywordAsync(this DbSet<Quote> quotes, ulong guildId,
        string keyword)
    {
        var rng = new Random();
        return (await quotes.AsQueryable()
            .Where(q => q.GuildId == guildId && q.Keyword == keyword)
            .ToListAsync().ConfigureAwait(false)).MinBy(_ => rng.Next());
    }

    /// <summary>
    ///     Searches for a Quote entity by guild ID, keyword, and text content.
    /// </summary>
    /// <param name="quotes">The DbSet of Quote entities to query.</param>
    /// <param name="guildId">The ID of the guild to filter by.</param>
    /// <param name="keyword">The keyword to filter by.</param>
    /// <param name="text">The text content to search for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a random Quote entity matching the
    ///     criteria, or null if none found.
    /// </returns>
    public static async Task<Quote> SearchQuoteKeywordTextAsync(this DbSet<Quote> quotes, ulong guildId, string keyword,
        string text)
    {
        var rngk = new Random();
        return (await quotes.AsQueryable()
            .Where(q => q.GuildId == guildId
                        && q.Keyword == keyword
                        && EF.Functions.Like(q.Text.ToUpper(), $"%{text.ToUpper()}%")
            )
            .ToListAsync().ConfigureAwait(false)).MinBy(_ => rngk.Next());
    }

    /// <summary>
    ///     Removes all Quote entities for a specific guild and keyword.
    /// </summary>
    /// <param name="quotes">The DbSet of Quote entities to query.</param>
    /// <param name="guildId">The ID of the guild to filter by.</param>
    /// <param name="keyword">The keyword to filter by.</param>
    public static void RemoveAllByKeyword(this DbSet<Quote> quotes, ulong guildId, string keyword)
    {
        quotes.RemoveRange(quotes.AsQueryable().Where(x => x.GuildId == guildId && x.Keyword.ToUpper() == keyword));
    }
}