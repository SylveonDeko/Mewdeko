using System.Text.Json.Serialization;
using Discord;

namespace Mewdeko.Modules.Administration.Common;

/// <summary>
///     The kinds of native AutoMod rule that can be managed.
/// </summary>
public enum AutoModRuleKind
{
    /// <summary>
    ///     Matches keywords and regex patterns.
    /// </summary>
    Keyword,

    /// <summary>
    ///     Matches suspected spam.
    /// </summary>
    Spam,

    /// <summary>
    ///     Matches Discord's preset word lists.
    /// </summary>
    Preset,

    /// <summary>
    ///     Matches messages with too many mentions.
    /// </summary>
    MentionSpam
}

/// <summary>
///     The actions a native AutoMod rule can take.
/// </summary>
public enum AutoModActionKind
{
    /// <summary>
    ///     Blocks the message.
    /// </summary>
    BlockMessage,

    /// <summary>
    ///     Sends an alert to a channel.
    /// </summary>
    SendAlert,

    /// <summary>
    ///     Times the member out.
    /// </summary>
    Timeout
}

/// <summary>
///     One action of a native AutoMod rule.
/// </summary>
public class AutoModActionSpec
{
    /// <summary>
    ///     The action.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AutoModActionKind Kind { get; set; }

    /// <summary>
    ///     The channel alerts are sent to.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     How long the timeout lasts, in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    ///     The text shown to a member whose message is blocked.
    /// </summary>
    public string? CustomMessage { get; set; }
}

/// <summary>
///     What to create or change on a native AutoMod rule. On a change, null fields stay as they are.
/// </summary>
public class AutoModRuleSpec
{
    /// <summary>
    ///     The rule name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     The kind of rule. Only used when creating.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AutoModRuleKind? Kind { get; set; }

    /// <summary>
    ///     Whether the rule is on.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    ///     The keywords of a keyword rule.
    /// </summary>
    public List<string>? Keywords { get; set; }

    /// <summary>
    ///     The regex patterns of a keyword rule.
    /// </summary>
    public List<string>? RegexPatterns { get; set; }

    /// <summary>
    ///     Words a keyword or preset rule lets through.
    /// </summary>
    public List<string>? AllowList { get; set; }

    /// <summary>
    ///     The presets of a preset rule.
    /// </summary>
    public List<KeywordPresetTypes>? Presets { get; set; }

    /// <summary>
    ///     The most mentions a message may have, from 1 to 50.
    /// </summary>
    public int? MentionLimit { get; set; }

    /// <summary>
    ///     Whether mention raids are detected too.
    /// </summary>
    public bool? MentionRaidProtection { get; set; }

    /// <summary>
    ///     The actions the rule takes. Replaces the current ones.
    /// </summary>
    public List<AutoModActionSpec>? Actions { get; set; }

    /// <summary>
    ///     The roles the rule ignores.
    /// </summary>
    public List<ulong>? ExemptRoleIds { get; set; }

    /// <summary>
    ///     The channels the rule ignores.
    /// </summary>
    public List<ulong>? ExemptChannelIds { get; set; }
}

/// <summary>
///     Why an AutoMod change was refused.
/// </summary>
public enum AutoModError
{
    /// <summary>
    ///     Nothing is wrong.
    /// </summary>
    None,

    /// <summary>
    ///     The bot lacks Manage Server.
    /// </summary>
    MissingPermission,

    /// <summary>
    ///     No rule matches.
    /// </summary>
    NotFound,

    /// <summary>
    ///     The name is empty or too long.
    /// </summary>
    InvalidName,

    /// <summary>
    ///     A keyword rule has no keywords or patterns.
    /// </summary>
    MissingTrigger,

    /// <summary>
    ///     The mention limit is outside 1 to 50.
    /// </summary>
    InvalidMentionLimit,

    /// <summary>
    ///     A preset rule has no presets.
    /// </summary>
    MissingPresets,

    /// <summary>
    ///     An action is not valid for this rule.
    /// </summary>
    InvalidAction,

    /// <summary>
    ///     The option is unknown.
    /// </summary>
    InvalidOption,

    /// <summary>
    ///     The value cannot be used.
    /// </summary>
    InvalidValue,

    /// <summary>
    ///     Discord refused the change.
    /// </summary>
    DiscordRejected
}

/// <summary>
///     The outcome of an AutoMod change.
/// </summary>
/// <param name="Error">The error, or none.</param>
/// <param name="Message">Text for the user.</param>
/// <param name="Rule">The rule as it is now.</param>
public record AutoModResult(AutoModError Error, string Message, IAutoModRule? Rule = null)
{
    /// <summary>
    ///     Whether the change went through.
    /// </summary>
    public bool Ok => Error == AutoModError.None;
}
