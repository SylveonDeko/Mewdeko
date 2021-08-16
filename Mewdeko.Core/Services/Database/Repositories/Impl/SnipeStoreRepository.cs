using System.Linq;
using Mewdeko.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Core.Services.Database.Repositories.Impl
{
    public class SnipeStoreRepository : Repository<SnipeStore>, ISnipeStoreRepository
    {
        public SnipeStoreRepository(DbContext context) : base(context)
        {
        }

        public SnipeStore[] ForChannel(ulong guildId, ulong chanid)
        {
            var query = _set.AsQueryable().Where(x => x.GuildId == guildId && x.ChannelId == chanid);

            return query.ToArray();
        }
    }
}