using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface ISuggestionsRepository : IRepository<Suggestionse>
    {
        Suggestionse[] ForSuggest(ulong guildId, ulong userId, ulong sid);
        Suggestionse[] ForId(ulong guildid, ulong sid);
    }
}