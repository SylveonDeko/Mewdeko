using System.Collections.Generic;
using Mewdeko.Common;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class QuoteRepository : Repository<Quote>, IQuoteRepository
{
    public QuoteRepository(DbContext context) : base(context)
    {
    }

    public IEnumerable<Quote> GetGroup(ulong guildId, int page, OrderType order)
    {
        var q = Set.AsQueryable().Where(x => x.GuildId == guildId);
        q = order == OrderType.Keyword ? q.OrderBy(x => x.Keyword) : q.OrderBy(x => x.Id);

        return q.Skip(15 * page).Take(15).ToArray();
    }

    public async Task<Quote> GetRandomQuoteByKeywordAsync(ulong guildId, string keyword)
    {
        var rng = new MewdekoRandom();
        return (await Set.AsQueryable()
                .Where(q => q.GuildId == guildId && q.Keyword == keyword)
                .ToListAsync())
            .OrderBy(_ => rng.Next())
            .FirstOrDefault();
    }

    public async Task<Quote> SearchQuoteKeywordTextAsync(ulong guildId, string keyword, string text)
    {
        var rngk = new MewdekoRandom();
        return (await Set.AsQueryable()
                .Where(q => q.GuildId == guildId
                            && q.Keyword == keyword
                            && EF.Functions.Like(q.Text.ToUpper(), $"%{text.ToUpper()}%")
                    // && q.Text.Contains(text, StringComparison.OrdinalIgnoreCase)
                )
                .ToListAsync())
            .OrderBy(_ => rngk.Next())
            .FirstOrDefault();
    }

    public void RemoveAllByKeyword(ulong guildId, string keyword) => Set.RemoveRange(Set.AsQueryable().Where(x => x.GuildId == guildId && x.Keyword.ToUpper() == keyword));
}