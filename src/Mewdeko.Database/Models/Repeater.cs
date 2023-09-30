using System.ComponentModel.DataAnnotations.Schema;

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

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