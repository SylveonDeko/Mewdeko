using NadekoBot.Core.Services.Database.Models;
using System.Collections.Generic;

namespace NadekoBot.Core.Services.Database.Repositories
{
    public interface ISelfAssignedRolesRepository : IRepository<SelfAssignedRole>
    {
        bool DeleteByGuildAndRoleId(ulong guildId, ulong roleId);
        IEnumerable<SelfAssignedRole> GetFromGuild(ulong guildId);
    }
}
