using System;

namespace Mewdeko.Services.Database.Models
{
    public class Giveaways : DbEntity
    {
        public DateTime When { get; set; }
        public ulong ChannelId { get; set; }
        public ulong ServerId { get; set; }
        public int Ended { get; set; }
        public ulong MessageId { get; set; }
        public int Winners { get; set; }
        public ulong UserId { get; set; }
        public string Item { get; set; }
        public string RestrictTo { get; set; }
        public string BlacklistUsers { get; set; }
        public string BlacklistRoles { get; set; }
    }
}