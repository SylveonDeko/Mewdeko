using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A distinct error message seen within an hour, with how often it repeated.
/// </summary>
[Table("AnalyticsErrorSample")]
public class AnalyticsErrorSample
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public long Id { get; set; }

    /// <summary>
    ///     Start of the hour, UTC.
    /// </summary>
    [Column("Hour")]
    public DateTime Hour { get; set; }

    /// <summary>
    ///     The bot instance.
    /// </summary>
    [Column("Bot")]
    public string Bot { get; set; } = string.Empty;

    /// <summary>
    ///     The shard, when known.
    /// </summary>
    [Column("Shard")]
    public int? Shard { get; set; }

    /// <summary>
    ///     The exception type name.
    /// </summary>
    [Column("Type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    ///     The module or service that raised it.
    /// </summary>
    [Column("Module")]
    public string? Module { get; set; }

    /// <summary>
    ///     The method it was thrown from.
    /// </summary>
    [Column("Location")]
    public string? Location { get; set; }

    /// <summary>
    ///     Short hash of the message, used to group repeats.
    /// </summary>
    [Column("MessageHash")]
    public string MessageHash { get; set; } = string.Empty;

    /// <summary>
    ///     The message, trimmed to 300 characters.
    /// </summary>
    [Column("Message")]
    public string? Message { get; set; }

    /// <summary>
    ///     How often it repeated in the hour.
    /// </summary>
    [Column("Count")]
    public int Count { get; set; }

    /// <summary>
    ///     First occurrence in the hour.
    /// </summary>
    [Column("FirstSeen")]
    public DateTime FirstSeen { get; set; }

    /// <summary>
    ///     Latest occurrence in the hour.
    /// </summary>
    [Column("LastSeen")]
    public DateTime LastSeen { get; set; }

    /// <summary>
    ///     The guild of the latest occurrence, when known.
    /// </summary>
    [Column("LastGuildId")]
    public ulong? LastGuildId { get; set; }
}