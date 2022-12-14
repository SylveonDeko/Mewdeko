using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class MiniWarningExtensions
{
    public static Warning2[] ForId(this DbSet<Warning2> set, ulong guildId, ulong userId)
    {
        var query = set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
            .OrderByDescending(x => x.DateAdded);

        return query.ToArray();
    }

    public static async Task<bool> Forgive(this DbSet<Warning2> set, ulong guildId, ulong userId, string mod, int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        var warn = await set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
            .OrderByDescending(x => x.DateAdded)
            .Skip(index)
            .FirstOrDefaultAsyncEF();

        if (warn == null || warn.Forgiven)
            return false;

        warn.Forgiven = true;
        warn.ForgivenBy = mod;
        return true;
    }

    public static async Task ForgiveAll(this DbSet<Warning2> set, ulong guildId, ulong userId, string mod) =>
        await set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
            .ForEachAsync(x =>
            {
                if (x.Forgiven) return;
                x.Forgiven = true;
                x.ForgivenBy = mod;
            }).ConfigureAwait(false);

    public static Warning2[] GetForGuild(this DbSet<Warning2> set, ulong id) => set.AsQueryable().Where(x => x.GuildId == id).ToArray();
}