namespace Mewdeko.Database.Models;

public class FilterChannelId : DbEntity
{
    public ulong ChannelId { get; set; }

    public override bool Equals(object obj) =>
        obj is FilterChannelId f
        && f.ChannelId == ChannelId;

    public override int GetHashCode() => ChannelId.GetHashCode();
}