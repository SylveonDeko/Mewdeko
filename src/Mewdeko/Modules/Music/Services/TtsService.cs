using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;
using DataModel;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Music.CustomPlayer;

namespace Mewdeko.Modules.Music.Services;

/// <summary>
///     Listens for messages in TTS-enabled voice channels and reads them aloud.
///     Also handles VC join/leave announcements.
/// </summary>
public class TtsService : INService, IReadyExecutor
{
    private const int MaxMessageLength = 200;
    private const float DuckVolume = 0.15f;
    private const string DefaultJoinFormat = "%user.name% joined the channel";
    private const string DefaultLeaveFormat = "%user.name% left the channel";
    private static readonly TimeSpan CooldownDuration = TimeSpan.FromSeconds(5);

    private readonly IAudioService audioService;
    private readonly IDataCache cache;
    private readonly DiscordShardedClient client;
    private readonly NonBlocking.ConcurrentDictionary<(ulong GuildId, ulong UserId), DateTime> cooldowns = new();
    private readonly IDataConnectionFactory dbFactory;
    private readonly GuildSettingsService guildSettingsService;
    private readonly NonBlocking.ConcurrentDictionary<ulong, ulong> lastSpeaker = new();
    private readonly ILogger<TtsService> logger;
    private readonly NonBlocking.ConcurrentDictionary<ulong, TaskCompletionSource> ttsCompletions = new();
    private readonly NonBlocking.ConcurrentDictionary<ulong, SemaphoreSlim> ttsLocks = new();

    private readonly NonBlocking.ConcurrentDictionary<ulong, ConcurrentQueue<TtsQueueItem>> ttsQueues = new();

    /// <summary>
    ///     Initializes a new instance of <see cref="TtsService" />.
    /// </summary>
    public TtsService(
        IAudioService audioService,
        IDataCache cache,
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        GuildSettingsService guildSettingsService,
        EventHandler eventHandler,
        ILogger<TtsService> logger)
    {
        this.audioService = audioService;
        this.cache = cache;
        this.client = client;
        this.dbFactory = dbFactory;
        this.guildSettingsService = guildSettingsService;
        this.logger = logger;

        eventHandler.Subscribe("MessageReceived", "TtsService", HandleMessage);
        eventHandler.Subscribe("UserVoiceStateUpdated", "TtsService", HandleVoiceStateUpdated);
    }

    /// <summary>
    ///     Called when the bot is ready. Required to ensure this service is eagerly instantiated.
    /// </summary>
    public Task OnReadyAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Signals that a TTS track has finished playing for a guild.
    /// </summary>
    public void SignalTtsCompleted(ulong guildId)
    {
        if (ttsCompletions.TryRemove(guildId, out var tcs))
            tcs.TrySetResult();
    }

    private async Task HandleVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        if (user.IsBot)
            return;

        var joined = before.VoiceChannel is null && after.VoiceChannel is not null;
        var left = before.VoiceChannel is not null && after.VoiceChannel is null;
        var moved = before.VoiceChannel is not null && after.VoiceChannel is not null
                                                    && before.VoiceChannel.Id != after.VoiceChannel.Id;

        if (!joined && !left && !moved)
            return;

        if (joined || moved)
        {
            var vc = after.VoiceChannel;
            var guild = vc.Guild;
            var vcSetting = await GetVcSetting(guild.Id, vc.Id);
            if (vcSetting is not { Enabled: true, AnnounceJoinLeave: true })
                return;

            var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guild.Id);
            if (player is null || player.VoiceChannelId != vc.Id)
                return;

