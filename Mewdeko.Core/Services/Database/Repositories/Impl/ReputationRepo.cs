using System.Linq;
using Mewdeko.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Core.Services.Database.Repositories.Impl
{
    public class ReputationRepository : Repository<Reputation>, IReputationRepository
    {
        public ReputationRepository(DbContext context) : base(context)
        {
        }

        public Reputation[] ForUserId(ulong userid)
        {
            var query = _set.AsQueryable().Where(x => x.UserId == userid);

            return query.ToArray();
        }

        public Reputation[] ForGuildId(ulong gid)
        {
            var query = _set.AsQueryable().Where(x => x.GuildId == gid);

            return query.ToArray();
        }

        public Reputation[] ForReviewerId(ulong uid)
        {
            var query = _set.AsQueryable().Where(x => x.ReviewerId == uid);

            return query.ToArray();
        }
    }
}