using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class ConfessionExtensions
{
    public static List<Confessions> ForGuild(this DbSet<Confessions> set, ulong guildId) =>
        set.AsQueryable().Where(x => x.GuildId == guildId).ToList();
}