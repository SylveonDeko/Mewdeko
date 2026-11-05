#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a compressed snapshot of a server that its owner can restore.
/// </summary>
[Table("ServerBackups")]
public class ServerBackup
{
    /// <summary>
    ///     Gets or sets the database row identifier, which is also the backup ID users see.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the user who owns the backup.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the server the backup was taken from.
    /// </summary>
    [Column("SourceGuildId")]
    public ulong SourceGuildId { get; set; }

    /// <summary>
    ///     Gets or sets the name the server had when the backup was taken.
    /// </summary>
    [Column("SourceGuildName")]
    public string SourceGuildName { get; set; } = "";

    /// <summary>
    ///     Gets or sets the gzip compressed JSON of the backup.
    /// </summary>
    [Column("Data")]
    public byte[] Data { get; set; } = [];

    /// <summary>
    ///     Gets or sets the compressed size in bytes.
    /// </summary>
    [Column("SizeBytes")]
    public int SizeBytes { get; set; }

    /// <summary>
    ///     Gets or sets how many roles the backup holds.
    /// </summary>
    [Column("RoleCount")]
    public int RoleCount { get; set; }

    /// <summary>
    ///     Gets or sets how many channels and categories the backup holds.
    /// </summary>
    [Column("ChannelCount")]
    public int ChannelCount { get; set; }

    /// <summary>
    ///     Gets or sets how many emojis the backup holds.
    /// </summary>
    [Column("EmojiCount")]
    public int EmojiCount { get; set; }

    /// <summary>
    ///     Gets or sets how many messages the backup holds.
    /// </summary>
    [Column("MessageCount")]
    public int MessageCount { get; set; }

    /// <summary>
    ///     Gets or sets when the backup was made.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
