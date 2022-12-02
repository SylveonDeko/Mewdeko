using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class RoleGreetExtensions
{
    public static async Task<RoleGreet[]> ForRoleId(this DbSet<RoleGreet> set, ulong roleId)
        => await set.AsQueryable().Where(x => x.RoleId == roleId).ToArrayAsyncEF();
}