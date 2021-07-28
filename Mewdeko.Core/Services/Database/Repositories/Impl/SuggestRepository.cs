using System.Linq;
using Mewdeko.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Core.Services.Database.Repositories.Impl
{
    public class SuggestRepository : Repository<Suggestionse>, ISuggestionsRepository
    {
        public SuggestRepository(DbContext context) : base(context)
        {
        }

        public Suggestionse[] ForSuggest(ulong guildId, ulong sid, ulong MessageID)
        {
            var query = _set.AsQueryable()
                .Where(x => x.GuildId == guildId && x.SuggestID == sid && MessageID == x.MessageID);

            return query.ToArray();
        }

        public Suggestionse[] ForId(ulong guildId, ulong sugid)
        {
            var query = _set.AsQueryable().Where(x => x.GuildId == guildId && x.SuggestID == sugid);

            return query.ToArray();
        }
    }
}