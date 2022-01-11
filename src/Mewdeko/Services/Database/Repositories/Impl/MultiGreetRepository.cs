using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class MultiGreetRepository : Repository<MultiGreet>, IMultiGreetRepository
{
    public MultiGreetRepository(DbContext context)
        : base(context)
    {
    }

    public MultiGreet[] GetAllGreets(ulong guildId) => _set.AsQueryable().Where(x => x.GuildId == guildId).ToArray();
    public MultiGreet[] GetForChannel(ulong channelId) => _set.AsQueryable().Where(x => x.ChannelId == channelId).ToArray();
}