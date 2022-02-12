using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class SuggestionsExtensions
{
    public static Suggestionse[] ForId(this DbSet<Suggestionse> set, ulong guildId, ulong sugid) => set.AsQueryable().Where(x => x.GuildId == guildId && x.SuggestID == sugid).ToArray();

    public static Suggestionse[] ForUser(this DbSet<Suggestionse> set, ulong guildId, ulong userId) 
        => set.AsQueryable().Where(x => x.GuildId == guildId && x.UserID == userId).ToArray();
}