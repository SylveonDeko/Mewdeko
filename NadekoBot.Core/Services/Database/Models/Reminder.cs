using System;

namespace NadekoBot.Core.Services.Database.Models
{
    public class Reminder : DbEntity
    {
        public DateTime When { get; set; }
        public ulong ChannelId { get; set; }
        public ulong ServerId { get; set; }
        public ulong UserId { get; set; }
        public string Message { get; set; }
        public bool IsPrivate { get; set; }
    }
}
