using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A stretch of time a member showed one presence activity (a game, stream, Spotify and so on) in a guild.
/// </summary>
[Table("ActivitySegments")]
public class ActivitySegment
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public long Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     The Discord application behind the activity, when it carried one.
    /// </summary>
    [Column("ApplicationId")]
    public ulong? ApplicationId { get; set; }

    /// <summary>
    ///     The activity name as shown in the member's presence.
    /// </summary>
    [Column("Name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     The Discord <see cref="Discord.ActivityType" /> value.
    /// </summary>
    [Column("Type")]
    public int Type { get; set; }

    [Column("StartedAt")]
    public DateTime StartedAt { get; set; }

    [Column("EndedAt")]
    public DateTime EndedAt { get; set; }

    [Column("Seconds")]
    public int Seconds { get; set; }
}