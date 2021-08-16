namespace Mewdeko.Core.Services.Database.Models
{
    public class Reputation : DbEntity
    {
        public ulong UserId { get; set; }
        public ulong ReviewerId { get; set; }
        public int ReviewType { get; set; }
        public string ReviewMessage { get; set; }
        public ulong GuildId { get; set; }
        public string ReviewerAv { get; set; }
        public string ReviewerUsername { get; set; }
    }
}