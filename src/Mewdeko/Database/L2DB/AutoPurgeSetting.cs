#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Which messages an auto-purge deletes.
/// </summary>
public enum AutoPurgeFilter
{
    /// <summary>
    ///     Every message.
    /// </summary>
    All = 0,

    /// <summary>
    ///     Messages from people.
    /// </summary>
    Humans = 1,

    /// <summary>
    ///     Messages from bots.
    /// </summary>
    Bots = 2,

    /// <summary>
    ///     Messages that contain a link.
    /// </summary>
    Links = 3,

    /// <summary>
    ///     Messages that have an attachment.
    /// </summary>
    Attachments = 4
}

/// <summary>
///     Represents a channel that has its old messages deleted on a schedule.
/// </summary>
[Table("AutoPurgeSettings")]
public class AutoPurgeSetting
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the channel ID.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets how old a message must be, in minutes, before it is deleted.
    /// </summary>
    [Column("MaxAgeMinutes")]
    public int MaxAgeMinutes { get; set; } = 1440;

    /// <summary>
    ///     Gets or sets how often the channel is purged, in minutes.
    /// </summary>
    [Column("IntervalMinutes")]
    public int IntervalMinutes { get; set; } = 10;

    /// <summary>
    ///     Gets or sets which messages are deleted.
    /// </summary>
    [Column("Filter")]
    public AutoPurgeFilter Filter { get; set; }

    /// <summary>
    ///     Gets or sets whether pinned messages are kept.
    /// </summary>
    [Column("KeepPinned")]
    public bool KeepPinned { get; set; } = true;

    /// <summary>
    ///     Gets or sets when the channel was last purged.
    /// </summary>
    [Column("LastRunAt")]
    public DateTime? LastRunAt { get; set; }

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
