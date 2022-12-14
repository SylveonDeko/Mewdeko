using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class MultiGreetExtensions
{
    public static MultiGreet[] GetAllGreets(this DbSet<MultiGreet> set, ulong guildId)
        => set.AsQueryable().Where(x => x.GuildId == guildId).ToArray();

    public static MultiGreet[] GetForChannel(this DbSet<MultiGreet> set, ulong channelId)
        => set.AsQueryable().Where(x => x.ChannelId == channelId).ToArray();
}