using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

[Table("GuildRepeater")]
public class Repeater : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong? LastMessageId { get; set; }
    public string Message { get; set; }
    public TimeSpan Interval { get; set; }
    public TimeSpan? StartTimeOfDay { get; set; }
    public bool NoRedundant { get; set; }
}