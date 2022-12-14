using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class SelfAssignedRolesExtensions
{
    public static async Task<bool> DeleteByGuildAndRoleId(this DbSet<SelfAssignedRole> set, ulong guildId, ulong roleId)
    {
        var role = await set.FirstOrDefaultAsyncEF(s => s.GuildId == guildId && s.RoleId == roleId);

        if (role == null)
            return false;

        set.Remove(role);
        return true;
    }

    public static async Task<IEnumerable<SelfAssignedRole>> GetFromGuild(this DbSet<SelfAssignedRole> set, ulong guildId) =>
        await set.AsQueryable()
            .Where(s => s.GuildId == guildId)
            .ToArrayAsyncEF();
}