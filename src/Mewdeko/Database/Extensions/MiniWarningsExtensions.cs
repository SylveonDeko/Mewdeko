using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class MiniWarningExtensions
{
    public static Warning2[] ForId(this DbSet<Warning2> Set, ulong guildId, ulong userId)
    {
        var query = Set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
                       .OrderByDescending(x => x.DateAdded);

        return query.ToArray();
    }

    public static bool Forgive(this DbSet<Warning2> Set,ulong guildId, ulong userId, string mod, int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        var warn = Set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
                      .OrderByDescending(x => x.DateAdded)
                      .Skip(index)
                      .FirstOrDefault();

        if (warn == null || warn.Forgiven)
            return false;

        warn.Forgiven = true;
        warn.ForgivenBy = mod;
        return true;
    }

    public static async Task ForgiveAll(this DbSet<Warning2> Set, ulong guildId, ulong userId, string mod) =>
        await Set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
                 .ForEachAsync(x =>
                 {
                     if (x.Forgiven) return;
                     x.Forgiven = true;
                     x.ForgivenBy = mod;
                 });

    public static Warning2[] GetForGuild(this DbSet<Warning2> Set, ulong id) => Set.AsQueryable().Where(x => x.GuildId == id).ToArray();
}