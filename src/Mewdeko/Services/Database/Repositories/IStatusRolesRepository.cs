using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories;

public interface IStatusRolesRepository : IRepository<StatusRoles>
{
    StatusRoles[] ForGuild(ulong id);
}