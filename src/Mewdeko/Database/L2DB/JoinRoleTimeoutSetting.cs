#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     What happens to a member who still lacks a required role when the join role timeout runs out.
/// </summary>
public enum JoinRoleTimeoutAction
{
    /// <summary>
    ///     Kicks the member.
    /// </summary>
    Kick = 0,

    /// <summary>
    ///     Bans the member.
    /// </summary>
    Ban = 1,

    /// <summary>
    ///     Gives the member a role, such as Unverified.
    /// </summary>
    AddRole = 2,

    /// <summary>
    ///     Removes every role from the member.
    /// </summary>
    RemoveRoles = 3,

    /// <summary>
    ///     Times the member out.
    /// </summary>
    Timeout = 4
}

/// <summary>
///     Represents a guild's join role timeout settings.
/// </summary>
[Table("JoinRoleTimeoutSettings")]
public class JoinRoleTimeoutSetting
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
    ///     Gets or sets whether the timeout is on in this guild.
    /// </summary>
    [Column("Enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets how many minutes a member has after joining to get a required role.
    /// </summary>
    [Column("TimeoutMinutes")]
    public int TimeoutMinutes { get; set; } = 1440;

    /// <summary>
    ///     Gets or sets the space separated IDs of the roles that count as verified. Any one of them is enough.
    /// </summary>
    [Column("RequiredRoleIds")]
    public string RequiredRoleIds { get; set; } = "";

    /// <summary>
    ///     Gets or sets the space separated IDs of the roles whose members are never acted on.
    /// </summary>
    [Column("ExemptRoleIds")]
    public string ExemptRoleIds { get; set; } = "";

    /// <summary>
    ///     Gets or sets what happens to a member when the timeout runs out.
    /// </summary>
    [Column("Action")]
    public JoinRoleTimeoutAction Action { get; set; }

    /// <summary>
    ///     Gets or sets the role given by the add role action.
    /// </summary>
    [Column("ActionRoleId")]
    public ulong? ActionRoleId { get; set; }

    /// <summary>
    ///     Gets or sets how many minutes before the timeout a warning is sent, or null for no warning.
    /// </summary>
    [Column("WarnBeforeMinutes")]
    public int? WarnBeforeMinutes { get; set; }

    /// <summary>
    ///     Gets or sets the warning message, or null for the default.
    /// </summary>
    [Column("WarnMessage")]
    public string? WarnMessage { get; set; }

    /// <summary>
    ///     Gets or sets the message sent when the action is taken, or null for the default.
    /// </summary>
    [Column("ActionMessage")]
    public string? ActionMessage { get; set; }

    /// <summary>
    ///     Gets or sets the channel actions are logged to.
    /// </summary>
    [Column("LogChannelId")]
    public ulong? LogChannelId { get; set; }

    /// <summary>
    ///     Gets or sets whether current members without a required role start their countdown when the timeout is
    ///     turned on.
    /// </summary>
    [Column("IncludeExistingMembers")]
    public bool IncludeExistingMembers { get; set; }

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     Represents a member whose join role timeout is counting down.
/// </summary>
[Table("JoinRoleTimeoutPending")]
public class JoinRoleTimeoutPending
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
    ///     Gets or sets the member's user ID.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets when the countdown started.
    /// </summary>
    [Column("JoinedAt")]
    public DateTime JoinedAt { get; set; }

    /// <summary>
    ///     Gets or sets when the warning was sent, or null if it has not been.
    /// </summary>
    [Column("WarnedAt")]
    public DateTime? WarnedAt { get; set; }
}
