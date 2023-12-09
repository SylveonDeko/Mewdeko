using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class WarningExtensions
{
    public static Warning[] ForId(this DbSet<Warning> set, ulong guildId, ulong userId)
    {
        var query = set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
            .OrderByDescending(x => x.DateAdded);

        return query.ToArray();
    }

    public static async Task<bool> Forgive(this DbSet<Warning> set, ulong guildId, ulong userId, string mod, int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        var warn = await set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
            .OrderByDescending(x => x.DateAdded)
            .Skip(index)
            .FirstOrDefaultAsyncEF();

        if (warn == null || warn.Forgiven == 1)
            return false;

        warn.Forgiven = 1;
        warn.ForgivenBy = mod;
        return true;
    }

    public static Task ForgiveAll(this DbSet<Warning> set, ulong guildId, ulong userId, string mod) =>
        set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
            .ForEachAsync(x =>
            {
                if (x.Forgiven == 1) return;
                x.Forgiven = 1;
                x.ForgivenBy = mod;
            });

    public static async Task<IEnumerable<Warning>> GetForGuild(this DbSet<Warning> set, ulong id)
        => await set.AsQueryable().Where(x => x.GuildId == id).ToArrayAsyncEF();
}