namespace Mewdeko.Core.Services.Database.Models
{
    public class Suggestions : DbEntity
    {
        public ulong GuildId { get; set; }
        public ulong SuggestID { get; set;}
        public ulong MessageID { get; set; }
        public ulong UserID { get; set; }
    }
}
