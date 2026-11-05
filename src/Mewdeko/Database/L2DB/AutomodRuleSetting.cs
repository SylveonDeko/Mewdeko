#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     The automod rules a server can turn on.
/// </summary>
public enum AutomodRule
{
    /// <summary>
    ///     Messages that are mostly capital letters.
    /// </summary>
    Caps = 0,

    /// <summary>
    ///     Too many attachments from one member in a short time.
    /// </summary>
    Attachments = 1,

    /// <summary>
    ///     Channels where every message needs an attachment, image or link.
    /// </summary>
    Media = 2,

    /// <summary>
    ///     Attachments with dangerous file extensions.
    /// </summary>
    Files = 3,

    /// <summary>
    ///     Too many emojis, custom and unicode, in one message.
    /// </summary>
    Emoji = 4,

    /// <summary>
    ///     Too many spoiler tags or spoiler attachments in one message.
    /// </summary>
    Spoilers = 5,

    /// <summary>
    ///     Text stacked with combining characters.
    /// </summary>
    Zalgo = 6,

    /// <summary>
    ///     Channels where only bot commands are allowed.
    /// </summary>
    Commands = 7,

    /// <summary>
    ///     Channels where every message needs an image.
    /// </summary>
    ImageOnly = 8,

    /// <summary>
    ///     Channels where every message needs a video.
    /// </summary>
    VideoOnly = 9
}

/// <summary>
///     What an automod rule does when it triggers. Any mix can be applied together.
/// </summary>
[Flags]
public enum AutomodAction
{
    /// <summary>
    ///     No actions.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Deletes the message.
    /// </summary>
    Delete = 1,

    /// <summary>
    ///     Warns the member.
    /// </summary>
    Warn = 2,

    /// <summary>
    ///     Times the member out.
    /// </summary>
    Timeout = 4,

    /// <summary>
    ///     Mutes the member.
    /// </summary>
    Mute = 8,

    /// <summary>
    ///     Kicks the member.
    /// </summary>
    Kick = 16,

    /// <summary>
    ///     Bans the member.
    /// </summary>
    Ban = 32,

    /// <summary>
    ///     Sends a short message in the channel.
    /// </summary>
    Message = 64,

    /// <summary>
    ///     Sends the member a DM.
    /// </summary>
    Dm = 128,

    /// <summary>
    ///     Sends the message to the review channel and holds the punishments until staff approve them.
    /// </summary>
    Review = 256
}

/// <summary>
///     Represents one automod rule's settings in a guild.
/// </summary>
[Table("AutomodRuleSettings")]
public class AutomodRuleSetting
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
    ///     Gets or sets which rule this row configures.
    /// </summary>
    [Column("Rule")]
    public AutomodRule Rule { get; set; }

    /// <summary>
    ///     Gets or sets whether the rule is on.
    /// </summary>
    [Column("Enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets the actions applied when the rule triggers.
    /// </summary>
    [Column("Actions")]
    public AutomodAction Actions { get; set; } = AutomodAction.Delete;

    /// <summary>
    ///     Gets or sets how many minutes the timeout or mute lasts.
    /// </summary>
    [Column("ActionMinutes")]
    public int ActionMinutes { get; set; } = 10;

    /// <summary>
    ///     Gets or sets the message sent in the channel, or null for the default.
    /// </summary>
    [Column("ActionMessage")]
    public string? ActionMessage { get; set; }

    /// <summary>
    ///     Gets or sets the space separated IDs of roles the rule ignores.
    /// </summary>
    [Column("ExemptRoleIds")]
    public string ExemptRoleIds { get; set; } = "";

    /// <summary>
    ///     Gets or sets the space separated IDs of channels the rule ignores.
    /// </summary>
    [Column("ExemptChannelIds")]
    public string ExemptChannelIds { get; set; } = "";

    /// <summary>
    ///     Gets or sets the shortest message the caps filter looks at.
    /// </summary>
    [Column("MinLength")]
    public int MinLength { get; set; } = 20;

    /// <summary>
    ///     Gets or sets the share of letters that must be uppercase for the caps filter to trigger.
    /// </summary>
    [Column("CapsPercent")]
    public int CapsPercent { get; set; } = 70;

    /// <summary>
    ///     Gets or sets how many attachments one member may post in the window.
    /// </summary>
    [Column("MaxAttachments")]
    public int MaxAttachments { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the length of the attachment spam window in seconds.
    /// </summary>
    [Column("WindowSeconds")]
    public int WindowSeconds { get; set; } = 10;

    /// <summary>
    ///     Gets or sets the space separated IDs of the channels a channel rule covers: media-only, image-only, video-only
    ///     or commands-only.
    /// </summary>
    [Column("MediaChannelIds")]
    public string MediaChannelIds { get; set; } = "";

    /// <summary>
    ///     Gets or sets how many emojis a message may hold before the emoji rule triggers.
    /// </summary>
    [Column("MaxEmojis")]
    public int MaxEmojis { get; set; } = 10;

    /// <summary>
    ///     Gets or sets how many spoilers a message may hold before the spoiler rule triggers.
    /// </summary>
    [Column("MaxSpoilers")]
    public int MaxSpoilers { get; set; } = 3;

    /// <summary>
    ///     Gets or sets how many combining marks one character may carry before the zalgo rule triggers.
    /// </summary>
    [Column("ZalgoThreshold")]
    public int ZalgoThreshold { get; set; } = 4;

    /// <summary>
    ///     Gets or sets whether messages in threads of a channel rule's channels are allowed.
    /// </summary>
    [Column("AllowThreads")]
    public bool AllowThreads { get; set; }

    /// <summary>
    ///     Gets or sets the space separated blocked file extensions, or null for the default list.
    /// </summary>
    [Column("Extensions")]
    public string? Extensions { get; set; }

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
}
