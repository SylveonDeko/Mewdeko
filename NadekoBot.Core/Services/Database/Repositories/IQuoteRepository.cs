using NadekoBot.Core.Services.Database.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NadekoBot.Core.Services.Database.Repositories
{
    public interface IQuoteRepository : IRepository<Quote>
    {
        Task<Quote> GetRandomQuoteByKeywordAsync(ulong guildId, string keyword);
        Task<Quote> SearchQuoteKeywordTextAsync(ulong guildId, string keyword, string text);
        IEnumerable<Quote> GetGroup(ulong guildId, int page, OrderType order);
        void RemoveAllByKeyword(ulong guildId, string keyword);
    }
}
