using Mewdeko.Services.Database.Models;

namespace Mewdeko.Modules.Tickets.Services;

public class TicketService : INService
{
    private readonly DbService _db;

    public TicketService(DbService db) => _db = db;

    public Ticket GetTicket(ulong guildId, ulong ticketId) => _db.GetDbContext().Tickets.ForTicketId(ticketId, guildId);

    public Ticket[] GetAllTickets(ulong guildId) => _db.GetDbContext().Tickets.ForGuildId(guildId);
}