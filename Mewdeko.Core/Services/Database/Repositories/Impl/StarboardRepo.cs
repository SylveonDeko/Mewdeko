using System.Linq;
using Mewdeko.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Core.Services.Database.Repositories.Impl
{
    public class StarboardRepository : Repository<Starboard>, IStarboardRepository
    {
        public StarboardRepository(DbContext context) : base(context)
        {
        }

        public Starboard[] ForMsgId(ulong msgid)
        {
            var query = _set.AsQueryable().Where(x => x.MessageId == msgid);

            return query.ToArray();
        }
    }
}