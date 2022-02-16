namespace Mewdeko.Database.Models;

public class GCChannelId : DbEntity
{
    public GuildConfig GuildConfig { get; set; }
    public ulong ChannelId { get; set; }

    public override bool Equals(object obj) => obj is GCChannelId gc && gc.ChannelId == ChannelId;

    public override int GetHashCode() => ChannelId.GetHashCode();
}