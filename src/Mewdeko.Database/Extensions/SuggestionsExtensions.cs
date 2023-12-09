using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class SuggestionsExtensions
{
    public static Task<SuggestionsModel[]> ForId(this DbSet<SuggestionsModel> set, ulong guildId, ulong sugid)
        => set.AsQueryable().Where(x => x.GuildId == guildId && x.SuggestionId == sugid).ToArrayAsyncEF();

    public static Task<SuggestionsModel[]> ForUser(this DbSet<SuggestionsModel> set, ulong guildId, ulong userId)
        => set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == userId).ToArrayAsyncEF();
}