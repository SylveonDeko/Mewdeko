using System.Collections.Generic;
using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories;

public interface ISelfAssignedRolesRepository : IRepository<SelfAssignedRole>
{
    bool DeleteByGuildAndRoleId(ulong guildId, ulong roleId);
    IEnumerable<SelfAssignedRole> GetFromGuild(ulong guildId);
}