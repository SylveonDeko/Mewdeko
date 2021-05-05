namespace NadekoBot.Core.Services.Database.Models
{
    public class PlantedCurrency : DbEntity
    {
        public long Amount { get; set; }
        public string Password { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong UserId { get; set; }
        public ulong MessageId { get; set; }
    }
}
