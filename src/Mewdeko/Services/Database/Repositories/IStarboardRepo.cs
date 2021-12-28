using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories;

public interface IStarboardRepository : IRepository<Starboard>
{
    Starboard[] ForMsgId(ulong msgid);
}