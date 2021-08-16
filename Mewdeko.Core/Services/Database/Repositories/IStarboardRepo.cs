using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface IStarboardRepository : IRepository<Starboard>
    {
        Starboard[] ForMsgId(ulong msgid);
    }
}