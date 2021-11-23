using System.Collections.Generic;
using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories
{
    public interface IGiveawaysRepository : IRepository<Giveaways>
    {
        IEnumerable<Giveaways> GiveawaysFor(ulong serverId, int page);
        List<Giveaways> GiveawaysForGuild(ulong id);
    }
}