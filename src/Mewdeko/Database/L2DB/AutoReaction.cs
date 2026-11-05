#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Which messages an auto reaction rule reacts to.
/// </summary>
public enum AutoReactionMessageType
{
    /// <summary>
    ///     Every message.
    /// </summary>
    Any = 0,

    /// <summary>
    ///     Messages with no attachments, images or links.
    /// </summary>
    Text = 1,

    /// <summary>
    ///     Messages with an image.
    /// </summary>
    Images = 2,

    /// <summary>
    ///     Messages with a video.
    /// </summary>
    Videos = 3,

    /// <summary>
    ///     Messages with an image or a video.
    /// </summary>
    Media = 4,

    /// <summary>
    ///     Messages with a link.
    /// </summary>
    Links = 5
}

/// <summary>
///     Represents a rule that reacts to messages in a channel.
/// </summary>
[Table("AutoReactions")]
public class AutoReaction
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID the rule belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the channel the rule applies to. A forum channel reacts to the first post of each thread.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the space separated emojis, in the order they are added.
    /// </summary>
    [Column("Emojis")]
    public string Emojis { get; set; } = "";

    /// <summary>
    ///     Gets or sets which messages the rule reacts to.
    /// </summary>
    [Column("MessageTypes")]
    public AutoReactionMessageType MessageTypes { get; set; }

    /// <summary>
    ///     Gets or sets whether messages from bots are skipped.
    /// </summary>
    [Column("IgnoreBots")]
    public bool IgnoreBots { get; set; }

    /// <summary>
    ///     Gets or sets whether messages from webhooks are skipped.
    /// </summary>
    [Column("IgnoreWebhooks")]
    public bool IgnoreWebhooks { get; set; }

    /// <summary>
    ///     Gets or sets whether replies are skipped.
    /// </summary>
    [Column("IgnoreReplies")]
    public bool IgnoreReplies { get; set; }

    /// <summary>
    ///     Gets or sets the space separated IDs of the roles allowed to trigger the rule. Empty allows everyone.
    /// </summary>
    [Column("AllowedRoleIds")]
    public string AllowedRoleIds { get; set; } = "";

    /// <summary>
    ///     Gets or sets the space separated IDs of the roles that never trigger the rule.
    /// </summary>
    [Column("IgnoredRoleIds")]
    public string IgnoredRoleIds { get; set; } = "";

    /// <summary>
    ///     Gets or sets the space separated IDs of the members that never trigger the rule.
    /// </summary>
    [Column("IgnoredUserIds")]
    public string IgnoredUserIds { get; set; } = "";

    /// <summary>
    ///     Gets or sets the newline separated words that stop the rule when a message has one.
    /// </summary>
    [Column("SkipWords")]
    public string SkipWords { get; set; } = "";

    /// <summary>
    ///     Gets or sets the newline separated words of which a message needs at least one. Empty needs none.
    /// </summary>
    [Column("RequiredWords")]
    public string RequiredWords { get; set; } = "";

    /// <summary>
    ///     Gets or sets the order of the rule in its guild. Lower runs first, so its reactions are added first.
    /// </summary>
    [Column("Priority")]
    public int Priority { get; set; }

    /// <summary>
    ///     Gets or sets whether messages that do not fit the message type are deleted.
    /// </summary>
    [Column("DeleteNonMatching")]
    public bool DeleteNonMatching { get; set; }

    /// <summary>
    ///     Gets or sets whether the rule is on.
    /// </summary>
    [Column("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets when the rule was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
