using System.Text.RegularExpressions;
using DataModel;
using Mewdeko.Modules.Bridge.Common;

namespace Mewdeko.Modules.Bridge.Services;

public partial class BridgeService
{
    private async Task OnMessageReceived(SocketMessage args)
    {
        try
        {
            if (args is not SocketUserMessage message || message.Channel is not SocketTextChannel channel
                                                      || !byChannel.TryGetValue(channel.Id, out var bridge)
                                                      || bridge.Paused || !ShouldRelay(bridge, message)
                                                      || !await PassesOutboundAsync(bridge, message))
                return;

            var built = BuildMessage(message);
            if (built.Content.Length == 0 && built.Attachments.Count == 0 && built.Embeds.Count == 0
                && built.Stickers.Count == 0)
                return;

            await PublishAsync(new BridgeEnvelope
            {
                Type = BridgeEnvelopeType.Message,
                BridgeId = bridge.BridgeId,
                OriginMessageId = message.Id,
                Message = built
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not relay a message to the bridge");
        }
    }

    private async Task OnMessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after,
        ISocketMessageChannel channel)
    {
        try
        {
            if (after is not SocketUserMessage message || channel is not SocketTextChannel textChannel
                                                       || !byChannel.TryGetValue(textChannel.Id, out var bridge)
                                                       || bridge.Paused || message.EditedTimestamp is null
                                                       || !ShouldRelay(bridge, message)
                                                       || !await PassesOutboundAsync(bridge, message))
                return;

            await PublishAsync(new BridgeEnvelope
            {
                Type = BridgeEnvelopeType.Edit,
                BridgeId = bridge.BridgeId,
                OriginMessageId = message.Id,
                Message = BuildMessage(message)
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not relay an edit to the bridge");
        }
    }

    private async Task OnMessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
    {
        try
        {
            foreach (var (id, at) in ownDeletes)
            {
                if (DateTime.UtcNow - at > TimeSpan.FromMinutes(1))
                    ownDeletes.TryRemove(id, out _);
            }

            if (ownDeletes.TryRemove(message.Id, out _) || !byChannel.TryGetValue(channel.Id, out var bridge)
                                                        || bridge.Paused || CannotSend(bridge))
                return;

            await PublishAsync(new BridgeEnvelope
            {
                Type = BridgeEnvelopeType.Delete, BridgeId = bridge.BridgeId, OriginMessageId = message.Id
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not relay a delete to the bridge");
        }
    }

    private Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction) => RelayReactionAsync(BridgeEnvelopeType.ReactionAdd, message, channel, reaction);

    private Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction) => RelayReactionAsync(BridgeEnvelopeType.ReactionRemove, message, channel, reaction);

    private async Task RelayReactionAsync(string type, Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        try
        {
            if (!byChannel.TryGetValue(channel.Id, out var bridge) || bridge.Paused || CannotSend(bridge)
                                                                   || reaction.UserId == client.CurrentUser.Id
                                                                   || reaction.User.IsSpecified
                                                                   && reaction.User.Value.IsBot)
                return;

            var target = await message.GetOrDownloadAsync();
            if (target is null)
                return;

            var users = await target.GetReactionUsersAsync(reaction.Emote, 100).FlattenAsync();
            var count = users.Count(x => !x.IsBot);
            var custom = reaction.Emote as Emote;
            await PublishAsync(new BridgeEnvelope
            {
                Type = type,
                BridgeId = bridge.BridgeId,
                OriginMessageId = target.Id,
                Reaction = new BridgeReaction
                {
                    Emoji = reaction.Emote.Name,
                    CustomEmojiId = custom?.Id,
                    Animated = custom?.Animated ?? false,
                    Count = count
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not relay a reaction to the bridge");
        }
    }

    private static bool CannotSend(ChannelBridge bridge) => bridge.Direction == (int)BridgeDirection.ReceiveOnly;

    private async Task<bool> PassesOutboundAsync(ChannelBridge bridge, SocketUserMessage message)
    {
        var author = message.Author;
        var options = bridge.OptionsOf();
        var member = author as SocketGuildUser;
        var roleIds = member?.Roles.Select(x => x.Id).ToHashSet() ?? [];

        string? reason = null;
        if (options.BlockedUserIds.Contains(author.Id))
            reason = "blocked user";
        else if (options.BlockedRoleIds.Any(roleIds.Contains))
            reason = "blocked role";
        else if (options.AllowedRoleIds.Count > 0 && !options.AllowedRoleIds.Any(roleIds.Contains))
            reason = "missing an allowed role";
        else if (options.MinMemberAgeMinutes > 0 && (member?.JoinedAt is not { } joined
                                                     || DateTimeOffset.UtcNow - joined
                                                     < TimeSpan.FromMinutes(options.MinMemberAgeMinutes)))
            reason = "member too new";
        else if (options.BlockInvites && (message.Content.IsDiscordInvite() || FluxerInviteRegex().IsMatch(message.Content)))
            reason = "invite link";
        else if (options.UseWordFilter && await filterService.ContainsFilteredWord(bridge.GuildId, message.Content))
            reason = "filtered word";

        if (reason is null)
            return true;

        logger.LogDebug("Not relaying message {MessageId} on bridge {BridgeId}: {Reason}", message.Id,
            bridge.BridgeId, reason);
        return false;
    }

    [GeneratedRegex(@"fluxer\.(?:gg|app/invite)/\w+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FluxerInviteRegex();

    private bool ShouldRelay(ChannelBridge bridge, SocketUserMessage message)
    {
        if (CannotSend(bridge) || message.Source == MessageSource.System
            || message.Type is not (MessageType.Default or MessageType.Reply or MessageType.ApplicationCommand))
            return false;

        if (message.Author is IWebhookUser webhook && webhook.WebhookId == WebhookIdOf(bridge.WebhookUrl))
            return false;

        if (message.Author.Id == client.CurrentUser.Id)
            return false;

        return bridge.AllowBots || !(message.Author.IsBot || message.Author.IsWebhook);
    }

    private static BridgeMessage BuildMessage(SocketUserMessage message)
    {
        var built = new BridgeMessage
        {
            AuthorName = NameOf(message.Author),
            AuthorAvatarUrl = (message.Author as IGuildUser)?.GetDisplayAvatarUrl()
                              ?? message.Author.GetAvatarUrl() ?? message.Author.GetDefaultAvatarUrl(),
            AuthorIsBot = message.Author.IsBot || message.Author.IsWebhook,
            Content = message.CleanContent ?? ""
        };

        foreach (var attachment in message.Attachments)
        {
            built.Attachments.Add(new BridgeAttachment
            {
                Url = attachment.Url,
                Filename = attachment.Filename,
                Size = attachment.Size,
                ContentType = attachment.ContentType,
                Spoiler = attachment.IsSpoiler()
            });
        }

        if (built.AuthorIsBot)
        {
            foreach (var embed in message.Embeds.Where(x => x.Type == EmbedType.Rich).Take(10))
                built.Embeds.Add(ToBridgeEmbed(embed));
        }

        foreach (var sticker in message.Stickers)
            built.Stickers.Add(sticker.Name);

        if (message.ReferencedMessage is { } replied)
        {
            built.ReplyTo = new BridgeReply
            {
                OriginMessageId = replied.Id,
                AuthorName = NameOf(replied.Author),
                Snippet = Snippet(replied.CleanContent)
            };
        }
        else if (message.Reference?.MessageId.IsSpecified == true)
        {
            built.ReplyTo = new BridgeReply
            {
                OriginMessageId = message.Reference.MessageId.Value
            };
        }

        return built;
    }

    private static string NameOf(IUser user) =>
        (user as IGuildUser)?.DisplayName ?? user.GlobalName ?? user.Username;

    private static string Snippet(string? text)
    {
        var plain = Regex.Replace(text ?? "", @"\s+", " ").Trim();
        return plain.Length <= 100 ? plain : plain[..97] + "...";
    }

    private static BridgeEmbed ToBridgeEmbed(IEmbed embed) => new()
    {
        Title = embed.Title,
        Description = embed.Description,
        Url = embed.Url,
        Color = embed.Color?.RawValue,
        AuthorName = embed.Author?.Name,
        AuthorIconUrl = embed.Author?.IconUrl,
        Footer = embed.Footer?.Text,
        FooterIconUrl = embed.Footer?.IconUrl,
        ImageUrl = embed.Image?.Url,
        ThumbnailUrl = embed.Thumbnail?.Url,
        Timestamp = embed.Timestamp,
        Fields = embed.Fields.Select(x => new BridgeEmbedField
        {
            Name = x.Name, Value = x.Value, Inline = x.Inline
        }).ToList()
    };
}
