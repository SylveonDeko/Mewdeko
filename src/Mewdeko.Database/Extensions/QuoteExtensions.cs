using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class QuoteExtensions
{
    public static IEnumerable<Quote> GetForGuild(this DbSet<Quote> quotes, ulong guildId) => quotes.AsQueryable().Where(x => x.GuildId == guildId);

    public static IEnumerable<Quote> GetGroup(this DbSet<Quote> quotes, ulong guildId, int page, OrderType order)
    {
        var q = quotes.AsQueryable().Where(x => x.GuildId == guildId);
        if (order == OrderType.Keyword)
            q = q.OrderBy(x => x.Keyword);
        else
            q = q.OrderBy(x => x.Id);

        return q.Skip(15 * page).Take(15).ToArray();
    }

    public static async Task<Quote> GetRandomQuoteByKeywordAsync(this DbSet<Quote> quotes, ulong guildId, string keyword)
    {
        var rng = new Random();
        return (await quotes.AsQueryable()
            .Where(q => q.GuildId == guildId && q.Keyword == keyword)
            .ToListAsync().ConfigureAwait(false)).MinBy(_ => rng.Next());
    }

    public static async Task<Quote> SearchQuoteKeywordTextAsync(this DbSet<Quote> quotes, ulong guildId, string keyword, string text)
    {
        var rngk = new Random();
        return (await quotes.AsQueryable()
            .Where(q => q.GuildId == guildId
                        && q.Keyword == keyword
                        && EF.Functions.Like(q.Text.ToUpper(), $"%{text.ToUpper()}%")
                // && q.Text.Contains(text, StringComparison.OrdinalIgnoreCase)
            )
            .ToListAsync().ConfigureAwait(false)).MinBy(_ => rngk.Next());
    }

    public static void RemoveAllByKeyword(this DbSet<Quote> quotes, ulong guildId, string keyword) =>
        quotes.RemoveRange(quotes.AsQueryable().Where(x => x.GuildId == guildId && x.Keyword.ToUpper() == keyword));
}