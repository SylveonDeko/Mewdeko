using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A stretch of time a member spent in one voice channel with one voice state. A new segment starts whenever
///     the member moves, or mutes, deafens or undoes either, so per state totals can be filtered later.
/// </summary>
[Table("VoiceSegments")]
public class VoiceSegment
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public long Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    [Column("UserId")]
    public ulong UserId { get; set; }

    [Column("StartedAt")]
    public DateTime StartedAt { get; set; }

    [Column("EndedAt")]
    public DateTime EndedAt { get; set; }

    /// <summary>
    ///     The segment length in seconds.
    /// </summary>
    [Column("Seconds")]
    public int Seconds { get; set; }

    /// <summary>
    ///     The <see cref="Mewdeko.Modules.ServerStats.Common.VoiceStateFlags" /> in effect for the whole segment.
    /// </summary>
    [Column("State")]
    public int State { get; set; }
}