#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a repeating Twitch chat message timer for a guild.
/// </summary>
[Table("TwitchTimers")]
public class TwitchTimer
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord guild ID that owns this timer.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the timer name.
    /// </summary>
    [Column("Name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the newline-separated timer message rotation.
    /// </summary>
    [Column("Messages")]
    public string Messages { get; set; } = "";

    /// <summary>
    ///     Gets or sets the minimum minutes between sends.
    /// </summary>
    [Column("IntervalMinutes")]
    public int IntervalMinutes { get; set; }

    /// <summary>
    ///     Gets or sets the minimum chat messages required since the previous send.
    /// </summary>
    [Column("MinChatMessages")]
    public int MinChatMessages { get; set; }

    /// <summary>
    ///     Gets or sets whether this timer only sends while the Twitch channel is live.
    /// </summary>
    [Column("OnlineOnly")]
    public bool OnlineOnly { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether messages are selected randomly instead of rotating sequentially.
    /// </summary>
    [Column("RandomizeMessages")]
    public bool RandomizeMessages { get; set; }

    /// <summary>
    ///     Gets or sets whether this timer is enabled.
    /// </summary>
    [Column("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the last sent message index for sequential rotation.
    /// </summary>
    [Column("LastMessageIndex")]
    public int LastMessageIndex { get; set; }

    /// <summary>
    ///     Gets or sets the chat message count when this timer last sent.
    /// </summary>
    [Column("LastChatMessageCount")]
    public int LastChatMessageCount { get; set; }

    /// <summary>
    ///     Gets or sets when this timer last sent a message.
    /// </summary>
    [Column("LastSentAt")]
    public DateTime? LastSentAt { get; set; }

    /// <summary>
    ///     Gets or sets when the timer was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }

    /// <summary>
    ///     Gets or sets when the timer was last changed.
    /// </summary>
    [Column("LastUpdatedAt")]
    public DateTime? LastUpdatedAt { get; set; }
}