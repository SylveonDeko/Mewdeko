#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a guild's verification gate settings.
/// </summary>
[Table("VerificationSettings")]
public class VerificationSetting
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID these settings belong to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets whether the gate is on in this guild.
    /// </summary>
    [Column("Enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets the channel holding the verification button.
    /// </summary>
    [Column("ChannelId")]
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the message holding the verification button.
    /// </summary>
    [Column("MessageId")]
    public ulong? MessageId { get; set; }

    /// <summary>
    ///     Gets or sets the role given to members on join and removed when they pass.
    /// </summary>
    [Column("UnverifiedRoleId")]
    public ulong? UnverifiedRoleId { get; set; }

    /// <summary>
    ///     Gets or sets the role given to members when they pass.
    /// </summary>
    [Column("VerifiedRoleId")]
    public ulong? VerifiedRoleId { get; set; }

    /// <summary>
    ///     Gets or sets how many wrong answers a member gets before failing.
    /// </summary>
    [Column("MaxAttempts")]
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    ///     Gets or sets how many minutes a captcha stays valid, and how long a member who failed is locked out.
    /// </summary>
    [Column("TimeoutMinutes")]
    public int TimeoutMinutes { get; set; } = 10;

    /// <summary>
    ///     Gets or sets whether a member who fails is kicked.
    /// </summary>
    [Column("KickOnFail")]
    public bool KickOnFail { get; set; }

    /// <summary>
    ///     Gets or sets the channel verification results are logged to.
    /// </summary>
    [Column("LogChannelId")]
    public ulong? LogChannelId { get; set; }

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     Represents a channel permission overwrite as it was before the verification gate changed it.
/// </summary>
[Table("VerificationPermissionBackups")]
public class VerificationPermissionBackup
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
    ///     Gets or sets the channel whose overwrite was changed.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the role the overwrite is for.
    /// </summary>
    [Column("RoleId")]
    public ulong RoleId { get; set; }

    /// <summary>
    ///     Gets or sets whether the role had an overwrite on the channel before.
    /// </summary>
    [Column("HadOverwrite")]
    public bool HadOverwrite { get; set; }

    /// <summary>
    ///     Gets or sets the allowed permissions before the change.
    /// </summary>
    [Column("PreviousAllow")]
    public ulong PreviousAllow { get; set; }

    /// <summary>
    ///     Gets or sets the denied permissions before the change.
    /// </summary>
    [Column("PreviousDeny")]
    public ulong PreviousDeny { get; set; }
}
