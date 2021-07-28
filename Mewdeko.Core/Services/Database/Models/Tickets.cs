namespace Mewdeko.Core.Services.Database.Models
{
    public class Tickets : DbEntity

    {
        public ulong GuildId { get; set; }
        public ulong Creator { get; set; }
        public ulong ChannelId { get; set; }
        public ulong ClaimedBy { get; set; }
        public ulong ClosedBy { get; set; }
        public ulong TicketNumber { get; set; }
    }
}