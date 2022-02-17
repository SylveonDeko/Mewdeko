using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class SnipeExtensions
{
    public static List<SnipeStore> ForGuild(this DbSet<SnipeStore> set, ulong guildId) =>
        set
            .AsQueryable()
            .AsNoTracking()
            .Where(x => x.GuildId == guildId).ToList();
}