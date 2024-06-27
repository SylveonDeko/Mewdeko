using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class HighlightExtensions
{
    public async static Task<List<Highlights>> ForUser(this DbSet<Highlights> set, ulong guildId, ulong userId)
        => await set.AsQueryable().Where(x => x.UserId == userId && x.GuildId == guildId).ToListAsyncEF();

    public async static Task<List<HighlightSettings>> ForUser(this DbSet<HighlightSettings> set, ulong guildId, ulong userId)
        => await set.AsQueryable().Where(x => x.UserId == userId && x.GuildId == guildId).ToListAsyncEF();

    public async static Task<List<HighlightSettings>> AllHighlightSettings(this DbSet<HighlightSettings> set)
        => await set.AsQueryable().ToListAsyncEF();

    public async static Task<List<Highlights>> AllHighlights(this DbSet<Highlights> set)
        => await set.AsQueryable().ToListAsyncEF();
}