using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories
{
    public interface ITicketRepository : IRepository<Tickets>
    {
        Tickets[] ForTicketId(ulong ticketNum, ulong guildId);
        Tickets[] ForGuildId(ulong guildId);
    }
}