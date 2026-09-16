using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     All time voice seconds per member per channel. Segments age out after 90 days, so this is what all time
///     leaderboards and lifetime stat roles read.
/// </summary>
[Table("VoiceTotals")]
public class VoiceTotal
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    [Column("UserId")]
    public ulong UserId { get; set; }

    [Column("Seconds")]
    public long Seconds { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}