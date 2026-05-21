#nullable enable
using System.Net.Http;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.Common;
using Mewdeko.Modules.Searches.Common.StreamNotifications;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Mewdeko.Modules.Searches.Services.Common;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Searches.Services;

/// <summary>
///     Service responsible for managing and tracking online stream notifications.
/// </summary>
public class StreamNotificationService : IReadyExecutor, INService
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly GuildSettingsService guildSettings;
    private readonly object hashSetLock = new();
    private readonly ILogger<StreamNotificationService> logger;
    private readonly Random rng = new MewdekoRandom();

    private readonly ConcurrentDictionary<StreamDataKey, ConcurrentDictionary<ulong, HashSet<FollowedStream>>>
        shardTrackedStreams = new();

    private readonly NotifChecker streamTracker;
    private readonly GeneratedBotStrings strings;

    private ConcurrentDictionary<StreamDataKey, HashSet<ulong>> trackCounter = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="StreamNotificationService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database service instance.</param>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="strings">The bot string service instance.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="httpFactory">The HTTP client factory.</param>
    /// <param name="bot">The bot instance.</param>
    /// <param name="eventHandler">The event handler for guild events.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="logger2">The logger instance for structured logging.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    public StreamNotificationService(
        IDataConnectionFactory dbFactory,
        DiscordShardedClient client,
        GeneratedBotStrings strings,
        IBotCredentials creds,
        IHttpClientFactory httpFactory,
        Mewdeko bot,
        EventHandler eventHandler, ILogger<StreamNotificationService> logger, ILogger<NotifChecker> logger2,
        GuildSettingsService guildSettings)
    {
        this.dbFactory = dbFactory;
        this.client = client;
        this.strings = strings;
        this.logger = logger;
        this.guildSettings = guildSettings;
        streamTracker = new NotifChecker(httpFactory, creds, creds.RedisKey(), true, logger2);

        // Set up stream notification event handlers
#if DEBUG
        logger.LogInformation("[StreamNotificationService] Setting up stream notification event handlers...");
#endif
        streamTracker.OnStreamsOffline += HandleStreamsOffline;
        streamTracker.OnStreamsOnline += HandleStreamsOnline;

        // Load all followed streams from database BEFORE starting the tracker
        _ = Task.Run(async () =>
        {
#if DEBUG
            logger.LogInformation("[StreamNotificationService] Starting database loading process...");
#endif
            await using var db = await dbFactory.CreateConnectionAsync();

            // Load offline notification servers
            OfflineNotificationServers = await db.GuildConfigs
                .Where(x => x.NotifyStreamOffline)
                .Select(x => x.GuildId)
                .ToListAsync();
#if DEBUG
            logger.LogInformation(
                "[StreamNotificationService] Loaded {OfflineNotificationCount} guilds with offline notifications enabled",
                OfflineNotificationServers.Count);
#endif

            // Load followed streams
            var followedStreams = await db.FollowedStreams.ToListAsync();
#if DEBUG
            logger.LogInformation(
                "[StreamNotificationService] Loaded {TotalStreamCount} followed streams from database",
                followedStreams.Count);
#endif

            // Group streams by type and name using efficient string comparison
            var groupedStreams = followedStreams.GroupBy(x => new
                {
                    x.Type, Name = x.Username?.ToLowerInvariant()
                })
                .ToList();

            foreach (var streamGroup in groupedStreams)
            {
                var key = new StreamDataKey((FType)streamGroup.Key.Type, streamGroup.Key.Name?.ToLowerInvariant());
                var guildMap = new ConcurrentDictionary<ulong, HashSet<FollowedStream>>();

                foreach (var guildGroup in streamGroup.GroupBy(y => y.GuildId))
                {
                    guildMap[guildGroup.Key] = guildGroup.AsEnumerable().ToHashSet();
                }

                shardTrackedStreams[key] = guildMap;
            }

            var streamsByType = followedStreams.GroupBy(x => (FType)x.Type).ToDictionary(x => x.Key, x => x.Count());
            foreach (var kvp in streamsByType)
            {
#if DEBUG
                logger.LogInformation("[StreamNotificationService] Loaded {StreamCount} {Platform} streams", kvp.Value,
                    kvp.Key);
#endif
            }

            // Cache all streams in the tracker
#if DEBUG
            logger.LogInformation("[StreamNotificationService] Adding {StreamCount} streams to tracker cache...",
                followedStreams.Count);
#endif
            foreach (var fs in followedStreams)
            {
                var key = fs.CreateKey();
                await streamTracker.CacheAddData(key, null, false);
#if DEBUG
                logger.LogTrace(
                    "[StreamNotificationService] Added stream to cache: {Platform}/{Username} for guild {GuildId}",
                    key.Type, key.Name, fs.GuildId);
#endif
            }

            // Create counter dictionary for tracking using efficient string comparison
            var counterGroups = followedStreams.GroupBy(x => new
            {
                x.Type, Name = x.Username?.ToLowerInvariant()
            });

            foreach (var group in counterGroups)
            {
                var key = new StreamDataKey((FType)group.Key.Type, group.Key.Name);
                trackCounter[key] = group.Select(fs => fs.GuildId).ToHashSet();
            }
#if DEBUG
            logger.LogInformation(
                "[StreamNotificationService] Created tracking counter for {UniqueStreamCount} unique streams",
                trackCounter.Count);
#endif
#if DEBUG
            logger.LogInformation("[StreamNotificationService] Database loading completed successfully");
#endif

            // Start the stream tracker AFTER all streams are loaded and cached
#if DEBUG
            logger.LogInformation("[StreamNotificationService] Starting stream tracker after database loading...");
#endif
            _ = streamTracker.RunAsync();
        });

        // Register guild events
        eventHandler.Subscribe("JoinedGuild", "StreamNotificationService", ClientOnJoinedGuild);
        eventHandler.Subscribe("LeftGuild", "StreamNotificationService", ClientOnLeftGuild);
    }

    private List<ulong> OfflineNotificationServers { get; set; } = [];

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
            while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
            {
                try
                {
                    var errorLimit = TimeSpan.FromHours(12);
                    var failingStreams = streamTracker.GetFailingStreams(errorLimit, true).ToList();

                    if (failingStreams.Count == 0)
                        continue;

                    var deleteGroups = failingStreams.GroupBy(x => x.Type)
                        .ToDictionary(x => x.Key, x => x.Select(y => y.Name).ToList());

                    foreach (var kvp in deleteGroups)
                    {
                        logger.LogInformation(
                            "Deleting {StreamCount} {Platform} streams because they've been erroring for more than {ErrorLimit}: {RemovedList}",
                            kvp.Value.Count,
                            kvp.Key,
                            errorLimit,
                            string.Join(", ", kvp.Value));

                        await using var db = await dbFactory.CreateConnectionAsync();

                        // Delete streams using batch deletion with LinqToDB
                        await db.FollowedStreams
                            .Where(x => (FType)x.Type == kvp.Key && kvp.Value.Contains(x.Username))
                            .DeleteAsync();

                        // Untrack each deleted stream
                        foreach (var loginToDelete in kvp.Value)
                            await streamTracker.UntrackStreamByKey(new StreamDataKey(kvp.Key, loginToDelete));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error cleaning up FollowedStreams");
                }
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles streams coming online and sends notifications.
    /// </summary>
    /// <param name="onlineStreams">The list of streams that came online.</param>
    private async Task HandleStreamsOnline(List<StreamData> onlineStreams)
    {
        foreach (var stream in onlineStreams)
        {
            var key = stream.CreateKey();
            if (shardTrackedStreams.TryGetValue(key, out var fss))
            {
                // Group by guild to get custom message once per guild
                var guildGroups = fss.SelectMany(x => x.Value).GroupBy(fs => fs.GuildId);

                foreach (var guildGroup in guildGroups)
                {
                    var guildId = guildGroup.Key;
                    var customMessage = await GetCustomStreamMessageAsync(guildId);

                    await guildGroup.Select(async fs =>
                    {
                        var textChannel = client.GetGuild(fs.GuildId)?.GetTextChannel(fs.ChannelId);

                        if (textChannel is null)
                        {
                            logger.LogWarning(
                                "[StreamNotificationService] Could not find channel {ChannelId} in guild {GuildId} for stream {Platform}/{Username}",
                                fs.ChannelId, fs.GuildId, key.Type, key.Name);
                            return;
                        }

                        await SendStreamNotificationAsync(textChannel, stream, fs, customMessage);
                    }).WhenAll().ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    ///     Handles streams going offline and sends notifications.
    /// </summary>
    /// <param name="offlineStreams">The list of streams that went offline.</param>
    private async Task HandleStreamsOffline(List<StreamData> offlineStreams)
    {
        foreach (var stream in offlineStreams)
        {
            var key = stream.CreateKey();

            if (shardTrackedStreams.TryGetValue(key, out var fss))
            {
                // Only send offline notifications to guilds that have them enabled
                var eligibleStreams = fss.SelectMany(x => x.Value)
                    .Where(x => OfflineNotificationServers.Contains(x.GuildId)).ToList();

                // Group by guild to get custom message once per guild
                var guildGroups = eligibleStreams.GroupBy(fs => fs.GuildId);

                foreach (var guildGroup in guildGroups)
                {
                    var guildId = guildGroup.Key;
                    var customMessage = await GetCustomStreamMessageAsync(guildId);

                    await guildGroup.Select(async fs =>
                    {
                        var channel = client.GetGuild(fs.GuildId)?.GetTextChannel(fs.ChannelId);
                        if (channel is null)
                        {
                            logger.LogWarning(
                                "[StreamNotificationService] Could not find channel {ChannelId} in guild {GuildId} for offline stream {Platform}/{Username}",
                                fs.ChannelId, fs.GuildId, key.Type, key.Name);
                            return;
                        }

                        await SendStreamNotificationAsync(channel, stream, fs, customMessage);
                    }).WhenAll().ConfigureAwait(false);
                }
            }
        }
    }

    private async Task ClientOnJoinedGuild(GuildConfig guildConfig)
    {
        var guildId = guildConfig.GuildId;
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get guild config for notification setting
        var gc = await db.GuildConfigs
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (gc is null)
            return;

        if (gc.NotifyStreamOffline)
            OfflineNotificationServers.Add(guildId);

        // Get followed streams for this guild
        var followedStreams = await db.FollowedStreams
            .Where(fs => fs.GuildId == guildId)
            .ToListAsync();

        foreach (var followedStream in followedStreams)
        {
            var key = followedStream.CreateKey();
            var streams = GetLocalGuildStreams(key, guildId);
            lock (hashSetLock)
            {
                streams.Add(followedStream);
            }

            TrackStream(followedStream);
        }
    }

    private async Task ClientOnLeftGuild(SocketGuild guild)
    {
        var guildId = guild.Id;
        await using var db = await dbFactory.CreateConnectionAsync();

        if (OfflineNotificationServers.Contains(guildId))
            OfflineNotificationServers.Remove(guildId);

        // Get followed streams for this guild
        var followedStreams = await db.FollowedStreams
            .Where(fs => fs.GuildId == guildId)
            .ToListAsync();

        foreach (var followedStream in followedStreams)
        {
            var streams = GetLocalGuildStreams(followedStream.CreateKey(), guildId);
            lock (hashSetLock)
            {
                streams.Remove(followedStream);
            }

            await UntrackStream(followedStream);
        }
    }

    /// <summary>
    ///     Clears all followed streams for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The number of streams removed.</returns>
    public async Task<int> ClearAllStreams(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get followed streams for this guild
        var followedStreams = await db.FollowedStreams
            .Where(fs => fs.GuildId == guildId)
            .ToListAsync();

        var removedCount = followedStreams.Count;

        if (removedCount > 0)
        {
            // Remove streams using batch deletion
            await db.FollowedStreams
                .Where(fs => fs.GuildId == guildId)
                .DeleteAsync();

            // Untrack each removed stream
            foreach (var s in followedStreams)
                await UntrackStream(s).ConfigureAwait(false);
        }

        return removedCount;
    }

    /// <summary>
    ///     Unfollows a stream for a guild and removes it from the tracked streams.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="index">The index of the stream to unfollow.</param>
    /// <returns>The unfollowed stream data if successful, otherwise <see langword="null" />.</returns>
    public async Task<FollowedStream?> UnfollowStreamAsync(ulong guildId, int index)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get streams for this guild, ordered by ID
        var fss = await db.FollowedStreams
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Id)
            .ToListAsync();

        // Out of range check
        if (fss.Count <= index)
            return null;

        var fs = fss[index];

        // Delete the stream
        await db.FollowedStreams
            .Where(x => x.Id == fs.Id)
            .DeleteAsync();

        // Remove from local cache
        var key = fs.CreateKey();
        var streams = GetLocalGuildStreams(key, guildId);
        lock (hashSetLock)
        {
            streams.Remove(fs);
        }

        await UntrackStream(fs).ConfigureAwait(false);

        return fs;
    }

    /// <summary>
    ///     Adds a stream to the tracking system.
    /// </summary>
    /// <param name="fs">The followed stream to track.</param>
    private void TrackStream(FollowedStream fs)
    {
        var key = fs.CreateKey();

        if (trackCounter.TryGetValue(key, out _))
        {
            trackCounter[key].Add(fs.GuildId);
        }
        else
        {
            trackCounter[key] = [fs.GuildId];
        }

        _ = streamTracker.CacheAddData(key, null, false);
    }

    /// <summary>
    ///     Removes a stream from the tracking system.
    /// </summary>
    /// <param name="fs">The followed stream to untrack.</param>
    private async Task UntrackStream(FollowedStream fs)
    {
        var key = fs.CreateKey();

        if (!trackCounter.TryGetValue(key, out var set))
        {
            await streamTracker.UntrackStreamByKey(key);
            return;
        }

        set.Remove(fs.GuildId);

        if (set.Count != 0)
            return;

        trackCounter.TryRemove(key, out _);
        // If no other guilds are following this stream, untrack it
        await streamTracker.UntrackStreamByKey(key);
    }

    /// <summary>
    ///     Follows a stream for a guild and adds it to the tracked streams.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="channelId">The ID of the channel.</param>
    /// <param name="url">The URL of the stream.</param>
    /// <returns>The stream data if successful, otherwise <see langword="null" />.</returns>
    public async Task<StreamData?> FollowStream(ulong guildId, ulong channelId, string url)
    {
        var data = await streamTracker.GetStreamDataByUrlAsync(url).ConfigureAwait(false);

        if (data is null)
            return null;

        await using var db = await dbFactory.CreateConnectionAsync();

        // Check stream count limit
        var streamCount = await db.FollowedStreams
            .Where(x => x.GuildId == guildId)
            .CountAsync();

        if (streamCount >= 10)
            return null;

        // Create new stream with guild data
        var fs = new FollowedStream
        {
            Type = (int)data.StreamType, Username = data.UniqueName, ChannelId = channelId, GuildId = guildId
        };

        // Insert the new stream
        await db.InsertAsync(fs);

        // Add to local cache
        var key = data.CreateKey();
        var streams = GetLocalGuildStreams(key, guildId);
        lock (hashSetLock)
        {
            streams.Add(fs);
        }

        TrackStream(fs);
        return data;
    }

    /// <summary>
    ///     Gets the embed for a stream status.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="status">The stream status data.</param>
    /// <returns>An embed builder with the stream status information.</returns>
    public EmbedBuilder GetEmbed(ulong guildId, StreamData status)
    {
        var embed = new EmbedBuilder()
            .WithTitle(status.Name)
            .WithUrl(status.StreamUrl)
            .WithDescription(status.StreamUrl)
            .AddField(efb => efb.WithName(strings.Status(guildId))
                .WithValue(status.IsLive ? "🟢 Online" : "🔴 Offline")
                .WithIsInline(true))
            .AddField(efb => efb.WithName(strings.Viewers(guildId))
                .WithValue(status.IsLive ? status.Viewers.ToString() : "-")
                .WithIsInline(true))
            .WithColor(status.IsLive ? Mewdeko.OkColor : Mewdeko.ErrorColor);

        if (!string.IsNullOrWhiteSpace(status.Title))
            embed.WithAuthor(status.Title);

        if (!string.IsNullOrWhiteSpace(status.Game))
            embed.AddField(strings.Streaming(guildId), status.Game, true);

        if (!string.IsNullOrWhiteSpace(status.AvatarUrl))
            embed.WithThumbnailUrl(status.AvatarUrl);

        if (!string.IsNullOrWhiteSpace(status.Preview))
            embed.WithImageUrl($"{status.Preview}?dv={rng.Next()}");

        return embed;
    }

    /// <summary>
    ///     Gets custom stream notification message for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The custom message or null if not set.</returns>
    public async Task<string?> GetCustomStreamMessageAsync(ulong guildId)
    {
        var guildConfig = await guildSettings.GetGuildConfig(guildId);
        return guildConfig?.StreamMessage;
    }

    /// <summary>
    ///     Creates a replacement builder with stream-specific placeholders.
    /// </summary>
    /// <param name="stream">The stream data.</param>
    /// <param name="followedStream">The followed stream configuration.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <returns>A replacement builder with stream placeholders.</returns>
    private ReplacementBuilder CreateStreamReplacer(StreamData stream, FollowedStream followedStream, ulong guildId)
    {
        var guild = client.GetGuild(guildId);

        // Ensure stream URL has protocol for button compatibility
        var streamUrl = stream.StreamUrl ?? "";
        if (!string.IsNullOrWhiteSpace(streamUrl) && !streamUrl.StartsWith("http://") &&
            !streamUrl.StartsWith("https://"))
        {
            streamUrl = $"https://{streamUrl}";
        }

        return new ReplacementBuilder()
            // Stream-specific data only
            .WithOverride("%stream.name%", () => stream.Name ?? "Unknown")
            .WithOverride("%stream.username%", () => stream.UniqueName ?? "Unknown")
            .WithOverride("%stream.url%", () => streamUrl)
            .WithOverride("%stream.title%", () => stream.Title ?? "No title")
            .WithOverride("%stream.game%", () => stream.Game ?? "No category")
            .WithOverride("%stream.viewers%", () => stream.IsLive ? stream.Viewers.ToString("N0") : "-")
            .WithOverride("%stream.platform%", () => stream.StreamType.ToString())
            .WithOverride("%stream.avatar%", () => stream.AvatarUrl ?? "")
            .WithOverride("%stream.preview%", () => stream.Preview ?? "")
            .WithOverride("%stream.status%", () => stream.IsLive ? "🟢 Online" : "🔴 Offline")
            .WithOverride("%stream.channelid%", () => stream.ChannelId ?? "");
    }

    /// <summary>
    ///     Sends a stream notification using hierarchical message priority.
    /// </summary>
    /// <param name="textChannel">The channel to send to.</param>
    /// <param name="stream">The stream data.</param>
    /// <param name="followedStream">The followed stream configuration.</param>
    /// <param name="guildTemplate">The guild-wide custom template (optional).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SendStreamNotificationAsync(ITextChannel textChannel, StreamData stream,
        FollowedStream followedStream, string? guildTemplate = null)
    {
        try
        {
            var replacer = CreateStreamReplacer(stream, followedStream, followedStream.GuildId).Build();

            // Priority 1: Per-streamer custom message (check both online/offline variants)
            var streamerMessage = GetStreamerCustomMessage(followedStream, stream.IsLive);
            if (!string.IsNullOrWhiteSpace(streamerMessage))
            {
                var processedMessage = replacer.Replace(streamerMessage);

                if (SmartEmbed.TryParse(processedMessage ?? string.Empty, followedStream.GuildId, out var embed, out var plainText,
                        out var components))
                {
                    // Valid JSON - use SmartEmbed
                    await textChannel.SendMessageAsync(plainText, embeds: embed, components: components?.Build());
                    return;
                }
                else
                {
                    // Not valid JSON - treat as text above default embed
                    await textChannel.EmbedAsync(GetEmbed(followedStream.GuildId, stream), processedMessage);
                    return;
                }
            }

            // Priority 2: Guild-wide template
            if (!string.IsNullOrWhiteSpace(guildTemplate))
            {
                var processedTemplate = replacer.Replace(guildTemplate);

                if (SmartEmbed.TryParse(processedTemplate ?? string.Empty, followedStream.GuildId, out var embed, out var plainText,
                        out var components))
                {
                    await textChannel.SendMessageAsync(plainText, embeds: embed, components: components?.Build());
                    return;
                }
                else
                {
                    // Fall back to simple message
                    await textChannel.SendMessageAsync(processedTemplate);
                    return;
                }
            }

            // Priority 3: Default embed (existing system)
            await textChannel.EmbedAsync(GetEmbed(followedStream.GuildId, stream));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending stream notification for {Platform}/{Username} in guild {GuildId}",
                stream.StreamType, stream.UniqueName, followedStream.GuildId);
        }
    }

    /// <summary>
    ///     Gets the appropriate custom message for a streamer based on online/offline status.
    /// </summary>
    /// <param name="followedStream">The followed stream configuration.</param>
    /// <param name="isOnline">Whether the stream is online.</param>
    /// <returns>The custom message or null if not set.</returns>
    private string? GetStreamerCustomMessage(FollowedStream followedStream, bool isOnline)
    {
        switch (isOnline)
        {
            // Check for status-specific messages first
            case true when !string.IsNullOrWhiteSpace(followedStream.OnlineMessage):
                return followedStream.OnlineMessage;
            case false when !string.IsNullOrWhiteSpace(followedStream.OfflineMessage):
                return followedStream.OfflineMessage;
            default:
                // If no status-specific message, return null (use guild template or default embed)
                return null;
        }
    }

    /// <summary>
    ///     Toggles the notification for offline streams for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns><see langword="true" /> if notifications are enabled, <see langword="false" /> otherwise.</returns>
    public async Task<bool> ToggleStreamOffline(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get guild config
        var gc = await db.GuildConfigs
            .FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (gc == null)
            return false;

        // Toggle setting
        gc.NotifyStreamOffline = !gc.NotifyStreamOffline;

        // Update database
        await db.UpdateAsync(gc);

        // Update local list
        if (gc.NotifyStreamOffline)
            OfflineNotificationServers.Add(guildId);
        else
            OfflineNotificationServers.Remove(guildId);

        return gc.NotifyStreamOffline;
    }

    /// <summary>
    ///     Retrieves stream data for a given URL.
    /// </summary>
    /// <param name="url">The URL of the stream.</param>
    /// <returns>The stream data if available, otherwise <see langword="null" />.</returns>
    public Task<StreamData?> GetStreamDataAsync(string url)
    {
        return streamTracker.GetStreamDataByUrlAsync(url);
    }

    private HashSet<FollowedStream> GetLocalGuildStreams(in StreamDataKey key, ulong guildId)
    {
        var guildMap =
            shardTrackedStreams.GetOrAdd(key, _ => new ConcurrentDictionary<ulong, HashSet<FollowedStream>>());
        return guildMap.GetOrAdd(guildId, _ => []);
    }

    /// <summary>
    ///     Sets a custom message for a followed stream.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="index">The index of the stream to set the message for.</param>
    /// <param name="message">The custom message to set.</param>
    /// <returns>A tuple containing whether the operation was successful and the updated stream.</returns>
    public async Task<(bool, FollowedStream)> SetStreamMessage(
        ulong guildId,
        int index,
        string message)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get streams for this guild, ordered by ID
        var fss = await db.FollowedStreams
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Id)
            .ToListAsync();

        if (fss.Count <= index)
        {
            return (false, null)!;
        }

        var fs = fss[index];
        fs.OnlineMessage = message;

        // Update database
        await db.UpdateAsync(fs);

        // Update local cache
        var streams = GetLocalGuildStreams(fs.CreateKey(), guildId);

        // Message doesn't participate in equality checking
        // Removing and adding = update
        lock (hashSetLock)
        {
            streams.Remove(fs);
            streams.Add(fs);
        }

        return (true, fs);
    }

    /// <summary>
    ///     Sets the offline message for a specific stream.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="index">The stream index.</param>
    /// <param name="message">The offline message.</param>
    /// <returns>Whether the operation succeeded and the stream data.</returns>
    public async Task<(bool, FollowedStream)> SetStreamOfflineMessage(
        ulong guildId,
        int index,
        string message)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var fss = await db.FollowedStreams
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Id)
            .ToListAsync();

        if (fss.Count <= index)
        {
            return (false, null)!;
        }

        var fs = fss[index];
        fs.OfflineMessage = message;

        await db.UpdateAsync(fs);

        // Update local cache
        var streams = GetLocalGuildStreams(fs.CreateKey(), guildId);
        lock (hashSetLock)
        {
            streams.Remove(fs);
            streams.Add(fs);
        }

        return (true, fs);
    }

    /// <summary>
    ///     Sets the custom stream notification message for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="message">The custom message template. Use null or empty to disable custom messages.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetCustomStreamMessageAsync(ulong guildId, string? message)
    {
        var guildConfig = await guildSettings.GetGuildConfig(guildId);
        if (guildConfig == null) return;

        guildConfig.StreamMessage = string.IsNullOrWhiteSpace(message) ? null : message;
        await guildSettings.UpdateGuildConfig(guildId, guildConfig);
    }

    /// <summary>
    ///     Gets available stream placeholder information for help text.
    /// </summary>
    /// <returns>A dictionary of placeholder categories and their placeholders.</returns>
    public static Dictionary<string, List<(string Placeholder, string Description)>> GetStreamPlaceholders()
    {
        return new Dictionary<string, List<(string, string)>>
        {
            ["Stream Information"] =
            [
                ("%stream.name%", "Display name of the streamer"),
                ("%stream.username%", "Login name/username of the streamer"),
                ("%stream.url%", "Direct URL to the stream"),
                ("%stream.title%", "Current stream title"),
                ("%stream.game%", "Game/category being streamed"),
                ("%stream.viewers%", "Current viewer count (- if offline)"),
                ("%stream.platform%", "Platform name (Twitch, YouTube, etc.)"),
                ("%stream.avatar%", "URL to streamer's avatar"),
                ("%stream.preview%", "URL to stream preview/thumbnail"),
                ("%stream.status%", "🟢 Online or 🔴 Offline"),
                ("%stream.channelid%", "Platform-specific channel ID")
            ]
        };
    }
}