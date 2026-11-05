#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a live stream message that is edited in place while the stream runs.
/// </summary>
[Table("TrackedStreamMessages")]
public class TrackedStreamMessage
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the followed stream this message belongs to.
    /// </summary>
    [Column("FollowedStreamId")]
    public int FollowedStreamId { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID the message was sent in.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID the message was sent in.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the message ID being edited.
    /// </summary>
    [Column("MessageId")]
    public ulong MessageId { get; set; }

    /// <summary>
    ///     Gets or sets the stream key, as the platform and lowercase username.
    /// </summary>
    [Column("StreamKey")]
    public string StreamKey { get; set; } = "";

    /// <summary>
    ///     Gets or sets when the stream went live.
    /// </summary>
    [Column("StartedAt")]
    public DateTime StartedAt { get; set; }

    /// <summary>
    ///     Gets or sets when the message was last edited.
    /// </summary>
    [Column("LastEdit")]
    public DateTime LastEdit { get; set; }
}
