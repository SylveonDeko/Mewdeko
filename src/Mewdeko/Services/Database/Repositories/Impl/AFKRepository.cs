using System.Collections.Generic;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class AfkRepository : Repository<AFK>, IAfkRepository
{
    public AfkRepository(DbContext context) : base(context)
    {
    }

    public List<AFK> ForId(ulong guildId, ulong uid)
    {
        var query = Set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == uid);

        return query.ToList();
    }

    public AFK[] ForGuild(ulong guildId)
    {
        var query = Set.AsQueryable().Where(x => x.GuildId == guildId);

        return query.ToArray();
    }
}