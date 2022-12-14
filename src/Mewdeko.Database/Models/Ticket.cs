namespace Mewdeko.Database.Models;

public class Ticket : DbEntity

{
    public ulong GuildId { get; set; }
    public ulong Creator { get; set; }
    public ulong ChannelId { get; set; }
    public string AddedUsers { get; set; } = "none";
    public string AddedRoles { get; set; } = "none";
    public ulong ClaimedBy { get; set; } = 0;
    public ulong ClosedBy { get; set; } = 0;
    public ulong TicketNumber { get; set; }
}