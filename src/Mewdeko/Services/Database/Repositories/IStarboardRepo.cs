using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories;

public interface IStarboardRepository : IRepository<StarboardPosts>
{
    StarboardPosts ForMsgId(ulong msgid);
    StarboardPosts[] All();
}