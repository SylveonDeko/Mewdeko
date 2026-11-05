namespace Mewdeko.Controllers.Common.ServerBackups;

/// <summary>
///     A user's server backup, without its data.
/// </summary>
public class ServerBackupDto
{
    /// <summary>
    ///     The backup ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The ID of the server the backup came from.
    /// </summary>
    public ulong SourceGuildId { get; set; }

    /// <summary>
    ///     The name the server had when the backup was made.
    /// </summary>
    public string SourceGuildName { get; set; } = "";

    /// <summary>
    ///     The compressed size in bytes.
    /// </summary>
    public int SizeBytes { get; set; }

    /// <summary>
    ///     How many roles the backup holds.
    /// </summary>
    public int RoleCount { get; set; }

    /// <summary>
    ///     How many channels and categories the backup holds.
    /// </summary>
    public int ChannelCount { get; set; }

    /// <summary>
    ///     How many emojis the backup holds.
    /// </summary>
    public int EmojiCount { get; set; }

    /// <summary>
    ///     How many messages the backup holds.
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    ///     When the backup was made.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
///     A user's backups and the limits that apply to them.
/// </summary>
public class ServerBackupListResponse
{
    /// <summary>
    ///     The most backups a user can keep.
    /// </summary>
    public int MaxBackups { get; set; }

    /// <summary>
    ///     Minutes a user has to wait between creating backups.
    /// </summary>
    public int CooldownMinutes { get; set; }

    /// <summary>
    ///     The backups, newest first.
    /// </summary>
    public List<ServerBackupDto> Backups { get; set; } = [];
}
