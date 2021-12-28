using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories;

public interface ISuggestionsRepository : IRepository<Suggestionse>
{
    Suggestionse[] ForSuggest(ulong guildId, ulong userId, ulong sid);
    Suggestionse[] ForId(ulong guildid, ulong sid);
}