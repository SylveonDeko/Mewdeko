using Mewdeko.Services.Database.Models;
using System.Collections.Generic;

namespace Mewdeko.Services.Database.Repositories
{
    public interface IAfkRepository : IRepository<AFK>
    {
        List<AFK> ForId(ulong guildid, ulong uid);
        AFK[] ForGuild(ulong guildid);
    }
}