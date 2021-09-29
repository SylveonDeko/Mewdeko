using System.Collections.Generic;
using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface IGiveawaysRepository : IRepository<Giveaways>
    {
        IEnumerable<Giveaways> GiveawaysFor(ulong serverId);
    }
}