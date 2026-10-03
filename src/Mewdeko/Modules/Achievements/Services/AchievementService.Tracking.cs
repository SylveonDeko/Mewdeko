using System.Threading;
using Discord.Commands;
using Discord.Interactions;
using LinqToDB.Async;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Modules.Xp.Events;
using IResult = Discord.Interactions.IResult;

namespace Mewdeko.Modules.Achievements.Services;

public sealed partial class AchievementService
{
    /// <summary>
    ///     How soon after sending an edit or delete counts as quick.
    /// </summary>
    private static readonly TimeSpan QuickWindow = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Window for the five message burst.
    /// </summary>
    private static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Window for visiting three voice channels.
    /// </summary>
    private static readonly TimeSpan HopWindow = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     How soon after joining leaving voice counts as quick.
    /// </summary>
    private static readonly TimeSpan QuickExitWindow = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<ulong, RecentMessage> recentMessages = new();
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), Queue<DateTime>> messageBursts = new();
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), VoiceTrack> voiceTracks = new();
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId, string Emoji), int> emojiDeltas = new();
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), CounterDelta> counterDeltas = new();
    private readonly ConcurrentDictionary<ulong, string?> userTimezones = new();

    private void SubscribeEvents()
    {
        eventHandler.Subscribe("MessageReceived", EventModuleName, OnMessageReceivedAsync);
        eventHandler.Subscribe("MessageUpdated", EventModuleName, OnMessageUpdatedAsync);
        eventHandler.Subscribe("MessageDeleted", EventModuleName, OnMessageDeletedAsync);
        eventHandler.Subscribe("ReactionAdded", EventModuleName, OnReactionAddedAsync);
        eventHandler.Subscribe("UserVoiceStateUpdated", EventModuleName, OnVoiceStateUpdatedAsync);
        eventHandler.Subscribe("GuildMemberUpdated", EventModuleName, OnGuildMemberUpdatedAsync);
        eventHandler.Subscribe("XpLevelChanged", EventModuleName, OnXpLevelChangedAsync);
        eventHandler.Subscribe("LeftGuild", EventModuleName, OnLeftGuildAsync);
        commandHandler.CommandExecuted += OnTextCommandExecutedAsync;
        interactionService.SlashCommandExecuted += OnSlashCommandExecutedAsync;
    }

    private void UnsubscribeEvents()
    {
        eventHandler.Unsubscribe("MessageReceived", EventModuleName, OnMessageReceivedAsync);
        eventHandler.Unsubscribe("MessageUpdated", EventModuleName, OnMessageUpdatedAsync);
        eventHandler.Unsubscribe("MessageDeleted", EventModuleName, OnMessageDeletedAsync);
        eventHandler.Unsubscribe("ReactionAdded", EventModuleName, OnReactionAddedAsync);
        eventHandler.Unsubscribe("UserVoiceStateUpdated", EventModuleName, OnVoiceStateUpdatedAsync);
        eventHandler.Unsubscribe("GuildMemberUpdated", EventModuleName, OnGuildMemberUpdatedAsync);
        eventHandler.Unsubscribe("XpLevelChanged", EventModuleName, OnXpLevelChangedAsync);
        eventHandler.Unsubscribe("LeftGuild", EventModuleName, OnLeftGuildAsync);
        commandHandler.CommandExecuted -= OnTextCommandExecutedAsync;
        interactionService.SlashCommandExecuted -= OnSlashCommandExecutedAsync;
    }

    /// <summary>
    ///     Whether a member can earn achievements from activity in a channel.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <param name="channelId">The channel, or 0 for activity outside a channel.</param>
    /// <returns>True when the server has achievements on and nothing excludes the member or channel.</returns>
    public bool CanEarn(SocketGuildUser user, ulong channelId = 0)
    {
        if (user.IsBot || !settingsCache.TryGetValue(user.Guild.Id, out var settings) || !settings.Enabled)
            return false;
        if (channelId != 0 && settings.ExcludedChannels.Contains(channelId))
            return false;
        return settings.ExcludedRoles.Count == 0 || !user.Roles.Any(r => settings.ExcludedRoles.Contains(r.Id));
    }

    /// <summary>
    ///     Marks metrics as worth checking again for a member.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="flags">Which metrics changed.</param>
    /// <param name="channelId">Where it happened, for announcements.</param>
    public void MarkDirty(ulong guildId, ulong userId, AchievementDirty flags, ulong channelId = 0)
    {
        if (!IsEnabled(guildId))
            return;
        dirty.AddOrUpdate((guildId, userId), flags, (_, existing) => existing | flags);
        if (channelId != 0)
            lastChannels[(guildId, userId)] = channelId;
    }

    private Task OnMessageReceivedAsync(SocketMessage message)
    {
        if (message is not SocketUserMessage userMessage ||
            message.Author is not SocketGuildUser user ||
            message.Channel is not SocketGuildChannel channel ||
            !CanEarn(user, channel.Id))
            return Task.CompletedTask;

        _ = HandleMessageAsync(userMessage, user, channel);
        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(SocketUserMessage message, SocketGuildUser user, SocketGuildChannel channel)
    {
        try
        {
            var guildId = user.Guild.Id;
            var now = message.Timestamp.UtcDateTime;
            recentMessages[message.Id] = new RecentMessage(guildId, user.Id, channel.Id, now);

            if (messageCounts.IsCounting(guildId))
                BumpTally(guildId, user.Id, channel.Id, now);
            MarkDirty(guildId, user.Id, AchievementDirty.Messages, channel.Id);

            var feats = new List<AchievementFeat>
            {
                AchievementFeat.FirstMessage
            };
            var content = message.Content ?? "";

            if (message.Poll is not null)
                feats.Add(AchievementFeat.Poll);
            if (message.Stickers.Count > 0)
                feats.Add(AchievementFeat.Sticker);
            if (message.Tags.Any(t => t.Type == TagType.Emoji))
                feats.Add(AchievementFeat.CustomEmoji);
            if (message.Attachments.Count > 0)
                feats.Add(AchievementFeat.Attachment);
            if (message.ForwardedMessages.Count > 0)
                feats.Add(AchievementFeat.Forward);
            if (message.MentionedUsers.Any(u => u.Id != user.Id && !u.IsBot))
                feats.Add(AchievementFeat.Mention);
            if (message.MentionedUsers.Any(u => u.Id == user.Id))
                feats.Add(AchievementFeat.SelfMention);
            if (message.ReferencedMessage is { } referenced && referenced.Author.Id == user.Id &&
                message.Type == MessageType.Reply)
                feats.Add(AchievementFeat.SelfReply);
            if (content.Length == 200)
                feats.Add(AchievementFeat.Exact200);
            if (content.Length >= 1000)
                feats.Add(AchievementFeat.WallOfText);
            if (IsNightOwlHour(user.Id, now))
                feats.Add(AchievementFeat.NightOwl);
            if (RecordBurst(guildId, user.Id, now))
                feats.Add(AchievementFeat.TypingStorm);

            await UnlockFeatsAsync(user, feats, channel.Id);

            var catalog = await GetCatalogAsync(guildId);
            if (catalog.Watchers.Count == 0 || content.Length == 0)
                return;

            var matched = catalog.Watchers
                .Where(d => d.Trigger == AchievementTrigger.Keyword &&
                            (d.ChannelId is null || d.ChannelId == channel.Id) &&
                            content.Contains(d.Keyword!, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matched.Count > 0)
                await UnlockForMemberAsync(user, matched, channel.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Achievement message tracking failed in {GuildId}", user.Guild.Id);
        }
    }

    private bool RecordBurst(ulong guildId, ulong userId, DateTime now)
    {
        var queue = messageBursts.GetOrAdd((guildId, userId), _ => new Queue<DateTime>());
        lock (queue)
        {
            queue.Enqueue(now);
            while (queue.Count > 0 && now - queue.Peek() > BurstWindow)
                queue.Dequeue();
            return queue.Count >= 5;
        }
    }

    private bool IsNightOwlHour(ulong userId, DateTime utc)
    {
        if (!userTimezones.TryGetValue(userId, out var zone))
        {
            _ = LoadTimezoneAsync(userId);
            zone = null;
        }

        var local = utc;
        if (!string.IsNullOrWhiteSpace(zone))
        {
            try
            {
                local = TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(zone));
            }
            catch (Exception)
            {
                local = utc;
            }
        }

        return local.Hour == 3;
    }

    private async Task LoadTimezoneAsync(ulong userId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var zone = await db.DiscordUsers
                .Where(x => x.UserId == userId)
                .Select(x => x.BirthdayTimezone)
                .FirstOrDefaultAsync();
            userTimezones[userId] = zone;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not load the timezone for {UserId}", userId);
            userTimezones[userId] = null;
        }
    }

    private Task OnMessageUpdatedAsync(Cacheable<IMessage, ulong> before, SocketMessage after,
        ISocketMessageChannel channel)
    {
        if (after.Author is not SocketGuildUser user || !recentMessages.TryGetValue(after.Id, out var recent))
            return Task.CompletedTask;
        if (after.EditedTimestamp is not { } edited || edited.UtcDateTime - recent.SentAt > QuickWindow)
            return Task.CompletedTask;
        if (before.HasValue && before.Value.Content == after.Content)
            return Task.CompletedTask;
        if (!CanEarn(user, recent.ChannelId))
            return Task.CompletedTask;

        _ = UnlockFeatsAsync(user, [AchievementFeat.QuickEdit], recent.ChannelId);
        return Task.CompletedTask;
    }

    private Task OnMessageDeletedAsync(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
    {
        if (!recentMessages.TryRemove(message.Id, out var recent) || DateTime.UtcNow - recent.SentAt > QuickWindow)
            return Task.CompletedTask;

        var user = client.GetGuild(recent.GuildId)?.GetUser(recent.UserId);
        if (user is null || !CanEarn(user, recent.ChannelId))
            return Task.CompletedTask;

        _ = UnlockFeatsAsync(user, [AchievementFeat.QuickDelete], recent.ChannelId);
        return Task.CompletedTask;
    }

    private Task OnReactionAddedAsync(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        var guildChannel = reaction.Channel as SocketGuildChannel;
        var member = reaction.User.GetValueOrDefault() as SocketGuildUser ?? guildChannel?.Guild.GetUser(reaction.UserId);
        if (member is null || guildChannel is null || !CanEarn(member, guildChannel.Id))
            return Task.CompletedTask;

        _ = HandleReactionAsync(member, guildChannel.Id, message, reaction);
        return Task.CompletedTask;
    }

    private async Task HandleReactionAsync(SocketGuildUser user, ulong channelId, Cacheable<IUserMessage, ulong> message,
        SocketReaction reaction)
    {
        try
        {
            var guildId = user.Guild.Id;
            var emojiKey = EmojiKey(reaction.Emote);
            emojiDeltas.AddOrUpdate((guildId, user.Id, emojiKey), 1, (_, count) => count + 1);
            MarkDirty(guildId, user.Id, AchievementDirty.Reactions, channelId);

            var feats = new List<AchievementFeat>
            {
                AchievementFeat.FirstReaction
            };
            var author = message.HasValue ? message.Value.Author : null;
            if (author is null && reaction.Message.IsSpecified)
                author = reaction.Message.Value.Author;
            if (author is not null)
            {
                if (author.Id == user.Id)
                    feats.Add(AchievementFeat.SelfReact);
                else if (author.IsBot)
                    feats.Add(AchievementFeat.BotReact);
            }

            await UnlockFeatsAsync(user, feats, channelId);

            var catalog = await GetCatalogAsync(guildId);
            var matched = catalog.Watchers
                .Where(d => d.Trigger == AchievementTrigger.Reaction &&
                            (d.ChannelId is null || d.ChannelId == channelId) &&
                            EmojiMatches(d.Keyword!, reaction.Emote))
                .ToList();
            if (matched.Count > 0)
                await UnlockForMemberAsync(user, matched, channelId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Achievement reaction tracking failed in {GuildId}", user.Guild.Id);
        }
    }

    /// <summary>
    ///     A stable key for an emoji: the unicode text, or the custom emoji ID.
    /// </summary>
    /// <param name="emote">The emoji.</param>
    /// <returns>The key.</returns>
    public static string EmojiKey(IEmote emote)
    {
        return emote is Emote custom ? custom.Id.ToString() : emote.Name;
    }

    private static bool EmojiMatches(string stored, IEmote emote)
    {
        var trimmed = stored.Trim();
        if (emote is Emote custom)
        {
            if (Emote.TryParse(trimmed, out var parsed))
                return parsed.Id == custom.Id;
            return trimmed == custom.Id.ToString() ||
                   string.Equals(trimmed.Trim(':'), custom.Name, StringComparison.OrdinalIgnoreCase);
        }

        return trimmed == emote.Name;
    }

    private void SeedVoiceSessions()
    {
        var now = DateTime.UtcNow;
        foreach (var guild in client.Guilds)
        {
            if (!IsEnabled(guild.Id))
                continue;
            foreach (var channel in guild.VoiceChannels)
            foreach (var user in channel.ConnectedUsers)
            {
                if (user.IsBot)
                    continue;
                voiceTracks[(guild.Id, user.Id)] = new VoiceTrack(channel.Id, now)
                {
                    MutedSince = IsMuted(user) ? now : null
                };
            }
        }
    }

    private static bool IsMuted(SocketGuildUser user)
    {
        return user.IsSelfMuted || user.IsMuted || user.IsSelfDeafened || user.IsDeafened;
    }

    private Task OnVoiceStateUpdatedAsync(SocketUser socketUser, SocketVoiceState before, SocketVoiceState after)
    {
        if (socketUser is not SocketGuildUser user || user.IsBot || !IsEnabled(user.Guild.Id))
            return Task.CompletedTask;

        try
        {
            HandleVoice(user, before, after);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Achievement voice tracking failed in {GuildId}", user.Guild.Id);
        }

        return Task.CompletedTask;
    }

    private void HandleVoice(SocketGuildUser user, SocketVoiceState before, SocketVoiceState after)
    {
        var guildId = user.Guild.Id;
        var key = (guildId, user.Id);
        var now = DateTime.UtcNow;
        var feats = new List<AchievementFeat>();
        voiceTracks.TryGetValue(key, out var track);

        if (track?.MutedSince is { } mutedSince && (after.VoiceChannel is null || !IsMuted(user)))
        {
            AddMutedSeconds(guildId, user.Id, (long)(now - mutedSince).TotalSeconds);
            track.MutedSince = null;
        }

        var left = before.VoiceChannel is not null && after.VoiceChannel is null;
        var joined = before.VoiceChannel is null && after.VoiceChannel is not null;
        var moved = before.VoiceChannel is not null && after.VoiceChannel is not null &&
                    before.VoiceChannel.Id != after.VoiceChannel.Id;

        if (left)
        {
            if (track is not null && now - track.JoinedAt <= QuickExitWindow)
                feats.Add(AchievementFeat.QuickExit);
            voiceTracks.TryRemove(key, out _);
            MarkDirty(guildId, user.Id, AchievementDirty.Voice);
        }
        else if (after.VoiceChannel is { } channel)
        {
            if (!CanEarn(user, channel.Id))
                return;

            if (joined || moved)
            {
                if (joined)
                {
                    track = new VoiceTrack(channel.Id, now);
                    voiceTracks[key] = track;
                    counterDeltas.GetOrAdd(key, _ => new CounterDelta()).AddJoin();
                    feats.Add(AchievementFeat.FirstVoice);
                }
                else
                {
                    track ??= new VoiceTrack(channel.Id, now);
                    track.ChannelId = channel.Id;
                    voiceTracks[key] = track;
                }

                if (channel.ConnectedUsers.All(u => u.Id == user.Id || u.IsBot))
                    feats.Add(AchievementFeat.EmptyRoom);
                if (track.RecordHop(channel.Id, now, HopWindow) >= 3)
                    feats.Add(AchievementFeat.ChannelHopper);
                MarkDirty(guildId, user.Id, AchievementDirty.Voice);
            }

            track ??= voiceTracks.GetOrAdd(key, _ => new VoiceTrack(channel.Id, now));
            if (IsMuted(user) && track.MutedSince is null)
                track.MutedSince = now;
        }

        if (feats.Count > 0)
            _ = UnlockFeatsAsync(user, feats, 0);
    }

    private void AddMutedSeconds(ulong guildId, ulong userId, long seconds)
    {
        if (seconds <= 0)
            return;
        counterDeltas.GetOrAdd((guildId, userId), _ => new CounterDelta()).AddMuted(seconds);
        MarkDirty(guildId, userId, AchievementDirty.Voice);
    }

    private Task OnGuildMemberUpdatedAsync(Cacheable<SocketGuildUser, ulong> before, SocketGuildUser after)
    {
        if (after.IsBot || !IsEnabled(after.Guild.Id))
            return Task.CompletedTask;

        var wasBoosting = before.HasValue && before.Value.PremiumSince is not null;
        if (after.PremiumSince is not null && !wasBoosting && CanEarn(after))
            _ = UnlockFeatsAsync(after, [AchievementFeat.Boosted], 0);

        MarkDirty(after.Guild.Id, after.Id, AchievementDirty.Membership);
        return Task.CompletedTask;
    }

    private Task OnXpLevelChangedAsync(XpLevelChangedEventArgs args)
    {
        if (args.IsLevelUp)
            MarkDirty(args.GuildId, args.UserId, AchievementDirty.Xp, args.ChannelId);
        return Task.CompletedTask;
    }

    private Task OnLeftGuildAsync(SocketGuild guild)
    {
        foreach (var key in voiceTracks.Keys.Where(k => k.GuildId == guild.Id))
            voiceTracks.TryRemove(key, out _);
        foreach (var key in unlockCache.Keys.Where(k => k.GuildId == guild.Id))
            unlockCache.TryRemove(key, out _);
        catalogCache.TryRemove(guild.Id, out _);
        return Task.CompletedTask;
    }

    private Task OnTextCommandExecutedAsync(IUserMessage message, CommandInfo command)
    {
        if (message.Author is SocketGuildUser user && message.Channel is SocketGuildChannel channel)
            MarkDirty(user.Guild.Id, user.Id, AchievementDirty.Commands, channel.Id);
        return Task.CompletedTask;
    }

    private Task OnSlashCommandExecutedAsync(SlashCommandInfo command, IInteractionContext context, IResult result)
    {
        if (result.IsSuccess && context.Guild is not null)
            MarkDirty(context.Guild.Id, context.User.Id, AchievementDirty.Commands, context.Channel?.Id ?? 0);
        return Task.CompletedTask;
    }

    private async Task UnlockFeatsAsync(SocketGuildUser user, IReadOnlyCollection<AchievementFeat> feats,
        ulong channelId)
    {
        try
        {
            var catalog = await GetCatalogAsync(user.Guild.Id);
            var state = await GetUnlockStateAsync(user.Guild.Id, user.Id);
            var defs = feats
                .Select(f => catalog.ByKey.GetValueOrDefault(AchievementCatalog.FeatKey(f)))
                .Where(d => d is { Enabled: true } && !state.Has(d.Key))
                .Cast<AchievementDefinition>()
                .ToList();
            if (defs.Count > 0)
                await UnlockForMemberAsync(user, defs, channelId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Achievement feat unlock failed in {GuildId}", user.Guild.Id);
        }
    }

    /// <summary>
    ///     A message the bot saw recently, for quick edits and deletes.
    /// </summary>
    /// <param name="GuildId">The guild.</param>
    /// <param name="UserId">The author.</param>
    /// <param name="ChannelId">The channel.</param>
    /// <param name="SentAt">When it was sent.</param>
    private sealed record RecentMessage(ulong GuildId, ulong UserId, ulong ChannelId, DateTime SentAt);

    /// <summary>
    ///     A member's current voice session.
    /// </summary>
    private sealed class VoiceTrack(ulong channelId, DateTime joinedAt)
    {
        private readonly Queue<(ulong ChannelId, DateTime At)> hops = new();

        /// <summary>
        ///     The channel they are in.
        /// </summary>
        public ulong ChannelId { get; set; } = channelId;

        /// <summary>
        ///     When they joined voice.
        /// </summary>
        public DateTime JoinedAt { get; } = joinedAt;

        /// <summary>
        ///     When they went muted, or null when not muted.
        /// </summary>
        public DateTime? MutedSince { get; set; }

        /// <summary>
        ///     Records entering a channel and counts different channels inside the window.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="at">When.</param>
        /// <param name="window">The window.</param>
        /// <returns>Different channels visited inside the window.</returns>
        public int RecordHop(ulong channel, DateTime at, TimeSpan window)
        {
            lock (hops)
            {
                hops.Enqueue((channel, at));
                while (hops.Count > 0 && at - hops.Peek().At > window)
                    hops.Dequeue();
                return hops.Select(h => h.ChannelId).Distinct().Count();
            }
        }
    }

    /// <summary>
    ///     Voice counters waiting to be written.
    /// </summary>
    private sealed class CounterDelta
    {
        private long joins;
        private long muted;

        /// <summary>
        ///     Counts a voice join.
        /// </summary>
        public void AddJoin()
        {
            Interlocked.Increment(ref joins);
        }

        /// <summary>
        ///     Adds muted seconds.
        /// </summary>
        /// <param name="seconds">The seconds.</param>
        public void AddMuted(long seconds)
        {
            Interlocked.Add(ref muted, seconds);
        }

        /// <summary>
        ///     Takes the pending counts and resets them.
        /// </summary>
        /// <returns>The joins and muted seconds.</returns>
        public (long Joins, long Muted) Take()
        {
            return (Interlocked.Exchange(ref joins, 0), Interlocked.Exchange(ref muted, 0));
        }

        /// <summary>
        ///     Reads the pending counts without resetting them.
        /// </summary>
        /// <returns>The joins and muted seconds.</returns>
        public (long Joins, long Muted) Peek()
        {
            return (Interlocked.Read(ref joins), Interlocked.Read(ref muted));
        }
    }
}
