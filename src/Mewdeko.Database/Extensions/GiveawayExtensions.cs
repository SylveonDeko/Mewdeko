using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class GiveawayExtensions
{
    public static List<Giveaways> GiveawaysForGuild(this DbSet<Giveaways> set, ulong serverId) =>
        set.AsNoTracking()
            .Where(x => x.ServerId == serverId).ToList();
}