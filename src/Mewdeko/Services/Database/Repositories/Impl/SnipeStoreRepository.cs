using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class SnipeStoreRepository : Repository<SnipeStore>, ISnipeStoreRepository
{
    public SnipeStoreRepository(DbContext context) : base(context)
    {
    }

    public SnipeStore[] ForChannel(ulong guildId, ulong chanid)
    {
        var query = Set.AsQueryable().Where(x => x.GuildId == guildId && x.ChannelId == chanid);

        return query.ToArray();
    }

    public SnipeStore[] All()
    {
        var query = Set.AsQueryable();

        return query.ToArray();
    }
}