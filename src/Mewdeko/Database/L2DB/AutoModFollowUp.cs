#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     What Mewdeko does to a member who triggered an AutoMod rule, besides the rule's own actions.
/// </summary>
public enum AutoModPunishAction
{
    /// <summary>
    ///     Nothing.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Applies a Discord timeout.
    /// </summary>
    Timeout = 1,

    /// <summary>
    ///     Applies a Mewdeko mute.
    /// </summary>
    Mute = 2
}

/// <summary>
///     What happens to a member who triggers a rule too many times within the window.
/// </summary>
public enum AutoModEscalateAction
{
    /// <summary>
    ///     Nothing.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Kicks the member.
    /// </summary>
    Kick = 1,

    /// <summary>
    ///     Bans the member.
    /// </summary>
    Ban = 2
}

/// <summary>
///     Represents the follow-up actions for one native AutoMod rule.
/// </summary>
[Table("AutoModFollowUps")]
public class AutoModFollowUp
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
    ///     Gets or sets the Discord AutoMod rule ID.
    /// </summary>
    [Column("RuleId")]
    public ulong RuleId { get; set; }

    /// <summary>
    ///     Gets or sets whether the member is warned through Mewdeko's warning system.
    /// </summary>
    [Column("Warn")]
    public bool Warn { get; set; }

    /// <summary>
    ///     Gets or sets the warning reason, or null for the default.
    /// </summary>
    [Column("WarnReason")]
    public string? WarnReason { get; set; }

    /// <summary>
    ///     Gets or sets the timeout or mute applied on every hit.
    /// </summary>
    [Column("PunishAction")]
    public AutoModPunishAction PunishAction { get; set; }

    /// <summary>
    ///     Gets or sets how long the timeout or mute lasts, in minutes.
    /// </summary>
    [Column("PunishMinutes")]
    public int PunishMinutes { get; set; }

    /// <summary>
    ///     Gets or sets what happens once the hit count is reached.
    /// </summary>
    [Column("EscalateAction")]
    public AutoModEscalateAction EscalateAction { get; set; }

    /// <summary>
    ///     Gets or sets how many hits trigger the escalation.
    /// </summary>
    [Column("EscalateHits")]
    public int EscalateHits { get; set; }

    /// <summary>
    ///     Gets or sets the window the hits are counted in, in minutes.
    /// </summary>
    [Column("EscalateWindowMinutes")]
    public int EscalateWindowMinutes { get; set; }

    /// <summary>
    ///     Gets or sets the channel follow-ups are logged to.
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
///     Represents one member triggering one AutoMod rule.
/// </summary>
[Table("AutoModHits")]
public class AutoModHit
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
    ///     Gets or sets the rule ID.
    /// </summary>
    [Column("RuleId")]
    public ulong RuleId { get; set; }

    /// <summary>
    ///     Gets or sets the member's user ID.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets when the rule was triggered.
    /// </summary>
    [Column("HitAt")]
    public DateTime HitAt { get; set; }
}
