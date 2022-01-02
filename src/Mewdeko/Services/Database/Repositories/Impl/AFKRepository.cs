using System.Collections.Generic;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class AFKRepository : Repository<AFK>, IAfkRepository
{
    public AFKRepository(DbContext context) : base(context)
    {
    }

    public List<AFK> ForId(ulong guildId, ulong uid)
    {
        var query = _set.AsQueryable().Where(x => x.GuildId == guildId && x.UserId == uid);

        return query.ToList();
    }

    public AFK[] ForGuild(ulong guildId)
    {
        var query = _set.AsQueryable().Where(x => x.GuildId == guildId);

        return query.ToArray();
    }
}