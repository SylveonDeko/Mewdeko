using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories;

public interface ITicketRepository : IRepository<Ticket>
{
    Ticket ForTicketId(ulong ticketNum, ulong guildId);
    Ticket[] ForGuildId(ulong guildId);
}