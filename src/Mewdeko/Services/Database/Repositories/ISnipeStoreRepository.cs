using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories
{
    public interface ISnipeStoreRepository : IRepository<SnipeStore>
    {
        SnipeStore[] ForChannel(ulong guildid, ulong chanid);
    }
}