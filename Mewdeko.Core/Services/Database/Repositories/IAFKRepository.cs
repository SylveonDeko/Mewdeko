using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface IAFKRepository : IRepository<AFK>
    {
        AFK[] ForId(ulong guildid, ulong uid);
    }
}