using Mewdeko.Core.Services.Database.Models;
using System.Collections.Generic;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface IAFKRepository : IRepository<AFK>
    {
        AFK[] ForId(ulong guildid, ulong uid);
    }
}
