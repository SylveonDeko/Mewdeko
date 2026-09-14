using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     One security relevant gateway event in a guild.
/// </summary>
[Table("AnalyticsGuildEventLog")]
public class AnalyticsGuildEventLog
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public long Id { get; set; }

    /// <summary>
    ///     When it happened, UTC.
    /// </summary>
    [Column("At")]
    public DateTime At { get; set; }

    /// <summary>
    ///     The guild.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     The gateway event name.
    /// </summary>
    [Column("EventType")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    ///     The bot instance.
    /// </summary>
    [Column("Bot")]
    public string Bot { get; set; } = string.Empty;

    /// <summary>
    ///     The shard.
    /// </summary>
    [Column("Shard")]
    public int? Shard { get; set; }
}