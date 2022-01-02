using System.Collections.Generic;
using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories;

public interface IQuoteRepository : IRepository<Quote>
{
    Task<Quote> GetRandomQuoteByKeywordAsync(ulong guildId, string keyword);
    Task<Quote> SearchQuoteKeywordTextAsync(ulong guildId, string keyword, string text);
    IEnumerable<Quote> GetGroup(ulong guildId, int page, OrderType order);
    void RemoveAllByKeyword(ulong guildId, string keyword);
}