using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
///     Provides extension methods for querying and manipulating SelfAssignedRole entities.
/// </summary>
public static class SelfAssignedRolesExtensions
{
    /// <summary>
    ///     Deletes a SelfAssignedRole entity for a specific guild and role.
    /// </summary>
    /// <param name="set">The DbSet of SelfAssignedRole entities to query.</param>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="roleId">The ID of the role.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result is true if a role was deleted, false
    ///     otherwise.
    /// </returns>
    public static async Task<bool> DeleteByGuildAndRoleId(this DbSet<SelfAssignedRole> set, ulong guildId, ulong roleId)
    {
        var role = await set.FirstOrDefaultAsyncEF(s => s.GuildId == guildId && s.RoleId == roleId);

        if (role == null)
            return false;

        set.Remove(role);
        return true;
    }

    /// <summary>
    ///     Retrieves all SelfAssignedRole entities for a specific guild.
    /// </summary>
    /// <param name="set">The DbSet of SelfAssignedRole entities to query.</param>
    /// <param name="guildId">The ID of the guild to filter by.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains an IEnumerable of SelfAssignedRole
    ///     entities for the specified guild.
    /// </returns>
    public static async Task<IEnumerable<SelfAssignedRole>> GetFromGuild(this DbSet<SelfAssignedRole> set,
        ulong guildId)
    {
        return await set.AsQueryable()
            .Where(s => s.GuildId == guildId)
            .ToArrayAsyncEF();
    }
}