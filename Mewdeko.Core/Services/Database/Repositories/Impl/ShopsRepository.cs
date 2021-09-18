using System.Linq;
using Mewdeko.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Core.Services.Database.Repositories.Impl
{
    public class SwitchShopsRepository : Repository<SwitchShops>, ISwitchShopsRepository
    {
        public SwitchShopsRepository(DbContext context) : base(context)
        {
        }

        public SwitchShops[] ForGuild(ulong id)
        {
            var query = _set.AsQueryable().Where(x => x.GuildId == id);

            return query.ToArray();
        }

        public SwitchShops[] GetAll(ulong e = 0)
        {
            var query = _set.AsQueryable();
            return query.ToArray();
        }
    }
}