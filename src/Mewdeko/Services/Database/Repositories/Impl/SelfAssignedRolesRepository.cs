using System.Collections.Generic;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class SelfAssignedRolesRepository : Repository<SelfAssignedRole>, ISelfAssignedRolesRepository
{
    public SelfAssignedRolesRepository(DbContext context) : base(context)
    {
    }

    public bool DeleteByGuildAndRoleId(ulong guildId, ulong roleId)
    {
        var role = Set.FirstOrDefault(s => s.GuildId == guildId && s.RoleId == roleId);

        if (role == null)
            return false;

        Set.Remove(role);
        return true;
    }

    public IEnumerable<SelfAssignedRole> GetFromGuild(ulong guildId) =>
        Set.AsQueryable()
            .Where(s => s.GuildId == guildId)
            .ToArray();
}