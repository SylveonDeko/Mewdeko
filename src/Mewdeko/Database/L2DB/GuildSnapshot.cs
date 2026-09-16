using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     An hourly sample of a guild's member and presence counts, used for member growth and status charts.
/// </summary>
[Table("GuildSnapshots")]
public class GuildSnapshot
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public long Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("Timestamp")]
    public DateTime Timestamp { get; set; }

    [Column("Members")]
    public int Members { get; set; }

    [Column("Humans")]
    public int Humans { get; set; }

    [Column("Bots")]
    public int Bots { get; set; }

    [Column("Online")]
    public int Online { get; set; }

    [Column("Idle")]
    public int Idle { get; set; }

    [Column("Dnd")]
    public int Dnd { get; set; }

    [Column("Offline")]
    public int Offline { get; set; }

    [Column("InVoice")]
    public int InVoice { get; set; }
}