namespace Mewdeko.Database.Models;

public class FilterLinksChannelId : DbEntity
{
    public ulong ChannelId { get; set; }

    public override bool Equals(object obj) =>
        obj is FilterLinksChannelId f
        && f.ChannelId == ChannelId;

    public override int GetHashCode() => ChannelId.GetHashCode();
}