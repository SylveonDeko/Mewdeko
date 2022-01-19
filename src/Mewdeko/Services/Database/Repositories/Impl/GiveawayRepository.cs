using System.Collections.Generic;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class GiveawayRepository : Repository<Giveaways>, IGiveawaysRepository
{
    public GiveawayRepository(DbContext context) : base(context)
    {
    }

    public IEnumerable<Giveaways> GiveawaysFor(ulong serverId, int page) =>
        Set.AsQueryable()
            .Where(x => x.ServerId == serverId)
            .OrderBy(x => x.DateAdded)
            .Skip(page * 10)
            .Take(10);

    public List<Giveaways> GiveawaysForGuild(ulong serverId) =>
        Set.AsQueryable()
            .Where(x => x.ServerId == serverId).ToList();
}