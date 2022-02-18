using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class AfkExtensions
{
    public static AFK[] ForGuild(this DbSet<AFK> set, ulong guildId) =>
        set
            .AsQueryable()
            .AsNoTracking().
            Where(x => x.GuildId == guildId).ToArray();

    public static AFK[] GetAll(this DbSet<AFK> set) => 
        set.AsQueryable().AsNoTracking().ToArray();
}