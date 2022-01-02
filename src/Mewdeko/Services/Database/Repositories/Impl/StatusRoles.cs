using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class StatusRolesRepository : Repository<StatusRoles>, IStatusRolesRepository
{
    public StatusRolesRepository(DbContext context) : base(context)
    {
    }

    public StatusRoles[] ForGuild(ulong guildId)
    {
        var query = _set.AsQueryable().Where(x => x.GuildId == guildId);

        return query.ToArray();
    }
}