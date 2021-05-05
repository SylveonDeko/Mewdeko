using System;

namespace NadekoBot.Core.Services.Database.Models
{
    public class FeedSub : DbEntity
    {
        public int GuildConfigId { get; set; }
        public GuildConfig GuildConfig { get; set; }

        public ulong ChannelId { get; set; }
        public string Url { get; set; }

        public override int GetHashCode()
        {
            return Url.GetHashCode(StringComparison.InvariantCulture) ^ GuildConfigId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is FeedSub s
                && s.Url.ToLower() == Url.ToLower()
                && s.GuildConfigId == GuildConfigId;
        }
    }
}
