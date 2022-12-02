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

        if (warn == null || warn.Forgiven)
            return false;

        warn.Forgiven = true;
        warn.ForgivenBy = mod;
        return true;
    }

    public static async Task ForgiveAll(this DbSet<Warning> set, ulong guildId, ulong userId, string mod) =>
        await set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId)
            .ForEachAsync(x =>
            {
                if (x.Forgiven) return;
                x.Forgiven = true;
                x.ForgivenBy = mod;
            }).ConfigureAwait(false);

    public static async Task<IEnumerable<Warning>> GetForGuild(this DbSet<Warning> set, ulong id)
        => await set.AsQueryable().Where(x => x.GuildId == id).ToArrayAsyncEF();
}