using System.Collections.Generic;
using System.Linq;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl
{
    public class GiveawayRepository : Repository<Giveaways>, IGiveawaysRepository
    {
        public GiveawayRepository(DbContext context) : base(context)
        {
        }

        public IEnumerable<Giveaways> GiveawaysFor(ulong serverId)
        {
            return _set.AsQueryable()
                .Where(x => x.ServerId == serverId).ToList();
        }
    }
}