#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a recent Twitch EventSub or dashboard test event processed for a guild.
/// </summary>
[Table("TwitchEventHistory")]
public class TwitchEventHistory
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord guild ID that owns this event.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the normalized Twitch event type.
    /// </summary>
    [Column("EventType")]
    public string EventType { get; set; } = "";

    /// <summary>
    ///     Gets or sets the source that produced the event, such as EventSub or dashboard.
    /// </summary>
    [Column("Source")]
    public string Source { get; set; } = "";

    /// <summary>
    ///     Gets or sets whether processing completed successfully.
    /// </summary>
    [Column("Succeeded")]
    public bool Succeeded { get; set; }

    /// <summary>
    ///     Gets or sets a short human-readable event summary.
    /// </summary>
    [Column("Message")]
    public string Message { get; set; } = "";

    /// <summary>
    ///     Gets or sets the processing error, if any.
    /// </summary>
    [Column("Error")]
    public string? Error { get; set; }

    /// <summary>
    ///     Gets or sets the raw EventSub or dashboard payload for debugging.
    /// </summary>
    [Column("RawPayload")]
    public string? RawPayload { get; set; }

    /// <summary>
    ///     Gets or sets when the event was recorded.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}