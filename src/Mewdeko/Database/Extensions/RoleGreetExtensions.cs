using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
/// Provides extension methods for querying RoleGreet entities.
/// </summary>
public static class RoleGreetExtensions
{
    /// <summary>
    /// Retrieves all RoleGreet entities for a specific role.
    /// </summary>
    /// <param name="set">The DbSet of RoleGreet entities to query.</param>
    /// <param name="roleId">The ID of the role to filter by.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an array of RoleGreet entities for the specified role.</returns>
    public static Task<RoleGreet[]> ForRoleId(this DbSet<RoleGreet> set, ulong roleId)
        => set.AsQueryable().Where(x => x.RoleId == roleId).ToArrayAsyncEF();
}