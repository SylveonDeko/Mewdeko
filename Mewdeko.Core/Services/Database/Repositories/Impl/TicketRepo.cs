using System.Linq;
using Mewdeko.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Core.Services.Database.Repositories.Impl
{
    public class TicketRepository : Repository<Tickets>, ITicketRepository
    {
        public TicketRepository(DbContext context) : base(context)
        {
        }

        public Tickets[] ForTicketId(ulong ticketNum, ulong guildId)
        {
            var query = _set.AsQueryable().Where(x => x.TicketNumber == ticketNum && x.GuildId == guildId);

            return query.ToArray();
        }

        public Tickets[] ForGuildId(ulong guildId)
        {
            var query = _set.AsQueryable().Where(x => x.GuildId == guildId);
            return query.ToArray();
        }
    }
}