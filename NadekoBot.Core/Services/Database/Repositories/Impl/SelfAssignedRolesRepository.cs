using NadekoBot.Core.Services.Database.Models;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace NadekoBot.Core.Services.Database.Repositories.Impl
{
    public class SelfAssignedRolesRepository : Repository<SelfAssignedRole>, ISelfAssignedRolesRepository
    {
        public SelfAssignedRolesRepository(DbContext context) : base(context)
        {
        }

        public bool DeleteByGuildAndRoleId(ulong guildId, ulong roleId)
        {
            var role = _set.FirstOrDefault(s => s.GuildId == guildId && s.RoleId == roleId);

            if (role == null)
                return false;

            _set.Remove(role);
            return true;
        }

        public IEnumerable<SelfAssignedRole> GetFromGuild(ulong guildId) 
            =>  _set.AsQueryable()
                    .Where(s => s.GuildId == guildId)
                    .ToArray();
    }
}
