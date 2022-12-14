using System.Threading.Tasks;

// ReSharper disable NotNullMemberIsNotInitialized

namespace Mewdeko.Common.DiscordImplementations;

public class MewdekoUserMessage : IUserMessage
{
    public ulong Id => 0;
    public DateTimeOffset CreatedAt => DateTime.Now;
    public Task DeleteAsync(RequestOptions options = null) => throw new NotImplementedException();

    public Task AddReactionAsync(IEmote emote, RequestOptions options = null) => throw new NotImplementedException();

    public Task RemoveReactionAsync(IEmote emote, IUser user, RequestOptions options = null) => throw new NotImplementedException();

    public Task RemoveReactionAsync(IEmote emote, ulong userId, RequestOptions options = null) => throw new NotImplementedException();

    public Task RemoveAllReactionsAsync(RequestOptions options = null) => throw new NotImplementedException();

    public Task RemoveAllReactionsForEmoteAsync(IEmote emote, RequestOptions options = null) => throw new NotImplementedException();

    public IAsyncEnumerable<IReadOnlyCollection<IUser>> GetReactionUsersAsync(IEmote emoji, int limit, RequestOptions options = null) => throw new NotImplementedException();

    public MessageType Type => MessageType.Default;
    public MessageSource Source => MessageSource.User;
    public bool IsTTS => false;
    public bool IsPinned => false;
    public bool IsSuppressed => false;
    public string Content { get; set; }
    public string CleanContent { get; set; }
    public DateTimeOffset Timestamp => DateTimeOffset.Now;
    public DateTimeOffset? EditedTimestamp => DateTimeOffset.Now;
    public IMessageChannel Channel { get; set; }
    public IUser Author { get; set; }
    public IReadOnlyCollection<IAttachment> Attachments { get; set; }
    public IReadOnlyCollection<IEmbed> Embeds { get; set; }
    public IReadOnlyCollection<ITag> Tags { get; set; }
    public IReadOnlyCollection<ulong> MentionedChannelIds { get; set; }
    public IReadOnlyCollection<ulong> MentionedRoleIds { get; set; }
    public IReadOnlyCollection<ulong> MentionedUserIds { get; set; }
    public bool MentionedEveryone { get; set; }
    public MessageActivity Activity { get; set; }
    public MessageApplication Application { get; set; }
    public MessageReference Reference { get; set; }
    public IReadOnlyDictionary<IEmote, ReactionMetadata> Reactions { get; set; }
    public IReadOnlyCollection<IMessageComponent> Components { get; set; }
    public IReadOnlyCollection<IStickerItem> Stickers { get; set; }
    public MessageFlags? Flags { get; set; }
    public IMessageInteraction Interaction { get; set; }
    public Task ModifyAsync(Action<MessageProperties> func, RequestOptions options = null) => throw new NotImplementedException();

    public Task PinAsync(RequestOptions options = null) => throw new NotImplementedException();

    public Task UnpinAsync(RequestOptions options = null) => throw new NotImplementedException();

    public Task CrosspostAsync(RequestOptions options = null) => throw new NotImplementedException();

    public string Resolve(
        TagHandling userHandling = TagHandling.Name,
        TagHandling channelHandling = TagHandling.Name,
        TagHandling roleHandling = TagHandling.Name,
        TagHandling everyoneHandling = TagHandling.Ignore,
        TagHandling emojiHandling = TagHandling.Name) =>
        throw new NotImplementedException();

    public IUserMessage ReferencedMessage { get; set; }
}