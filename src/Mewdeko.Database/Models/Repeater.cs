using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

[Table("GuildRepeater")]
public class Repeater : DbEntity
{
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong? LastMessageId { get; set; }
    public string Message { get; set; }
    public string Interval { get; set; }
    public string? StartTimeOfDay { get; set; }
    public long NoRedundant { get; set; }
}