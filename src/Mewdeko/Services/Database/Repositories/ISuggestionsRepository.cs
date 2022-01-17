using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories;

public interface ISuggestionsRepository : IRepository<Suggestionse>
{
    Suggestionse[] ForId(ulong guildid, ulong sid);
    Suggestionse[] ForUser(ulong guildId, ulong userId);
}