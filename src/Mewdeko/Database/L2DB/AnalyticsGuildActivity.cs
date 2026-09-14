using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Hourly count of one gateway event type in a guild.
/// </summary>
[Table("AnalyticsGuildActivity")]
public class AnalyticsGuildActivity
{
    /// <summary>
    ///     The guild.
    /// </summary>
    [Column("GuildId", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The gateway event name.
    /// </summary>
    [Column("EventType", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    ///     Start of the hour, UTC.
    /// </summary>
    [Column("Hour", IsPrimaryKey = true, PrimaryKeyOrder = 2)]
    public DateTime Hour { get; set; }

    /// <summary>
    ///     The bot instance.
    /// </summary>
    [Column("Bot")]
    public string Bot { get; set; } = string.Empty;

    /// <summary>
    ///     Events in the hour.
    /// </summary>
    [Column("Count")]
    public int Count { get; set; }
}