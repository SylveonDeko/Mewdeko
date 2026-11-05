namespace Mewdeko.Controllers.Common.Protection;

/// <summary>
///     Request model for turning an automod rule on or off and changing it. Fields left out stay as they are.
/// </summary>
public class AutomodRuleRequest
{
    /// <summary>
    ///     Whether the rule is on
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The actions, for example "delete warn review". Needed when turning the rule on
    /// </summary>
    public string? Actions { get; set; }

    /// <summary>
    ///     How many minutes timeouts and mutes last
    /// </summary>
    public int? ActionMinutes { get; set; }

    /// <summary>
    ///     The message sent in the channel, or empty for the default
    /// </summary>
    public string? ActionMessage { get; set; }

    /// <summary>
    ///     The shortest message the caps filter looks at
    /// </summary>
    public int? MinLength { get; set; }

    /// <summary>
    ///     The share of letters that must be uppercase for the caps filter to trigger
    /// </summary>
    public int? CapsPercent { get; set; }

    /// <summary>
    ///     How many attachments one member may post in the window
    /// </summary>
    public int? MaxAttachments { get; set; }

    /// <summary>
    ///     The length of the attachment window in seconds
    /// </summary>
    public int? WindowSeconds { get; set; }

    /// <summary>
    ///     How many emojis a message may hold before the emoji rule triggers
    /// </summary>
    public int? MaxEmojis { get; set; }

    /// <summary>
    ///     How many spoilers a message may hold before the spoiler rule triggers
    /// </summary>
    public int? MaxSpoilers { get; set; }

    /// <summary>
    ///     How many combining marks one character may carry before the zalgo rule triggers
    /// </summary>
    public int? ZalgoThreshold { get; set; }

    /// <summary>
    ///     Whether messages in threads of channel rules' channels are allowed
    /// </summary>
    public bool? AllowThreads { get; set; }

    /// <summary>
    ///     The roles exempt from this rule, replacing the current list
    /// </summary>
    public List<ulong>? ExemptRoleIds { get; set; }

    /// <summary>
    ///     The channels exempt from this rule, replacing the current list
    /// </summary>
    public List<ulong>? ExemptChannelIds { get; set; }

    /// <summary>
    ///     The channels of a media-only, image-only, video-only or commands-only rule, replacing the current list
    /// </summary>
    public List<ulong>? MediaChannelIds { get; set; }
}
