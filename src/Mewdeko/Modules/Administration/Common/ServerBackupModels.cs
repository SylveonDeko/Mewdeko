namespace Mewdeko.Modules.Administration.Common;

/// <summary>
///     The contents of a server backup, stored as compressed JSON.
/// </summary>
public class ServerBackupData
{
    /// <summary>
    ///     Gets or sets the format version.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the server settings.
    /// </summary>
    public BackupSettings Settings { get; set; } = new();

    /// <summary>
    ///     Gets or sets the roles, including @everyone.
    /// </summary>
    public List<BackupRole> Roles { get; set; } = [];

    /// <summary>
    ///     Gets or sets the categories and channels.
    /// </summary>
    public List<BackupChannel> Channels { get; set; } = [];

    /// <summary>
    ///     Gets or sets the emojis.
    /// </summary>
    public List<BackupEmoji> Emojis { get; set; } = [];

    /// <summary>
    ///     Gets or sets the saved messages.
    /// </summary>
    public List<BackupChannelMessages> Messages { get; set; } = [];
}

/// <summary>
///     The server settings held in a backup.
/// </summary>
public class BackupSettings
{
    /// <summary>
    ///     Gets or sets the server name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the server icon, if it has one.
    /// </summary>
    public byte[]? Icon { get; set; }

    /// <summary>
    ///     Gets or sets the verification level.
    /// </summary>
    public int VerificationLevel { get; set; }

    /// <summary>
    ///     Gets or sets the default message notification level.
    /// </summary>
    public int DefaultMessageNotifications { get; set; }

    /// <summary>
    ///     Gets or sets the explicit content filter.
    /// </summary>
    public int ExplicitContentFilter { get; set; }

    /// <summary>
    ///     Gets or sets the AFK timeout in seconds.
    /// </summary>
    public int AfkTimeout { get; set; }

    /// <summary>
    ///     Gets or sets the backed-up ID of the AFK channel.
    /// </summary>
    public ulong? AfkChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the backed-up ID of the system channel.
    /// </summary>
    public ulong? SystemChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the system channel flags.
    /// </summary>
    public int SystemChannelFlags { get; set; }
}

/// <summary>
///     A role held in a backup.
/// </summary>
public class BackupRole
{
    /// <summary>
    ///     Gets or sets the ID the role had in the original server.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     Gets or sets whether this is the @everyone role.
    /// </summary>
    public bool IsEveryone { get; set; }

    /// <summary>
    ///     Gets or sets the role name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the raw colour value.
    /// </summary>
    public uint Color { get; set; }

    /// <summary>
    ///     Gets or sets the raw permission value.
    /// </summary>
    public ulong Permissions { get; set; }

    /// <summary>
    ///     Gets or sets whether the role is shown separately in the member list.
    /// </summary>
    public bool Hoist { get; set; }

    /// <summary>
    ///     Gets or sets whether the role can be mentioned.
    /// </summary>
    public bool Mentionable { get; set; }

    /// <summary>
    ///     Gets or sets the role position.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    ///     Gets or sets the role icon image, if it has one.
    /// </summary>
    public byte[]? Icon { get; set; }

    /// <summary>
    ///     Gets or sets the role's unicode emoji, if it has one.
    /// </summary>
    public string? UnicodeEmoji { get; set; }
}

/// <summary>
///     A permission overwrite held in a backup.
/// </summary>
public class BackupOverwrite
{
    /// <summary>
    ///     Gets or sets whether the target is a role rather than a member.
    /// </summary>
    public bool IsRole { get; set; }

    /// <summary>
    ///     Gets or sets the backed-up role ID or the member ID.
    /// </summary>
    public ulong TargetId { get; set; }

    /// <summary>
    ///     Gets or sets the raw allowed permissions.
    /// </summary>
    public ulong Allow { get; set; }

    /// <summary>
    ///     Gets or sets the raw denied permissions.
    /// </summary>
    public ulong Deny { get; set; }
}

/// <summary>
///     A category or channel held in a backup.
/// </summary>
public class BackupChannel
{
    /// <summary>
    ///     Gets or sets the ID the channel had in the original server.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     Gets or sets the kind: category, text, voice, stage or forum.
    /// </summary>
    public string Type { get; set; } = "text";

    /// <summary>
    ///     Gets or sets the channel name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the topic.
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    ///     Gets or sets the position.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    ///     Gets or sets the slowmode in seconds.
    /// </summary>
    public int SlowMode { get; set; }

