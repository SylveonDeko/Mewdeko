using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories
{
    public interface IAFKRepository : IRepository<AFK>
    {
        AFK[] ForId(ulong guildid, ulong uid);
    }
}