using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class AfkExtensions
{
    public static Afk[] ForGuild(this DbSet<Afk> set, ulong guildId) =>
        set
            .AsQueryable()
            .AsNoTracking().
            Where(x => x.GuildId == guildId).ToArray();

    public static Afk[] GetAll(this DbSet<Afk> set) => 
        set.AsQueryable().AsNoTracking().ToArray();
}