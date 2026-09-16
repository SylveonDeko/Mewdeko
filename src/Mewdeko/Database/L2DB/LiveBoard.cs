using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A message the bot keeps refreshing with a leaderboard, chart or overview, optionally pinned.
/// </summary>
[Table("LiveBoards")]
public class LiveBoard
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The message being refreshed, or 0 until it has been sent.
    /// </summary>
    [Column("MessageId")]
    public ulong MessageId { get; set; }

    /// <summary>
    ///     The <see cref="Mewdeko.Modules.ServerStats.Common.LiveBoardKind" /> shown.
    /// </summary>
    [Column("Kind")]
    public int Kind { get; set; }

    /// <summary>
    ///     The <see cref="Mewdeko.Modules.Utility.Common.StatsRange" /> the board covers.
    /// </summary>
    [Column("Range")]
    public int Range { get; set; }

    /// <summary>
    ///     Whether the message is pinned when sent.
    /// </summary>
    [Column("Pin")]
    public bool Pin { get; set; } = true;

    /// <summary>
    ///     How many rows a leaderboard shows.
    /// </summary>
    [Column("Entries")]
    public int Entries { get; set; } = 10;

    /// <summary>
    ///     How often the message is refreshed, in minutes.
    /// </summary>
    [Column("IntervalMinutes")]
    public int IntervalMinutes { get; set; } = 15;

    [Column("LastUpdateAt")]
    public DateTime? LastUpdateAt { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}