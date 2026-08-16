namespace Mewdeko.Controllers.Common.Embeds;

/// <summary>
///     Request model for sending a built embed to a guild channel.
/// </summary>
public class SendEmbedRequest
{
    /// <summary>
    ///     The ID of the user requesting the send. Used for permission checks when the caller has no
    ///     dashboard JWT identity (mobile/legacy callers).
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The channel to send the message to. May be a text channel, announcement channel, thread, or the
    ///     text chat of a voice/stage channel.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The JSON representation of the message, in the same
    ///     <c>{ content, embeds, components }</c> shape the embed builder produces. Plain text is also
    ///     accepted and sent as message content.
    /// </summary>
    public string JsonCode { get; set; } = null!;

    /// <summary>
    ///     Whether to deliver the message through a channel webhook instead of as the bot itself. Requires
    ///     the Manage Webhooks permission for both the requesting user and the bot.
    /// </summary>
    public bool UseWebhook { get; set; }

    /// <summary>
    ///     A saved persona to send as. When set, the persona supplies the display name and avatar and the
    ///     ad-hoc webhook fields below are ignored. Requires <see cref="UseWebhook" />.
    /// </summary>
    public int? PersonaId { get; set; }

    /// <summary>
    ///     The display name the webhook posts under. Ignored when <see cref="UseWebhook" /> is false, or when
    ///     <see cref="PersonaId" /> is set.
    /// </summary>
    public string? WebhookUsername { get; set; }

    /// <summary>
    ///     The avatar the webhook posts with, as a URL. Ignored when <see cref="UseWebhook" /> is false or
    ///     when <see cref="PersonaId" /> is set. Uploading an image instead means saving a persona, which
    ///     owns a hosted copy of it.
    /// </summary>
    public string? WebhookAvatarUrl { get; set; }
}

/// <summary>
///     Response model describing a message the embed builder sent.
/// </summary>
public class SendEmbedResponse
{
    /// <summary>
    ///     The ID of the message that was sent.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    ///     The channel the message was sent to.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The name of the channel the message was sent to.
    /// </summary>
    public string ChannelName { get; set; } = null!;

    /// <summary>
    ///     A jump link to the sent message.
    /// </summary>
    public string MessageLink { get; set; } = null!;

    /// <summary>
    ///     Whether the message was delivered through a webhook rather than as the bot.
    /// </summary>
    public bool SentViaWebhook { get; set; }

    /// <summary>
    ///     The display name the webhook posted under, when a webhook was used.
    /// </summary>
    public string? WebhookUsername { get; set; }

    /// <summary>
    ///     The name of the persona the message was sent as, when one was used.
    /// </summary>
    public string? PersonaName { get; set; }

    /// <summary>
    ///     Whether everyone/here and role mentions were stripped because the requesting user lacks the
    ///     Mention Everyone permission in the target channel.
    /// </summary>
    public bool MentionsSuppressed { get; set; }
}

/// <summary>
///     A channel the embed builder may target, along with the effective permissions the requesting user
///     and the bot each have in it. Channels the user cannot even see are never returned.
/// </summary>
public class SendableChannelResponse
{
    /// <summary>
    ///     The channel ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     The channel name.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    ///     The category the channel sits under, if any. Threads report their parent channel's category.
    /// </summary>
    public ulong? CategoryId { get; set; }

    /// <summary>
    ///     The name of the category the channel sits under, if any.
    /// </summary>
    public string? CategoryName { get; set; }

    /// <summary>
    ///     The channel's sort position within the guild.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    ///     Whether this channel is a thread.
    /// </summary>
    public bool IsThread { get; set; }

    /// <summary>
    ///     Whether this channel is an announcement channel, whose messages can be published.
    /// </summary>
    public bool IsAnnouncement { get; set; }

    /// <summary>
    ///     Whether the requesting user may send messages here.
    /// </summary>
    public bool CanSend { get; set; }

    /// <summary>
    ///     Whether the requesting user may send embeds here.
    /// </summary>
    public bool CanEmbed { get; set; }

    /// <summary>
    ///     Whether the requesting user may mention everyone, here, and non-mentionable roles here.
    /// </summary>
    public bool CanMentionEveryone { get; set; }

    /// <summary>
    ///     Whether the requesting user may manage webhooks here, which is required to send via webhook.
    /// </summary>
    public bool CanUseWebhooks { get; set; }

    /// <summary>
    ///     Whether the bot may send messages here.
    /// </summary>
    public bool BotCanSend { get; set; }

    /// <summary>
    ///     Whether the bot may send embeds here.
    /// </summary>
    public bool BotCanEmbed { get; set; }

    /// <summary>
    ///     Whether the bot may manage webhooks here.
    /// </summary>
    public bool BotCanUseWebhooks { get; set; }

    /// <summary>
    ///     A human readable explanation of why the channel cannot be posted to, or null when it can.
    /// </summary>
    public string? Restriction { get; set; }
}