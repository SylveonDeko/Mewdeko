namespace Mewdeko.Core.Services.Database.Models
{
    public class SwitchShops : DbEntity
    {
        public ulong GuildId { get; set; }
        public ulong Owner { get; set; }
        public ulong ShopName { get; set; }
        public string Status { get; set; }
        public string Announcement { get; set; }
        public string InviteLink { get; set; }
        public string ExtraOwners { get; set; }
    }
}