    /// <summary>
    ///     Gets or sets whether the channel is age restricted.
    /// </summary>
    public bool Nsfw { get; set; }

    /// <summary>
    ///     Gets or sets the voice bitrate.
    /// </summary>
    public int Bitrate { get; set; }

    /// <summary>
    ///     Gets or sets the voice user limit.
    /// </summary>
    public int? UserLimit { get; set; }

    /// <summary>
    ///     Gets or sets the backed-up ID of the parent category.
    /// </summary>
    public ulong? ParentId { get; set; }

    /// <summary>
    ///     Gets or sets the permission overwrites.
    /// </summary>
    public List<BackupOverwrite> Overwrites { get; set; } = [];
}

/// <summary>
///     An emoji held in a backup.
/// </summary>
public class BackupEmoji
{
    /// <summary>
    ///     Gets or sets the emoji name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets whether the emoji is animated.
    /// </summary>
    public bool Animated { get; set; }

    /// <summary>
    ///     Gets or sets the image bytes.
    /// </summary>
    public byte[] Image { get; set; } = [];
}

/// <summary>
///     The saved messages of one channel.
/// </summary>
public class BackupChannelMessages
{
    /// <summary>
    ///     Gets or sets the backed-up ID of the channel.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the messages, oldest first.
    /// </summary>
    public List<BackupMessage> Messages { get; set; } = [];
}

/// <summary>
///     A saved message.
/// </summary>
public class BackupMessage
{
    /// <summary>
    ///     Gets or sets the author's display name.
    /// </summary>
    public string Author { get; set; } = "";

    /// <summary>
    ///     Gets or sets the author's avatar URL.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    ///     Gets or sets the message text, with attachment links appended.
    /// </summary>
    public string Content { get; set; } = "";
}

/// <summary>
///     How a restore treats what the server already has.
/// </summary>
public enum BackupRestoreMode
{
    /// <summary>
    ///     Deletes the roles, channels and emojis first, then recreates everything.
    /// </summary>
    Replace,

    /// <summary>
    ///     Only adds the roles, channels and emojis that are missing.
    /// </summary>
    AddMissing
}

/// <summary>
///     Why a backup could not be created or restored.
/// </summary>
public enum BackupError
{
    /// <summary>
    ///     Nothing is wrong.
    /// </summary>
    None,

    /// <summary>
    ///     The user already has the most backups allowed.
    /// </summary>
    LimitReached,

    /// <summary>
    ///     The user made a backup too recently.
    /// </summary>
    Cooldown,

    /// <summary>
    ///     The backup would be too large.
    /// </summary>
    TooLarge,

    /// <summary>
    ///     The backup does not exist or belongs to someone else.
    /// </summary>
    NotFound,

    /// <summary>
    ///     The person is not the owner or an administrator of the server.
    /// </summary>
    NotAdmin,

    /// <summary>
    ///     The person is not the owner or an administrator of the server the backup came from.
    /// </summary>
    NotAdminInSource,

    /// <summary>
    ///     The bot lacks permissions it needs.
    /// </summary>
    BotMissingPermissions,

    /// <summary>
    ///     The bot's top role is too low to manage roles.
    /// </summary>
    BotRoleTooLow,

    /// <summary>
    ///     A restore is already running in the server.
    /// </summary>
    RestoreRunning,

    /// <summary>
    ///     The backup data could not be read.
    /// </summary>
    Corrupt
}

/// <summary>
///     Running totals of a restore, reported as it goes.
/// </summary>
public class BackupRestoreProgress
{
    /// <summary>
    ///     Gets or sets what the restore is doing now.
    /// </summary>
    public string Step { get; set; } = "";

    /// <summary>
    ///     Gets or sets how many roles were created.
    /// </summary>
    public int RolesCreated { get; set; }

    /// <summary>
    ///     Gets or sets how many channels and categories were created.
    /// </summary>
    public int ChannelsCreated { get; set; }

    /// <summary>
    ///     Gets or sets how many emojis were created.
    /// </summary>
    public int EmojisCreated { get; set; }

    /// <summary>
    ///     Gets or sets how many messages were posted.
    /// </summary>
    public int MessagesRestored { get; set; }

    /// <summary>
    ///     Gets the things that failed or were skipped.
    /// </summary>
    public List<string> Failures { get; } = [];
}
