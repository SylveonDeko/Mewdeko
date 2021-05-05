using Mewdeko.Core.Services.Database.Models;
using System.Collections.Generic;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface ISuggestionsRepository : IRepository<Suggestions>
    {
        Suggestions[] ForSuggest(ulong guildId, ulong userId, ulong sid);
        Suggestions[] ForId(ulong guildid, ulong sid);
    }
}
