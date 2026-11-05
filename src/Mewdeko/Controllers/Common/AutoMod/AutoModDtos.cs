using System.Text.Json.Serialization;
using DataModel;
using Discord;
using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.AutoMod;

/// <summary>
///     A native AutoMod rule, as read.
/// </summary>
public class AutoModRuleDto
{
    /// <summary>
    ///     The rule ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     The rule name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Keyword, Spam, Preset or MentionSpam.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AutoModRuleKind Kind { get; set; }

    /// <summary>
    ///     Whether the rule is on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The keywords.
    /// </summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>
    ///     The regex patterns.
    /// </summary>
    public List<string> RegexPatterns { get; set; } = [];

    /// <summary>
    ///     The words let through.
    /// </summary>
    public List<string> AllowList { get; set; } = [];

    /// <summary>
    ///     The presets.
    /// </summary>
    public List<string> Presets { get; set; } = [];

    /// <summary>
    ///     The most mentions a message may have.
    /// </summary>
    public int? MentionLimit { get; set; }

    /// <summary>
    ///     Whether mention raids are detected too.
    /// </summary>
    public bool MentionRaidProtection { get; set; }

    /// <summary>
    ///     The actions the rule takes.
    /// </summary>
    public List<AutoModActionSpec> Actions { get; set; } = [];

    /// <summary>
    ///     The roles the rule ignores.
    /// </summary>
    public List<ulong> ExemptRoleIds { get; set; } = [];

    /// <summary>
    ///     The channels the rule ignores.
    /// </summary>
    public List<ulong> ExemptChannelIds { get; set; } = [];

    /// <summary>
    ///     The follow-up Mewdeko runs when the rule fires, or null.
    /// </summary>
    public AutoModFollowUpDto? FollowUp { get; set; }
}

/// <summary>
///     What Mewdeko does when a rule fires, as read and as written.
/// </summary>
public class AutoModFollowUpDto
{
    /// <summary>
    ///     Whether the member is warned, so warn punishments apply.
    /// </summary>
    public bool Warn { get; set; }

    /// <summary>
    ///     The warning reason, or null for the default.
    /// </summary>
    public string? WarnReason { get; set; }

    /// <summary>
    ///     None, Timeout or Mute.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AutoModPunishAction PunishAction { get; set; }

    /// <summary>
    ///     How long the timeout or mute lasts, in minutes. A timeout lasts at most 40320.
    /// </summary>
    public int PunishMinutes { get; set; }

    /// <summary>
    ///     None, Kick or Ban.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AutoModEscalateAction EscalateAction { get; set; }

    /// <summary>
    ///     How many hits trigger the escalation, at least 2.
    /// </summary>
    public int EscalateHits { get; set; }

    /// <summary>
    ///     The window the hits are counted in, from 1 to 43200 minutes.
    /// </summary>
    public int EscalateWindowMinutes { get; set; }

    /// <summary>
    ///     The channel follow-ups are logged to.
    /// </summary>
    public ulong? LogChannelId { get; set; }
}

/// <summary>
///     Whether the bot can manage AutoMod rules in a guild.
/// </summary>
public class AutoModStatusDto
{
    /// <summary>
    ///     Whether the bot has Manage Server.
    /// </summary>
    public bool CanManage { get; set; }

    /// <summary>
    ///     Whether the bot has Moderate Members, needed for timeouts.
    /// </summary>
    public bool CanModerate { get; set; }
}

/// <summary>
///     Why an AutoMod request was refused.
/// </summary>
public class AutoModErrorDto
{
    /// <summary>
    ///     The error code.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AutoModError Error { get; set; }

    /// <summary>
    ///     Text describing the error.
    /// </summary>
    public string Message { get; set; } = "";
}
