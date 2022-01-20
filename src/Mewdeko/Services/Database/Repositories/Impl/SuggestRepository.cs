using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class SuggestRepository : Repository<Suggestionse>, ISuggestionsRepository
{
    public SuggestRepository(DbContext context) : base(context)
    {
    }
    

    public Suggestionse[] ForId(ulong guildId, ulong sugid)
    {
        var query = Set.AsQueryable().Where(x => x.GuildId == guildId && x.SuggestID == sugid);

        return query.ToArray();
    }

    public Suggestionse[] ForUser(ulong guildId, ulong userId) 
        => Set.AsQueryable().Where(x => x.GuildId == guildId && x.UserID == userId).ToArray();
}