namespace Mewdeko.Services.Database.Models
{
    public class Suggestionse : DbEntity
    {
        public ulong GuildId { get; set; }
        public ulong SuggestID { get; set; }
        public string Suggestion { get; set; }
        public ulong MessageID { get; set; }
        public ulong UserID { get; set; }
    }
}