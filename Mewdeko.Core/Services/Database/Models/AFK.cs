namespace Mewdeko.Core.Services.Database.Models
{
    public class AFK : DbEntity
    {
        public ulong UserId { get; set; }
        public ulong GuildId { get; set; }
        public string Message { get; set; }
        public int WasTimed { get; set; } = 0;
    }
}