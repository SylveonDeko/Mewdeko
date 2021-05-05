using Mewdeko.Core.Services.Database.Models;
using System.Collections.Generic;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface IStarboardRepository : IRepository<Starboard>
    {
        Starboard[] ForMsgId(ulong msgid);
    }
}
