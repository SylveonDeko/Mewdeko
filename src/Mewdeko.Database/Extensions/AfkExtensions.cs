using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class AfkExtensions
{
    public static async Task<Afk[]> ForGuild(this DbSet<Afk> set, ulong guildId) =>
        await set
            .AsQueryable()
            .AsNoTracking().Where(x => x.GuildId == guildId).ToArrayAsyncEF();

    public static async Task<Afk[]> GetAll(this DbSet<Afk> set) =>
        await set.AsQueryable().AsNoTracking().ToArrayAsyncEF();
}