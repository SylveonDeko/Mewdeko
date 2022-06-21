using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class RoleGreetExtensions
{
    public static RoleGreet[] ForRoleId(this DbSet<RoleGreet> set, ulong roleId)
        => set.AsQueryable().Where(x => x.RoleId == roleId).ToArray();
}