using Mewdeko.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace Mewdeko.Core.Services.Database.Repositories.Impl
{
    public class CustomReactionsRepository : Repository<CustomReaction>, ICustomReactionRepository
    {
        public CustomReactionsRepository(DbContext context) : base(context)
        {
        }

        public int ClearFromGuild(ulong id)
        {
            return _context.Database.ExecuteSqlInterpolated($"DELETE FROM CustomReactions WHERE GuildId={id};");
        }

        public IEnumerable<CustomReaction> ForId(ulong id)
        {
            return _set
                .AsNoTracking()
                .AsQueryable()
                .Where(x => x.GuildId == id)
                .ToArray();
        }

        public CustomReaction GetByGuildIdAndInput(ulong? guildId, string input)
        {
            return _set.FirstOrDefault(x => x.GuildId == guildId && x.Trigger.ToUpper() == input);
        }
    }
}
