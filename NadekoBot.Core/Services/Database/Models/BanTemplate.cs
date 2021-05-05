namespace NadekoBot.Core.Services.Database.Models
{
    public class BanTemplate : DbEntity 
    {
        public ulong GuildId { get; set; }
        public string Text { get; set; }
    }
}