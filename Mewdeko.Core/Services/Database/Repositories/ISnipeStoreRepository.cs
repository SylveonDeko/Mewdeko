using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface ISnipeStoreRepository : IRepository<SnipeStore>
    {
        SnipeStore[] ForChannel(ulong guildid, ulong chanid);
    }
}