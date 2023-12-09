using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class AfkExtensions
{
    public static Task<Afk[]> ForGuild(this DbSet<Afk> set, ulong guildId) =>
        set
            .AsQueryable()
            .AsNoTracking().Where(x => x.GuildId == guildId).ToArrayAsyncEF();

    public static Task<Afk[]> GetAll(this DbSet<Afk> set) =>
        set.AsQueryable().AsNoTracking().ToArrayAsyncEF();
}