using NadekoBot.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace NadekoBot.Core.Services.Database.Repositories.Impl
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

        /// <summary>
        /// Gets all global custom reactions and custom reactions only for the specified guild ids
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public IEnumerable<CustomReaction> GetFor(IEnumerable<ulong> ids)
        {
            return _set
                .AsNoTracking()
                .AsQueryable()
                .Where(x => ids.Contains(x.GuildId.Value))
                .ToArray();
        }

        public IEnumerable<CustomReaction> GetGlobal()
        {
            return _set
                .AsNoTracking()
                .AsQueryable()
                .Where(x => x.GuildId == null)
                .ToArray();
        }
    }
}
