using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Discord.Net;
using Mewdeko.Modules.Bridge.Common;
using StackExchange.Redis;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Bridge.Services;

public partial class BridgeService
{
    private async Task EnsureGroupAsync()
    {
        try
        {
            await cache.Redis.GetDatabase().StreamCreateConsumerGroupAsync(
                BridgeProtocol.InboundStream(ThisPlatform), BridgeProtocol.ConsumerGroup, "0-0");
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
        }
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        var stream = BridgeProtocol.InboundStream(ThisPlatform);
        try
        {
            await ClaimPendingAsync(stream);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not claim pending bridge entries");
        }

        while (!token.IsCancellationRequested)
        {
            try
            {
                var entries = await cache.Redis.GetDatabase().StreamReadGroupAsync(stream,
                    BridgeProtocol.ConsumerGroup, consumer, ">", 50);
                if (entries.Length == 0)
                {
                    await wake.WaitAsync(TimeSpan.FromSeconds(1), token);
                    continue;
                }

                foreach (var entry in entries)
                    Dispatch(entry);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Bridge reader failed");
                try
                {
                    await EnsureGroupAsync();
                    await Task.Delay(TimeSpan.FromSeconds(2), token);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    private async Task ClaimPendingAsync(string stream)
    {
        var db = cache.Redis.GetDatabase();
        RedisValue start = "0-0";
        do
        {
            var result = await db.StreamAutoClaimAsync(stream, BridgeProtocol.ConsumerGroup, consumer,
                (long)MinClaimIdle.TotalMilliseconds, start, 100);
            foreach (var entry in result.ClaimedEntries)
                Dispatch(entry);
            start = result.NextStartId;
        } while (start != "0-0");
    }

    private void Dispatch(StreamEntry entry)
    {
        var envelope = entry.Values.Length == 0
            ? null
            : BridgeProtocol.Deserialize(entry[BridgeProtocol.StreamField].ToString());

        if (envelope is null || envelope.Origin == ThisPlatform
                             || DateTimeOffset.UtcNow - envelope.SentAt > BridgeProtocol.MaxEnvelopeAge
                             || envelope.Type != BridgeEnvelopeType.LinkAccepted
                             && !BridgeEnvelopeType.IsPairLevel(envelope.Type)
                             && !byId.ContainsKey(envelope.BridgeId))
        {
            _ = AckAsync(entry.Id);
            return;
        }

        var laneKey = BridgeEnvelopeType.IsPairLevel(envelope.Type) ? envelope.PairId ?? Guid.Empty : envelope.BridgeId;
        var lane = lanes.GetOrAdd(laneKey, key =>
        {
            var created = new Lane();
            _ = Task.Run(() => RunLaneAsync(created));
            return created;
        });

        var written = lane.Queue.Writer.TryWrite(async () =>
        {
            try
            {
                if (envelope.Type == BridgeEnvelopeType.Message)
                    await ThrottleAsync(lane);

                await HandleAsync(envelope);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not handle a {Type} bridge envelope", envelope.Type);
            }
            finally
            {
                await AckAsync(entry.Id);
            }
        });

        if (!written)
        {
            logger.LogWarning("Dropping a {Type} envelope for bridge {BridgeId}, its queue is full", envelope.Type,
                envelope.BridgeId);
            _ = AckAsync(entry.Id);
        }
    }

    private async Task RunLaneAsync(Lane lane)
    {
        try
        {
            await foreach (var work in lane.Queue.Reader.ReadAllAsync())
                await work();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "A bridge worker stopped");
        }
    }

    private static async Task ThrottleAsync(Lane lane)
    {
        while (true)
        {
            var now = DateTime.UtcNow;
            while (lane.Stamps.Count > 0 && now - lane.Stamps.Peek() >= BurstWindow)
                lane.Stamps.Dequeue();

            if (lane.Stamps.Count < BurstMessages)
            {
                lane.Stamps.Enqueue(now);
                return;
            }

            await Task.Delay(BurstWindow - (now - lane.Stamps.Peek()) + TimeSpan.FromMilliseconds(10));
        }
    }

    private async Task AckAsync(RedisValue id)
    {
        try
        {
            await cache.Redis.GetDatabase().StreamAcknowledgeAsync(BridgeProtocol.InboundStream(ThisPlatform),
                BridgeProtocol.ConsumerGroup, id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not acknowledge a bridge entry");
        }
    }

    private async Task HandleAsync(BridgeEnvelope envelope)
    {
        if (envelope.Type == BridgeEnvelopeType.LinkAccepted)
        {
            await HandleLinkAcceptedAsync(envelope);
            return;
        }

        if (BridgeEnvelopeType.IsPairLevel(envelope.Type))
        {
            await Pairs.HandleAsync(envelope);
            return;
        }

        if (!byId.TryGetValue(envelope.BridgeId, out var bridge))
            return;

        switch (envelope.Type)
        {
            case BridgeEnvelopeType.Unlinked:
                await HandleUnlinkedAsync(bridge);
                return;
            case BridgeEnvelopeType.PauseChanged when envelope.Paused is { } paused:
                await HandlePauseChangedAsync(bridge, paused);
                return;
            case BridgeEnvelopeType.DirectionChanged when envelope.Direction is { } direction:
                await HandleDirectionChangedAsync(bridge, direction);
                return;
        }

        if (bridge.Paused || bridge.Direction == (int)BridgeDirection.SendOnly)
            return;

        try
        {
            switch (envelope.Type)
            {
                case BridgeEnvelopeType.Message when envelope.Message is not null:
                    await HandleMessageAsync(bridge, envelope);
                    break;
                case BridgeEnvelopeType.Edit when envelope.Message is not null:
                    await HandleEditAsync(bridge, envelope);
                    break;
                case BridgeEnvelopeType.Delete:
                    await HandleDeleteAsync(bridge, envelope);
                    break;
                case BridgeEnvelopeType.ReactionAdd or BridgeEnvelopeType.ReactionRemove
                    when envelope.Reaction is not null:
                    await HandleReactionAsync(bridge, envelope);
                    break;
            }
        }
        catch (HttpException ex) when (ex.HttpCode == System.Net.HttpStatusCode.Forbidden
                                       || ex.DiscordCode == DiscordErrorCode.MissingPermissions)
        {
            await PauseForPermissionsAsync(bridge);
        }
    }

    private async Task HandleLinkAcceptedAsync(BridgeEnvelope envelope)
    {
        if (envelope.Link is not { } link || link.Request.Platform != ThisPlatform)
            return;

        var request = link.Request;
        var channel = client.GetGuild(request.GuildId)?.GetTextChannel(request.ChannelId);
        string? url = null;
        var stillWanted = envelope.ProposalId is not { } proposalId || await Pairs.TakeOutgoingProposalAsync(proposalId);
        if (stillWanted && channel is not null && !byChannel.ContainsKey(channel.Id)
            && channel.Guild.CurrentUser.GetPermissions(channel).ManageWebhooks)
            url = await EnsureWebhookAsync(channel, null);

        if (channel is null || url is null)
        {
            await PublishAsync(new BridgeEnvelope
            {
                Type = BridgeEnvelopeType.Unlinked, BridgeId = envelope.BridgeId
            });
            return;
        }

        await InsertAsync(NewBridge(envelope.BridgeId, request.GuildId, request.ChannelId, url,
            request.CreatedByUserId, link.GuildId, link.GuildName, link.ChannelId, link.ChannelName,
            envelope.PairId));

        await channel.SendConfirmAsync(strings.BridgeLinked(channel.Guild.Id, link.ChannelName, link.GuildName,
            BridgeSettings.PlatformLabel(BridgeProtocol.Fluxer)));
    }

    private async Task HandleUnlinkedAsync(DataModel.ChannelBridge bridge)
    {
        if (client.GetGuild(bridge.GuildId)?.GetTextChannel(bridge.ChannelId) is { } channel)
        {
            try
            {
                await channel.SendConfirmAsync(strings.BridgeNoticeUnlinked(bridge.GuildId));
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not post the bridge removal notice");
            }
        }

        await DropAsync(bridge);
    }

    private async Task HandlePauseChangedAsync(DataModel.ChannelBridge bridge, bool paused)
    {
        if (bridge.Paused == paused)
            return;

        bridge.Paused = paused;
        await UpdateAsync(bridge);
        if (client.GetGuild(bridge.GuildId)?.GetTextChannel(bridge.ChannelId) is not { } channel)
            return;

        try
        {
            await channel.SendConfirmAsync(paused
                ? strings.BridgeNoticePausedByRemote(bridge.GuildId)
                : strings.BridgeNoticeResumedByRemote(bridge.GuildId));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not post the bridge pause notice");
        }
    }

    private async Task HandleDirectionChangedAsync(DataModel.ChannelBridge bridge, BridgeDirection direction)
    {
        var inverse = (int)direction.Inverse();
        if (bridge.Direction == inverse)
            return;

        bridge.Direction = inverse;
        await UpdateAsync(bridge);
    }

    private async Task<ulong?> CopyOfAsync(DataModel.ChannelBridge bridge, string platform, ulong messageId)
    {
        var value = await cache.Redis.GetDatabase()
            .StringGetAsync(BridgeProtocol.MessageMapKey(bridge.BridgeId, platform, messageId));
        return value.HasValue && ulong.TryParse(value.ToString(), out var id) ? id : null;
    }

    private async Task HandleMessageAsync(DataModel.ChannelBridge bridge, BridgeEnvelope envelope)
    {
        var message = envelope.Message!;
        var redis = cache.Redis.GetDatabase();
        if (await CopyOfAsync(bridge, envelope.Origin, envelope.OriginMessageId) is not null)
            return;

        var channel = client.GetGuild(bridge.GuildId)?.GetTextChannel(bridge.ChannelId);
        if (channel is null)
            return;

        var content = new StringBuilder();
        if (message.ReplyTo is { } reply)
        {
            var quote = reply.AuthorName.Length == 0 && reply.Snippet.Length == 0
                ? ""
                : $"> **{Format.Sanitize(reply.AuthorName)}**: {Format.Sanitize(reply.Snippet)}";
            if (await CopyOfAsync(bridge, envelope.Origin, reply.OriginMessageId) is { } copy)
            {
                var link = $"https://discord.com/channels/{bridge.GuildId}/{bridge.ChannelId}/{copy}";
                quote = quote.Length == 0 ? $"> [jump]({link})" : $"{quote} [jump]({link})";
            }

            if (quote.Length > 0)
                content.AppendLine(quote);
        }

        content.Append(message.Content);
        foreach (var sticker in message.Stickers)
            content.Append("\n[sticker: ").Append(sticker).Append(']');

        var files = new List<FileAttachment>();
        var total = 0L;
        var linked = new List<string>();
        foreach (var attachment in message.Attachments.Take(10))
        {
            if (attachment.Size <= MaxFileBytes && total + attachment.Size <= MaxTotalFileBytes
                                                 && await DownloadAsync(attachment) is { } stream)
            {
                total += attachment.Size;
                files.Add(new FileAttachment(stream, SafeFileName(attachment.Filename),
                    isSpoiler: attachment.Spoiler));
            }
            else
            {
                linked.Add(attachment.Spoiler ? $"||{attachment.Url}||" : attachment.Url);
            }
        }

        foreach (var url in linked)
            content.Append('\n').Append(url);

        var text = content.ToString().Trim();
        if (text.Length > MaxContentLength)
            text = text[..(MaxContentLength - 3)] + "...";

        var embeds = message.Embeds.Take(10).Select(ToEmbed).OfType<Embed>().ToList();
        if (text.Length == 0 && files.Count == 0 && embeds.Count == 0)
            return;

        var username = SanitizeName(BridgeSettings.FormatName(bridge.NameFormat, message.AuthorName,
            envelope.Origin, bridge.RemoteGuildName));
        var avatar = IsHttpUrl(message.AuthorAvatarUrl) ? message.AuthorAvatarUrl : null;
        ulong copyId;
        try
        {
            copyId = await WithWebhookAsync(bridge, hook => files.Count > 0
                ? hook.SendFilesAsync(files, text, embeds: embeds, username: username, avatarUrl: avatar,
                    allowedMentions: AllowedMentions.None)
                : hook.SendMessageAsync(text, embeds: embeds, username: username, avatarUrl: avatar,
                    allowedMentions: AllowedMentions.None));
        }
        finally
        {
            foreach (var file in files)
                file.Stream.Dispose();
        }

        await redis.StringSetAsync(BridgeProtocol.MessageMapKey(bridge.BridgeId, envelope.Origin,
            envelope.OriginMessageId), copyId.ToString(), BridgeProtocol.MessageMapLifetime);
        await redis.StringSetAsync(BridgeProtocol.MessageMapKey(bridge.BridgeId, ThisPlatform, copyId),
            envelope.OriginMessageId.ToString(), BridgeProtocol.MessageMapLifetime);
    }

    private async Task HandleEditAsync(DataModel.ChannelBridge bridge, BridgeEnvelope envelope)
    {
        if (await CopyOfAsync(bridge, envelope.Origin, envelope.OriginMessageId) is not { } copyId)
            return;

        var message = envelope.Message!;
        var content = new StringBuilder();
        if (message.ReplyTo is { } reply && reply.Snippet.Length > 0)
            content.AppendLine($"> **{Format.Sanitize(reply.AuthorName)}**: {Format.Sanitize(reply.Snippet)}");

        content.Append(message.Content);
        foreach (var sticker in message.Stickers)
            content.Append("\n[sticker: ").Append(sticker).Append(']');

        var text = content.ToString().Trim();
        if (text.Length > MaxContentLength)
            text = text[..(MaxContentLength - 3)] + "...";

        var embeds = message.Embeds.Take(10).Select(ToEmbed).OfType<Embed>().ToArray();
        await WithWebhookAsync(bridge, async hook =>
        {
            await hook.ModifyMessageAsync(copyId, properties =>
            {
                properties.Content = text;
                properties.Embeds = embeds;
                properties.AllowedMentions = AllowedMentions.None;
            });
            return true;
        });
    }

    private async Task HandleDeleteAsync(DataModel.ChannelBridge bridge, BridgeEnvelope envelope)
    {
        if (await CopyOfAsync(bridge, envelope.Origin, envelope.OriginMessageId) is not { } copyId)
            return;

        ownDeletes[copyId] = DateTime.UtcNow;
        try
        {
            await WithWebhookAsync(bridge, async hook =>
            {
                await hook.DeleteMessageAsync(copyId);
                return true;
            });
        }
        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.UnknownMessage)
        {
        }

        var redis = cache.Redis.GetDatabase();
        await redis.KeyDeleteAsync([
            BridgeProtocol.MessageMapKey(bridge.BridgeId, envelope.Origin, envelope.OriginMessageId),
            BridgeProtocol.MessageMapKey(bridge.BridgeId, ThisPlatform, copyId)
        ]);
    }

    private async Task HandleReactionAsync(DataModel.ChannelBridge bridge, BridgeEnvelope envelope)
    {
        var reaction = envelope.Reaction!;
        if (await CopyOfAsync(bridge, envelope.Origin, envelope.OriginMessageId) is not { } copyId)
            return;

        var channel = client.GetGuild(bridge.GuildId)?.GetTextChannel(bridge.ChannelId);
        if (channel is null || await channel.GetMessageAsync(copyId) is not IUserMessage target)
            return;

        IEmote? emote;
        if (reaction.CustomEmojiId is { } emojiId)
        {
            emote = client.Guilds.SelectMany(x => x.Emotes).FirstOrDefault(x => x.Id == emojiId);
        }
        else
        {
            emote = new Emoji(reaction.Emoji);
        }

        if (emote is null)
            return;

        var mine = target.Reactions.TryGetValue(emote, out var metadata) && metadata.IsMe;
        try
        {
            if (reaction.Count > 0 && !mine)
                await target.AddReactionAsync(emote);
            else if (reaction.Count == 0 && mine)
                await target.RemoveReactionAsync(emote, client.CurrentUser);
        }
        catch (HttpException ex)
        {
            logger.LogDebug(ex, "Could not mirror a reaction on message {MessageId}", copyId);
        }
    }

    private static async Task<Stream?> DownloadAsync(BridgeAttachment attachment)
    {
        if (!IsHttpUrl(attachment.Url))
            return null;

        try
        {
            await using var source = await Http.GetStreamAsync(attachment.Url);
            var buffer = new MemoryStream();
            await source.CopyToAsync(buffer);
            if (buffer.Length > MaxFileBytes)
            {
                await buffer.DisposeAsync();
                return null;
            }

            buffer.Position = 0;
            return buffer;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private static string SafeFileName(string name)
    {
        var cleaned = Path.GetFileName(name.Replace('\\', '/'));
        return string.IsNullOrWhiteSpace(cleaned) ? "file" : cleaned;
    }

    private static string SanitizeName(string? name)
    {
        var cleaned = ReservedNames.Replace(name ?? "", "");
        cleaned = new string(cleaned.Where(x => !char.IsControl(x)).ToArray());
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        if (cleaned.Length > 80)
            cleaned = cleaned[..80].Trim();

        return cleaned.Length == 0 ? "Member" : cleaned;
    }

    private static Embed? ToEmbed(BridgeEmbed source)
    {
        try
        {
            var builder = new EmbedBuilder();
            if (!string.IsNullOrWhiteSpace(source.Title))
                builder.WithTitle(Trim(source.Title, 256));
            if (!string.IsNullOrWhiteSpace(source.Description))
                builder.WithDescription(Trim(source.Description, 4096));
            if (IsHttpUrl(source.Url))
                builder.WithUrl(source.Url);
            if (source.Color is { } color)
                builder.WithColor(new Color(color));
            if (!string.IsNullOrWhiteSpace(source.AuthorName))
                builder.WithAuthor(Trim(source.AuthorName, 256),
                    IsHttpUrl(source.AuthorIconUrl) ? source.AuthorIconUrl : null);
            if (!string.IsNullOrWhiteSpace(source.Footer))
                builder.WithFooter(Trim(source.Footer, 2048),
                    IsHttpUrl(source.FooterIconUrl) ? source.FooterIconUrl : null);
            if (IsHttpUrl(source.ImageUrl))
                builder.WithImageUrl(source.ImageUrl);
            if (IsHttpUrl(source.ThumbnailUrl))
                builder.WithThumbnailUrl(source.ThumbnailUrl);
            if (source.Timestamp is { } timestamp)
                builder.WithTimestamp(timestamp);

            foreach (var field in source.Fields.Take(25))
            {
                if (!string.IsNullOrWhiteSpace(field.Name) && !string.IsNullOrWhiteSpace(field.Value))
                    builder.AddField(Trim(field.Name, 256), Trim(field.Value, 1024), field.Inline);
            }

            return builder.Title is null && builder.Description is null && builder.Fields.Count == 0
                   && builder.ImageUrl is null && builder.ThumbnailUrl is null
                ? null
                : builder.Build();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Trim(string text, int max) => text.Length <= max ? text : text[..(max - 3)] + "...";
}
