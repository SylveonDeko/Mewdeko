using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

public class DelMsgOnCmdChannel : DbEntity
{
    public ulong ChannelId { get; set; }
    public bool State { get; set; } = true;

    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public override int GetHashCode() => ChannelId.GetHashCode();

    public override bool Equals(object obj) =>
        obj is DelMsgOnCmdChannel x
        && x.ChannelId == ChannelId;
}