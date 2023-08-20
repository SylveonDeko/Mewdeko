using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

public class FilterInvitesChannelIds : DbEntity
{
    public ulong ChannelId { get; set; }

    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public override bool Equals(object obj) =>
        obj is FilterInvitesChannelIds f
        && f.ChannelId == ChannelId;

    public override int GetHashCode() => ChannelId.GetHashCode();
}

public class FilterWordsChannelIds : DbEntity
{
    public ulong ChannelId { get; set; }

    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public override bool Equals(object obj) =>
        obj is FilterWordsChannelIds f
        && f.ChannelId == ChannelId;

    public override int GetHashCode() => ChannelId.GetHashCode();
}