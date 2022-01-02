namespace Mewdeko.Services.Database.Models;

public class FilterLinksChannelId : DbEntity
{
    public ulong ChannelId { get; set; }

    public override bool Equals(object obj) =>
        obj is FilterLinksChannelId f
            ? f.ChannelId == ChannelId
            : false;

    public override int GetHashCode() => ChannelId.GetHashCode();
}