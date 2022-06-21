using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class SelfAssignedRolesExtensions
{
    public static bool DeleteByGuildAndRoleId(this DbSet<SelfAssignedRole> set, ulong guildId, ulong roleId)
    {
        var role = set.FirstOrDefault(s => s.GuildId == guildId && s.RoleId == roleId);

        if (role == null)
            return false;

        set.Remove(role);
        return true;
    }

    public static IEnumerable<SelfAssignedRole> GetFromGuild(this DbSet<SelfAssignedRole> set, ulong guildId) =>
        set.AsQueryable()
           .Where(s => s.GuildId == guildId)
           .ToArray();
}