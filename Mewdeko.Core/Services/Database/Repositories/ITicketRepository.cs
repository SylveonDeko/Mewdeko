using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface ITicketRepository : IRepository<Tickets>
    {
        Tickets[] ForTicketId(ulong ticketNum, ulong guildId);
        Tickets[] ForGuildId(ulong guildId);
    }
}