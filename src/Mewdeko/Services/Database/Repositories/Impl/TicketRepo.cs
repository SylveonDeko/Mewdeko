using System.Linq;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl
{
    public class TicketRepository : Repository<Ticket>, ITicketRepository
    {
        public TicketRepository(DbContext context) : base(context)
        {
        }

        public Ticket ForTicketId(ulong ticketNum, ulong guildId)
        {
            var query = _set.AsQueryable().Where(x => x.TicketNumber == ticketNum && x.GuildId == guildId);

            return query.FirstOrDefault();
        }

        public Ticket[] ForGuildId(ulong guildId)
        {
            var query = _set.AsQueryable().Where(x => x.GuildId == guildId);
            return query.ToArray();
        }
    }
}