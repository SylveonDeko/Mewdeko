using DataModel;

namespace Mewdeko.Modules.Automations.Common;

/// <summary>
///     What conditions can look at for one event, copied out of the Discord objects so conditions can be checked
///     without a connection.
/// </summary>
public sealed class AutomationEventData
{
    /// <summary>
    ///     Gets or sets the trigger that fired.
    /// </summary>
    public AutomationTriggerType Trigger { get; init; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    public ulong GuildId { get; init; }

    /// <summary>
    ///     Gets or sets the ID of the channel or thread the event happened in, or 0.
    /// </summary>
    public ulong ChannelId { get; init; }

    /// <summary>
    ///     Gets or sets the ID of the channel a thread belongs to, or 0.
    /// </summary>
    public ulong ParentChannelId { get; init; }

    /// <summary>
    ///     Gets or sets the ID of the category the channel is in, or 0.
    /// </summary>
    public ulong CategoryId { get; init; }

    /// <summary>
    ///     Gets or sets the ID of the member who caused the event, or 0 when unknown.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    ///     Gets or sets whether the member is a bot.
    /// </summary>
    public bool IsBot { get; init; }

    /// <summary>
    ///     Gets or sets the IDs of the roles the member has.
    /// </summary>
    public IReadOnlyCollection<ulong> RoleIds { get; init; } = [];

    /// <summary>
    ///     Gets or sets the ID of the role that was gained or lost.
    /// </summary>
    public ulong ChangedRoleId { get; init; }

    /// <summary>
    ///     Gets or sets the ID of the message, or 0.
    /// </summary>
    public ulong MessageId { get; init; }

    /// <summary>
    ///     Gets or sets whether the message could be read. A deleted message the bot never saw cannot.
    /// </summary>
    public bool MessageKnown { get; init; }

    /// <summary>
    ///     Gets or sets the text of the message.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    ///     Gets or sets the types of the message's attachments, as image, video, audio or file.
    /// </summary>
    public IReadOnlyCollection<string> AttachmentTypes { get; init; } = [];

    /// <summary>
    ///     Gets or sets whether the message is a reply.
    /// </summary>
    public bool IsReply { get; init; }

    /// <summary>
    ///     Gets or sets the reaction emoji as <see cref="AutomationEmoji.Normalize(Discord.IEmote)" /> writes it.
    /// </summary>
    public string? EmojiKey { get; init; }

    /// <summary>
    ///     Gets or sets how many members used the reaction on the message.
    /// </summary>
    public int ReactionCount { get; init; }

    /// <summary>
    ///     Gets or sets the custom ID of the clicked button, without the prefix.
    /// </summary>
    public string? ButtonCustomId { get; init; }
}