            var format = vcSetting.JoinFormat ?? DefaultJoinFormat;
            var replacer = new ReplacementBuilder()
                .WithUser(user)
                .WithServer(client, guild)
                .WithClient(client)
                .Build();
            var text = replacer.Replace(format);
            var voice = await player.GetEffectiveTtsVoiceAsync(user.Id);
            EnqueueTts(guild.Id, text, voice);
        }

        if (left || moved)
        {
            var vc = before.VoiceChannel;
            var guild = vc.Guild;
            var vcSetting = await GetVcSetting(guild.Id, vc.Id);
            if (vcSetting is not { Enabled: true, AnnounceJoinLeave: true })
                return;

            var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guild.Id);
            if (player is null || player.VoiceChannelId != vc.Id)
                return;

            var format = vcSetting.LeaveFormat ?? DefaultLeaveFormat;
            var replacer = new ReplacementBuilder()
                .WithUser(user)
                .WithServer(client, guild)
                .WithClient(client)
                .Build();
            var text = replacer.Replace(format);
            var voice = await player.GetEffectiveTtsVoiceAsync(user.Id);
            EnqueueTts(guild.Id, text, voice);
        }
    }

    private async Task HandleMessage(SocketMessage msg)
    {
        if (msg is not SocketUserMessage userMsg)
            return;

        if (msg.Author.IsBot || msg.Author.IsWebhook)
            return;

        if (userMsg.Channel is not SocketGuildChannel guildChannel)
            return;

        var guildId = guildChannel.Guild.Id;

        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player is null)
            return;

        var vcSetting = await GetVcSetting(guildId, player.VoiceChannelId);
        if (vcSetting is not { Enabled: true })
            return;

        var isLinkedTextChannel = vcSetting.LinkedTextChannelId.HasValue
                                  && vcSetting.LinkedTextChannelId.Value == guildChannel.Id;
        var isVcTextChat = guildChannel.Id == player.VoiceChannelId;

        if (!isLinkedTextChannel && !isVcTextChat)
            return;

        var settings = await cache.GetMusicPlayerSettings(guildId);

        if (settings?.TtsRoleId is not null)
        {
            var guildUser = msg.Author as IGuildUser;
            if (guildUser is not null && !guildUser.RoleIds.Contains(settings.TtsRoleId.Value)
                                      && !guildUser.GuildPermissions.Administrator)
                return;
        }

        if (await player.IsTtsUserBlockedAsync(msg.Author.Id))
            return;

        var authorAsGuildUser = msg.Author as IGuildUser;
        if (authorAsGuildUser?.VoiceChannel is null || authorAsGuildUser.VoiceChannel.Id != player.VoiceChannelId)
            return;

        var content = userMsg.Content;

        if (!string.IsNullOrWhiteSpace(content))
        {
            var prefix = await guildSettingsService.GetPrefix(guildChannel.Guild);
            if (content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || content.StartsWith('/')
                || content.StartsWith('!'))
                return;
        }

        var cooldownKey = (guildId, msg.Author.Id);
        if (cooldowns.TryGetValue(cooldownKey, out var lastUsed) && DateTime.UtcNow - lastUsed < CooldownDuration)
            return;
        cooldowns[cooldownKey] = DateTime.UtcNow;

        var maxQueue = settings?.TtsMaxQueueSize is > 0 ? settings.TtsMaxQueueSize : 10;
        if (ttsQueues.TryGetValue(guildId, out var existingQueue) && existingQueue.Count >= maxQueue)
            return;

        var parts = new List<string>();

        var ttsReplyContext = settings?.TtsReplyContext ?? true;
        var ttsAttachmentNarration = settings?.TtsAttachmentNarration ?? true;
        var ttsConsecutiveGrouping = settings?.TtsConsecutiveGrouping ?? true;

        if (ttsReplyContext && userMsg.ReferencedMessage is not null)
        {
            var replyAuthor = userMsg.ReferencedMessage.Author as IGuildUser;
            var replyName = replyAuthor?.DisplayName ?? userMsg.ReferencedMessage.Author?.Username ?? "someone";
            parts.Add($"replying to {replyName}");
        }

        if (!string.IsNullOrWhiteSpace(content))
        {
            var sanitized = SanitizeForTts(content);
            if (sanitized.Length > MaxMessageLength)
                sanitized = sanitized[..MaxMessageLength] + "...";
            if (!string.IsNullOrWhiteSpace(sanitized))
                parts.Add(sanitized);
        }

        if (ttsAttachmentNarration && userMsg.Attachments.Count > 0)
        {
            var attachmentDesc = userMsg.Attachments.Count == 1
                ? DescribeAttachment(userMsg.Attachments.First())
                : $"sent {userMsg.Attachments.Count} attachments";
            parts.Add(attachmentDesc);
        }

        if (parts.Count == 0)
            return;

        var displayName = authorAsGuildUser.DisplayName;
        var shouldGroupConsecutive = ttsConsecutiveGrouping;
        var skipName = shouldGroupConsecutive && lastSpeaker.TryGetValue(guildId, out var prev) &&
                       prev == msg.Author.Id;
        lastSpeaker[guildId] = msg.Author.Id;

        var ttsText = skipName
            ? string.Join(". ", parts)
            : $"{displayName} said: {string.Join(". ", parts)}";

        var voice = await player.GetEffectiveTtsVoiceAsync(msg.Author.Id);
        EnqueueTts(guildId, ttsText, voice);
    }

    private void EnqueueTts(ulong guildId, string text, string? voice)
    {
        var queue = ttsQueues.GetOrAdd(guildId, _ => new ConcurrentQueue<TtsQueueItem>());
        queue.Enqueue(new TtsQueueItem(guildId, text, voice));
        _ = ProcessQueueAsync(guildId);
    }

    private async Task ProcessQueueAsync(ulong guildId)
    {
        var semaphore = ttsLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));

        if (!await semaphore.WaitAsync(0))
            return;

        try
        {
            if (!ttsQueues.TryGetValue(guildId, out var queue))
                return;

            var settings = await cache.GetMusicPlayerSettings(guildId);
            var speed = settings?.TtsSpeed is > 0 ? settings.TtsSpeed : 1.0f;
            var ttsVolume = settings?.TtsVolume is > 0 ? settings.TtsVolume : 100;
            var ttsVolumeFloat = ttsVolume / 100f;
            var loadOptions = new TrackLoadOptions
            {
                SearchMode = TrackSearchMode.None, SearchBehavior = StrictSearchBehavior.Passthrough
            };

            while (queue.TryDequeue(out var item))
            {
                try
                {
                    var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
                    if (player is null)
                    {
                        while (queue.TryDequeue(out _)) { }

                        return;
                    }

                    var wasPlaying = player.State == PlayerState.Playing;
                    var currentTrack = player.CurrentTrack;
                    var currentPosition = player.Position?.Position ?? TimeSpan.Zero;
                    var originalVolume = player.Volume;

                    var ttsUri = BuildTtsUri(item.Text, item.Voice, speed);

                    var ttsTrack = await audioService.Tracks.LoadTrackAsync(ttsUri, loadOptions);

                    if (ttsTrack is null)
                    {
                        logger.LogWarning("Failed to load TTS track for guild {GuildId}", guildId);
                        continue;
                    }

                    if (wasPlaying)
                        await player.SetVolumeAsync(originalVolume * DuckVolume);

                    await player.PlayAsync(ttsTrack);
                    await player.SetVolumeAsync(ttsVolumeFloat);

                    var ttsFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    var ttsIdentifier = ttsTrack.Identifier;
                    ttsCompletions[guildId] = ttsFinished;

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    await using var reg = cts.Token.Register(() => ttsFinished.TrySetResult());

                    var pollInterval = TimeSpan.FromMilliseconds(50);
                    while (!ttsFinished.Task.IsCompleted)
                    {
                        await Task.WhenAny(ttsFinished.Task, Task.Delay(pollInterval));

                        if (ttsFinished.Task.IsCompleted)
                            break;

                        var currentPlayer = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
                        if (currentPlayer is null)
                        {
                            ttsCompletions.TryRemove(guildId, out _);
                            return;
                        }

                        if (currentPlayer.State is PlayerState.NotPlaying or PlayerState.Destroyed)
                            break;

                        if (currentPlayer.CurrentTrack?.Identifier != ttsIdentifier)
                            break;
                    }

                    ttsCompletions.TryRemove(guildId, out _);

                    var resumePlayer = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
                    if (resumePlayer is not null)
                    {
                        if (currentTrack is not null && wasPlaying)
                        {
                            await resumePlayer.PlayAsync(currentTrack);
                            if (currentPosition > TimeSpan.Zero)
                                await resumePlayer.SeekAsync(currentPosition);
                        }

                        await resumePlayer.SetVolumeAsync(originalVolume);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing TTS item for guild {GuildId}", guildId);
                    ttsCompletions.TryRemove(guildId, out _);

                    try
                    {
                        var errorPlayer = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
                        if (errorPlayer is not null)
                        {
                            var vol = await errorPlayer.GetVolume();
                            await errorPlayer.SetVolumeAsync(vol / 100f);
                        }
                    }
                    catch { }
                }
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static string BuildTtsUri(string text, string? voice, float speed)
    {
        var uri = $"ftts://{Uri.EscapeDataString(text)}";
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(voice))
            queryParams.Add($"voice={Uri.EscapeDataString(voice)}");
        if (speed is not 1.0f)
            queryParams.Add($"speed={speed:F1}");
        if (queryParams.Count > 0)
            uri += "?" + string.Join("&", queryParams);
        return uri;
    }

    private async Task<TtsVoiceChannelSetting?> GetVcSetting(ulong guildId, ulong voiceChannelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.GetTable<TtsVoiceChannelSetting>()
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.VoiceChannelId == voiceChannelId);
    }

    private static string DescribeAttachment(Attachment attachment)
    {
        var filename = attachment.Filename.ToLowerInvariant();
        if (filename.EndsWith(".png") || filename.EndsWith(".jpg") || filename.EndsWith(".jpeg")
            || filename.EndsWith(".gif") || filename.EndsWith(".webp"))
            return "sent an image";
        if (filename.EndsWith(".mp4") || filename.EndsWith(".webm") || filename.EndsWith(".mov"))
            return "sent a video";
        if (filename.EndsWith(".mp3") || filename.EndsWith(".ogg") || filename.EndsWith(".wav") ||
            filename.EndsWith(".flac"))
            return "sent an audio file";
        return "sent a file";
    }

    private static string SanitizeForTts(string text)
    {
        text = Regex.Replace(text, @"<@!?\d+>", "");
        text = Regex.Replace(text, @"<@&\d+>", "");
        text = Regex.Replace(text, @"<#\d+>", "");
        text = Regex.Replace(text, @"<a?:\w+:\d+>", "");
        text = Regex.Replace(text, @"https?://\S+", "");
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    private sealed record TtsQueueItem(ulong GuildId, string Text, string? Voice);
}