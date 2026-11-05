using System.Text.Json.Serialization;
using DataModel;

namespace Mewdeko.Controllers.Common.AutoReactions;

/// <summary>
///     An auto reaction rule, as read and as written.
/// </summary>
public class AutoReactionDto
{
    /// <summary>
    ///     The rule ID. Ignored when creating.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The channel the rule applies to. A forum channel reacts to the first post of each thread.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The emojis to add in order, as unicode or as custom emojis from the guild like &lt;:name:id&gt;.
    /// </summary>
    public List<string> Emojis { get; set; } = [];

    /// <summary>
    ///     Any, Text, Images, Videos, Media or Links.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AutoReactionMessageType MessageTypes { get; set; }

    /// <summary>
    ///     Whether messages from bots are skipped.
    /// </summary>
    public bool IgnoreBots { get; set; }

    /// <summary>
    ///     Whether messages from webhooks are skipped.
    /// </summary>
    public bool IgnoreWebhooks { get; set; }

    /// <summary>
    ///     Whether replies are skipped.
    /// </summary>
    public bool IgnoreReplies { get; set; }

    /// <summary>
    ///     The roles allowed to trigger the rule. Empty allows everyone.
    /// </summary>
    public List<ulong> AllowedRoleIds { get; set; } = [];

    /// <summary>
    ///     The roles that never trigger the rule.
    /// </summary>
    public List<ulong> IgnoredRoleIds { get; set; } = [];

    /// <summary>
    ///     The members that never trigger the rule.
    /// </summary>
    public List<ulong> IgnoredUserIds { get; set; } = [];

    /// <summary>
    ///     The words that stop the rule when a message has one.
    /// </summary>
    public List<string> SkipWords { get; set; } = [];

    /// <summary>
    ///     The words of which a message needs one. Empty needs none.
    /// </summary>
    public List<string> RequiredWords { get; set; } = [];

    /// <summary>
    ///     The order of the rule. Lower runs first. Read only, change it with the order endpoint.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    ///     Whether messages that do not fit the message type are deleted.
    /// </summary>
    public bool DeleteNonMatching { get; set; }

    /// <summary>
    ///     Whether the rule is on.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
///     A guild's auto reaction rules and the limits they follow.
/// </summary>
public class AutoReactionListResponse
{
    /// <summary>
    ///     The rules.
    /// </summary>
    public List<AutoReactionDto> Rules { get; set; } = [];

    /// <summary>
    ///     The most emojis a rule can have.
    /// </summary>
    public int MaxEmojis { get; set; }

    /// <summary>
    ///     The most words a rule can have in each word list.
    /// </summary>
    public int MaxWords { get; set; }

    /// <summary>
    ///     The longest a word can be.
    /// </summary>
    public int MaxWordLength { get; set; }

    /// <summary>
    ///     The role whose members can manage auto reactions without Manage Server, or null.
    /// </summary>
    public ulong? ManagerRoleId { get; set; }
}

/// <summary>
///     The new order of a guild's rules.
/// </summary>
public class AutoReactionOrderRequest
{
    /// <summary>
    ///     The rule IDs, first to last.
    /// </summary>
    public List<int> RuleIds { get; set; } = [];
}

/// <summary>
///     The role whose members can manage auto reactions.
/// </summary>
public class AutoReactionManagerRequest
{
    /// <summary>
    ///     The role ID, or null to clear it.
    /// </summary>
    public ulong? RoleId { get; set; }
}
