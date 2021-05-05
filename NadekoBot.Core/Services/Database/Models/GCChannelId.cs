namespace NadekoBot.Core.Services.Database.Models
{
    public class GCChannelId : DbEntity
    {
        public GuildConfig GuildConfig { get; set; }
        public ulong ChannelId { get; set; }

        public override bool Equals(object obj)
        {
            return obj is GCChannelId gc
                ? gc.ChannelId == ChannelId
                : false;
        }

        public override int GetHashCode() =>
            this.ChannelId.GetHashCode();
    }
}
