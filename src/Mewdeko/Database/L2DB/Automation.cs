#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     The event that starts an automation.
/// </summary>
public enum AutomationTriggerType
{
    /// <summary>
    ///     A member sends a message.
    /// </summary>
    MessageSent = 0,

    /// <summary>
    ///     A member edits a message.
    /// </summary>
    MessageEdited = 1,

    /// <summary>
    ///     A message is deleted.
    /// </summary>
    MessageDeleted = 2,

    /// <summary>
    ///     A member adds a reaction.
    /// </summary>
    ReactionAdded = 3,

    /// <summary>
    ///     A member removes a reaction.
    /// </summary>
    ReactionRemoved = 4,

    /// <summary>
    ///     A member clicks a button posted by an automation.
    /// </summary>
    ButtonClicked = 5,

    /// <summary>
    ///     A member joins the server.
    /// </summary>
    MemberJoined = 6,

    /// <summary>
    ///     A member leaves the server.
    /// </summary>
    MemberLeft = 7,

    /// <summary>
    ///     A member gains a role.
    /// </summary>
    RoleGained = 8,

    /// <summary>
    ///     A member loses a role.
    /// </summary>
    RoleLost = 9,

    /// <summary>
    ///     A member joins a voice channel.
    /// </summary>
    VoiceJoined = 10,

    /// <summary>
    ///     A member leaves a voice channel.
    /// </summary>
    VoiceLeft = 11,

    /// <summary>
    ///     A thread is created.
    /// </summary>
    ThreadCreated = 12
}

/// <summary>
///     What an automation's cooldown is counted per.
/// </summary>
public enum AutomationCooldownScope
{
    /// <summary>
    ///     Each member has their own cooldown.
    /// </summary>
    User = 0,

    /// <summary>
    ///     Each channel has its own cooldown.
    /// </summary>
    Channel = 1
}

/// <summary>
///     Represents a no-code rule made of one trigger, a list of conditions and an ordered list of actions.
/// </summary>
[Table("Automations")]
public class Automation
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID the automation belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the name shown in lists and the run log.
    /// </summary>
    [Column("Name")]
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets whether the automation runs.
    /// </summary>
    [Column("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the event that starts the automation.
    /// </summary>
    [Column("Trigger")]
    public AutomationTriggerType Trigger { get; set; }

    /// <summary>
    ///     Gets or sets the conditions, as a JSON array. All of them have to pass.
    /// </summary>
    [Column("Conditions")]
    public string Conditions { get; set; } = "[]";

    /// <summary>
    ///     Gets or sets the actions, as a JSON array. They run in order.
    /// </summary>
    [Column("Actions")]
    public string Actions { get; set; } = "[]";

    /// <summary>
    ///     Gets or sets the seconds before the same member or channel can run the automation again, or 0 for none.
    /// </summary>
    [Column("CooldownSeconds")]
    public int CooldownSeconds { get; set; }

    /// <summary>
    ///     Gets or sets what the cooldown is counted per.
    /// </summary>
    [Column("CooldownScope")]
    public AutomationCooldownScope CooldownScope { get; set; }

    /// <summary>
    ///     Gets or sets whether an action that fails on a rate limit or a temporary Discord error is tried again.
    /// </summary>
    [Column("RetryOnFailure")]
    public bool RetryOnFailure { get; set; }

    /// <summary>
    ///     Gets or sets whether events caused by bots are ignored.
    /// </summary>
    [Column("IgnoreBots")]
    public bool IgnoreBots { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether messages the automation sends may mention everyone, here and roles.
    /// </summary>
    [Column("AllowMassMentions")]
    public bool AllowMassMentions { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the member who created the automation.
    /// </summary>
    [Column("CreatorId")]
    public ulong CreatorId { get; set; }

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }

    /// <summary>
    ///     Gets or sets when the row was last changed.
    /// </summary>
    [Column("DateModified")]
    public DateTime DateModified { get; set; }
}

/// <summary>
///     Represents one run of an automation, kept in its run log.
/// </summary>
[Table("AutomationRuns")]
public class AutomationRun
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the automation that ran.
    /// </summary>
    [Column("AutomationId")]
    public int AutomationId { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets whether every action finished.
    /// </summary>
    [Column("Success")]
    public bool Success { get; set; }

    /// <summary>
    ///     Gets or sets whether the run was started by hand as a test.
    /// </summary>
    [Column("IsTest")]
    public bool IsTest { get; set; }

    /// <summary>
    ///     Gets or sets the position, from 0, of the action that failed.
    /// </summary>
    [Column("FailedActionIndex")]
    public int? FailedActionIndex { get; set; }

    /// <summary>
    ///     Gets or sets the type of the action that failed.
    /// </summary>
    [Column("FailedActionType")]
    public string? FailedActionType { get; set; }

    /// <summary>
    ///     Gets or sets why the run failed.
    /// </summary>
    [Column("Reason")]
    public string? Reason { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the member who caused the run, or 0.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the channel the run happened in, or 0.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets how long the run took in milliseconds.
    /// </summary>
    [Column("DurationMs")]
    public int DurationMs { get; set; }

    /// <summary>
    ///     Gets or sets when the run started.
    /// </summary>
    [Column("RanAt")]
    public DateTime RanAt { get; set; }
}
