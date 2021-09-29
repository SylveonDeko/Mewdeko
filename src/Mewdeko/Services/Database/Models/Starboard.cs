namespace Mewdeko.Services.Database.Models
{
    public class Starboard : DbEntity
    {
        public ulong MessageId { get; set; }
        public ulong PostId { get; set; }
    }
}