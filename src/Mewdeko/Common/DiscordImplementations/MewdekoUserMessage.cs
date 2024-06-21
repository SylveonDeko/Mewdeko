// ReSharper disable NotNullMemberIsNotInitialized

// ReSharper disable UnassignedGetOnlyAutoProperty

// ReSharper disable AssignNullToNotNullAttribute

using Poll = Discord.Poll;

namespace Mewdeko.Common.DiscordImplementations;

/// <summary>
/// Class used for faking messages for commands like Sudo
/// </summary>
public class MewdekoUserMessage : IUserMessage
{
    /// <inheritdoc />
    public ulong Id => 0;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt => DateTime.Now;

    /// <inheritdoc />
    public Task DeleteAsync(RequestOptions options = null) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task AddReactionAsync(IEmote emote, RequestOptions options = null) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task RemoveReactionAsync(IEmote emote, IUser user, RequestOptions options = null) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task RemoveReactionAsync(IEmote emote, ulong userId, RequestOptions options = null) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task RemoveAllReactionsAsync(RequestOptions options = null) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task RemoveAllReactionsForEmoteAsync(IEmote emote, RequestOptions options = null) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public IAsyncEnumerable<IReadOnlyCollection<IUser>> GetReactionUsersAsync(IEmote emoji, int limit,
        RequestOptions options = null,
        ReactionType type = ReactionType.Normal)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public MessageType Type => MessageType.Default;

    /// <inheritdoc />
    public MessageSource Source => MessageSource.User;

    /// <inheritdoc />
    public bool IsTTS => false;

    /// <inheritdoc />
    public bool IsPinned => false;

    /// <inheritdoc />
    public bool IsSuppressed => false;

    /// <inheritdoc />
    public string Content { get; set; }

    /// <inheritdoc />
    public string CleanContent { get; set; }

    /// <inheritdoc />
    public DateTimeOffset Timestamp => DateTimeOffset.Now;

    /// <inheritdoc />
    public DateTimeOffset? EditedTimestamp => DateTimeOffset.Now;

    /// <inheritdoc />
    public IMessageChannel Channel { get; set; }

    /// <inheritdoc />
    public IUser Author { get; set; }

    /// <inheritdoc />
    public IReadOnlyCollection<IAttachment> Attachments { get; set; } = new List<IAttachment>();

    /// <inheritdoc />
    public IReadOnlyCollection<IEmbed> Embeds { get; set; } = new List<IEmbed>();

    /// <inheritdoc />
    public IReadOnlyCollection<ITag> Tags { get; set; }

    /// <inheritdoc />
    public IReadOnlyCollection<ulong> MentionedChannelIds { get; set; }

    /// <inheritdoc />
    public IReadOnlyCollection<ulong> MentionedRoleIds { get; set; }

    /// <inheritdoc />
    public IReadOnlyCollection<ulong> MentionedUserIds { get; set; }

    /// <inheritdoc />
    public bool MentionedEveryone { get; set; }

    /// <inheritdoc />
    public MessageActivity Activity { get; set; }

    /// <inheritdoc />
    public MessageApplication Application { get; set; }

    /// <inheritdoc />
    public MessageReference Reference { get; set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<IEmote, ReactionMetadata> Reactions { get; set; }

    /// <inheritdoc />
    public IReadOnlyCollection<IMessageComponent> Components { get; set; }

    /// <inheritdoc />
    public IReadOnlyCollection<IStickerItem> Stickers { get; set; }

    /// <inheritdoc />
    public MessageFlags? Flags { get; set; }

    /// <inheritdoc />
    public IMessageInteraction Interaction { get; set; }

    /// <inheritdoc />
    public Task ModifyAsync(Action<MessageProperties> func, RequestOptions options = null) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task PinAsync(RequestOptions options = null) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task UnpinAsync(RequestOptions options = null) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task CrosspostAsync(RequestOptions options = null) => throw new NotImplementedException();

    /// <inheritdoc />
    public string Resolve(
        TagHandling userHandling = TagHandling.Name,
        TagHandling channelHandling = TagHandling.Name,
        TagHandling roleHandling = TagHandling.Name,
        TagHandling everyoneHandling = TagHandling.Ignore,
        TagHandling emojiHandling = TagHandling.Name) =>
        throw new NotImplementedException();

   /// <inheritdoc/>
    public Task EndPollAsync(RequestOptions options)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<IReadOnlyCollection<IUser>> GetPollAnswerVotersAsync(uint answerId, int? limit = null, ulong? afterId = null,
        RequestOptions options = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public MessageResolvedData ResolvedData { get; }

    /// <inheritdoc />
    public IUserMessage ReferencedMessage { get; set; }

    /// <inheritdoc />
    public IMessageInteractionMetadata InteractionMetadata { get; }

    /// <inheritdoc/>
    public Poll? Poll { get; }

    /// <inheritdoc />
    public IThreadChannel Thread => throw new NotImplementedException();

    /// <inheritdoc />
    public MessageRoleSubscriptionData RoleSubscriptionData => throw new NotImplementedException();

    /// <inheritdoc />
    public PurchaseNotification PurchaseNotification { get; }

    /// <inheritdoc />
    public MessageCallData? CallData { get; }
}