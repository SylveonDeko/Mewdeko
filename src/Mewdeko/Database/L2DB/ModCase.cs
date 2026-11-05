#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     The kind of moderation action a case records.
/// </summary>
public enum ModCaseAction
{
    /// <summary>
    ///     A warning.
    /// </summary>
    Warn = 0,

    /// <summary>
    ///     A mute.
    /// </summary>
    Mute = 1,

    /// <summary>
    ///     An unmute.
    /// </summary>
    Unmute = 2,

    /// <summary>
    ///     A timeout.
    /// </summary>
    Timeout = 3,

    /// <summary>
    ///     A removed timeout.
    /// </summary>
    Untimeout = 4,

    /// <summary>
    ///     A kick.
    /// </summary>
    Kick = 5,

    /// <summary>
    ///     A ban.
    /// </summary>
    Ban = 6,

    /// <summary>
    ///     A softban.
    /// </summary>
    Softban = 7,

    /// <summary>
    ///     An unban.
    /// </summary>
    Unban = 8,

    /// <summary>
    ///     A mute that also removes the member's roles.
    /// </summary>
    HardMute = 9,

    /// <summary>
    ///     All of a member's roles being removed.
    /// </summary>
    RemoveRoles = 10
}

/// <summary>
///     Represents a short name staff use in place of a full punishment reason.
/// </summary>
[Table("ModReasonAliases")]
public class ModReasonAlias
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID the alias belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the short name, stored lowercase.
    /// </summary>
    [Column("Name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the full reason the name stands for.
    /// </summary>
    [Column("Reason")]
    public string Reason { get; set; } = "";

    /// <summary>
    ///     Gets or sets when the alias was added.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     Represents a guild's custom DM for one punishment action.
/// </summary>
[Table("PunishDmTemplates")]
public class PunishDmTemplate
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID the template belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the action the template is sent for.
    /// </summary>
    [Column("Action")]
    public ModCaseAction Action { get; set; }

    /// <summary>
    ///     Gets or sets the message template, plain text or embed code.
    /// </summary>
    [Column("Template")]
    public string Template { get; set; } = "";
}

/// <summary>
///     Represents one numbered moderation case in a guild.
/// </summary>
[Table("ModCases")]
public class ModCase
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID the case belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the case number, counted per guild.
    /// </summary>
    [Column("CaseNumber")]
    public int CaseNumber { get; set; }

    /// <summary>
    ///     Gets or sets the member the action was taken on.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the moderator who took the action.
    /// </summary>
    [Column("ModeratorId")]
    public ulong ModeratorId { get; set; }

    /// <summary>
    ///     Gets or sets the action taken.
    /// </summary>
    [Column("Action")]
    public ModCaseAction Action { get; set; }

    /// <summary>
    ///     Gets or sets the reason.
    /// </summary>
    [Column("Reason")]
    public string? Reason { get; set; }

    /// <summary>
    ///     Gets or sets how long the action lasts, in seconds, or null if it has no duration.
    /// </summary>
    [Column("DurationSeconds")]
    public long? DurationSeconds { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the linked warning for warn cases.
    /// </summary>
    [Column("WarningId")]
    public int? WarningId { get; set; }

    /// <summary>
    ///     Gets or sets when the case was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     Represents a guild's case and report channels.
/// </summary>
[Table("ModCaseSettings")]
public class ModCaseSetting
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
    ///     Gets or sets the channel new cases are posted to.
    /// </summary>
    [Column("LogChannelId")]
    public ulong? LogChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the channel member reports are posted to.
    /// </summary>
    [Column("ReportChannelId")]
    public ulong? ReportChannelId { get; set; }
}

/// <summary>
///     Represents when a member last filed a report in a guild.
/// </summary>
[Table("ModReportCooldowns")]
public class ModReportCooldown
{
    /// <summary>
    ///     Gets or sets the guild ID the report was filed in.
    /// </summary>
    [Column("GuildId", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the member who filed the report.
    /// </summary>
    [Column("UserId", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets when the member last filed a report.
    /// </summary>
    [Column("LastReportAt")]
    public DateTime LastReportAt { get; set; }
}

/// <summary>
///     Represents a private staff note on a member.
/// </summary>
[Table("ModNotes")]
public class ModNote
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID the note belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the member the note is about.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the moderator who wrote the note.
    /// </summary>
    [Column("ModeratorId")]
    public ulong ModeratorId { get; set; }

    /// <summary>
    ///     Gets or sets the note text.
    /// </summary>
    [Column("Note")]
    public string Note { get; set; } = "";

    /// <summary>
    ///     Gets or sets when the note was added.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
