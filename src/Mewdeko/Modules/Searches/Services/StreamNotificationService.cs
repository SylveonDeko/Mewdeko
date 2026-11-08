#nullable enable
using System.Net.Http;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.Common;
using Mewdeko.Database.DbContextStuff;
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
    private readonly GuildOwnershipService ownership;
    private readonly object hashSetLock = new();
    private readonly ILogger<StreamNotificationService> logger;
    private readonly Random rng = new MewdekoRandom();

    private readonly ConcurrentDictionary<StreamDataKey, ConcurrentDictionary<ulong, HashSet<FollowedStream>>>
        shardTrackedStreams = new();

    private static readonly TimeSpan LiveEditInterval = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<int, LiveMessageState> liveMessages = new();
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
        GuildSettingsService guildSettings, GuildOwnershipService ownership)
    {
        this.ownership = ownership;
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
        streamTracker.OnStreamsUpdated += HandleStreamsUpdated;
        streamTracker.OnVideosPosted += HandleVideosPosted;
        streamTracker.OnStreamsFirstChecked += HandleStreamsFirstChecked;

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
            var allFollowedStreams = await db.FollowedStreams.ToListAsync();
            var followedStreams = allFollowedStreams.Where(x => ownership.IsMine(x.GuildId)).ToList();
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
                streamTracker.RegisterSource((FType)fs.Type, key.Name, fs.SourceUrl);
                await streamTracker.CacheAddData(key, null, false);
#if DEBUG
                logger.LogTrace(
                    "[StreamNotificationService] Added stream to cache: {Platform}/{Username} for guild {GuildId}",
                    key.Type, key.Name, fs.GuildId);
#endif
            }

            await LoadTrackedMessagesAsync(db, allFollowedStreams);

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

                    if (await IsSupersededByTwitchIntegrationAsync(guildId, key))
                        continue;

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

                        if (fs.UpdateLiveMessage && liveMessages.ContainsKey(fs.Id))
                        {
                            await EditLiveMessageAsync(fs, stream, customMessage, true);
                            return;
                        }

                        await SendStreamNotificationAsync(textChannel, stream, fs, customMessage);
                    }).WhenAll().ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    ///     Handles streams that are still live by refreshing their live messages.
    /// </summary>
    /// <param name="liveStreams">The list of streams that were live on this check and the previous one.</param>
    private async Task HandleStreamsUpdated(List<StreamData> liveStreams)
    {
        foreach (var stream in liveStreams)
        {
            if (!shardTrackedStreams.TryGetValue(stream.CreateKey(), out var fss))
                continue;

            var tracked = fss.SelectMany(x => x.Value)
                .Where(x => x.UpdateLiveMessage && liveMessages.ContainsKey(x.Id))
                .ToList();

            foreach (var guildGroup in tracked.GroupBy(fs => fs.GuildId))
            {
                var customMessage = await GetCustomStreamMessageAsync(guildGroup.Key);
                foreach (var fs in guildGroup)
                    await EditLiveMessageAsync(fs, stream, customMessage, false);
            }
        }
    }

    /// <summary>
    ///     Handles streams checked for the first time, resuming or ending live messages that survived a restart.
    /// </summary>
    /// <param name="streams">The list of streams checked for the first time.</param>
    private async Task HandleStreamsFirstChecked(List<StreamData> streams)
    {
        foreach (var stream in streams)
        {
            if (!shardTrackedStreams.TryGetValue(stream.CreateKey(), out var fss))
                continue;

            var tracked = fss.SelectMany(x => x.Value)
                .Where(x => TracksMessage(x) && liveMessages.ContainsKey(x.Id))
                .ToList();

            foreach (var guildGroup in tracked.GroupBy(fs => fs.GuildId))
            {
                var customMessage = stream.IsLive ? await GetCustomStreamMessageAsync(guildGroup.Key) : null;
                foreach (var fs in guildGroup)
                {
                    if (!stream.IsLive)
                        await EndLiveMessageAsync(fs, stream);
                    else if (fs.UpdateLiveMessage)
                        await EditLiveMessageAsync(fs, stream, customMessage, true);
                }
            }
        }
    }

    private async Task LoadTrackedMessagesAsync(MewdekoDb db, List<FollowedStream> followedStreams)
    {
        try
        {
            var rows = await db.TrackedStreamMessages.ToListAsync();
            var stale = new List<int>();
            foreach (var row in rows)
            {
                var fs = followedStreams.FirstOrDefault(x => x.Id == row.FollowedStreamId);
                if (fs is not null && !ownership.IsMine(fs.GuildId))
                    continue;

                if (fs is null || !TracksMessage(fs))
                {
                    stale.Add(row.Id);
                    continue;
                }

                liveMessages[fs.Id] = new LiveMessageState
                {
                    ChannelId = row.ChannelId,
                    MessageId = row.MessageId,
                    StartedAt = DateTime.SpecifyKind(row.StartedAt, DateTimeKind.Utc),
                    LastEdit = DateTime.SpecifyKind(row.LastEdit, DateTimeKind.Utc)
                };
            }

            if (stale.Count > 0)
                await db.TrackedStreamMessages.Where(x => stale.Contains(x.Id)).DeleteAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading tracked live stream messages");
        }
    }

    private async Task SaveTrackedMessageAsync(FollowedStream fs, LiveMessageState state)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            await db.TrackedStreamMessages.Where(x => x.FollowedStreamId == fs.Id).DeleteAsync();
            await db.InsertAsync(new TrackedStreamMessage
            {
                FollowedStreamId = fs.Id,
                GuildId = fs.GuildId,
                ChannelId = state.ChannelId,
                MessageId = state.MessageId,
                StreamKey = $"{fs.Type}:{fs.Username?.ToLowerInvariant()}",
                StartedAt = state.StartedAt,
                LastEdit = state.LastEdit
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving tracked live message for followed stream {StreamId}", fs.Id);
        }
    }

    private async Task TouchTrackedMessageAsync(int followedStreamId, DateTime lastEdit)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            await db.TrackedStreamMessages.Where(x => x.FollowedStreamId == followedStreamId)
                .Set(x => x.LastEdit, lastEdit)
                .UpdateAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating tracked live message for followed stream {StreamId}",
                followedStreamId);
        }
    }

    private async Task ForgetLiveMessageAsync(int followedStreamId)
    {
        liveMessages.TryRemove(followedStreamId, out _);
        await DeleteTrackedMessageAsync(followedStreamId);
    }

    private async Task DeleteTrackedMessageAsync(int followedStreamId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            await db.TrackedStreamMessages.Where(x => x.FollowedStreamId == followedStreamId).DeleteAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting tracked live message for followed stream {StreamId}",
                followedStreamId);
        }
    }

    /// <summary>
    ///     Handles accounts posting new videos and sends notifications.
    /// </summary>
    /// <param name="videoStreams">The list of streams whose latest video changed.</param>
    private async Task HandleVideosPosted(List<StreamData> videoStreams)
    {
        foreach (var stream in videoStreams)
        {
            if (!shardTrackedStreams.TryGetValue(stream.CreateKey(), out var fss))
                continue;

            await fss.SelectMany(x => x.Value).Select(async fs =>
            {
                var textChannel = client.GetGuild(fs.GuildId)?.GetTextChannel(fs.ChannelId);
                if (textChannel is null)
                    return;

                try
                {
                    var posts = stream.NewItems is { Count: > 0 } items
                        ? items.Select(x => x.ApplyTo(stream)).ToList()
                        : [stream];

                    foreach (var post in posts)
                    {
                        var payload = BuildVideoPayload(post, fs);
                        await textChannel.SendMessageAsync(payload.Text, embeds: payload.Embeds,
                            components: payload.Components);
                    }

                    if (stream.SkippedItems > 0)
                        await textChannel.SendMessageAsync(strings.StreamItemsSkipped(fs.GuildId, stream.SkippedItems));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error sending video notification for {Platform}/{Username} in guild {GuildId}",
                        stream.StreamType, stream.UniqueName, fs.GuildId);
                }
            }).WhenAll().ConfigureAwait(false);
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
                var allStreams = fss.SelectMany(x => x.Value).ToList();

                var ended = new HashSet<int>();
                foreach (var fs in allStreams.Where(x => TracksMessage(x) && liveMessages.ContainsKey(x.Id)))
                {
                    if (await EndLiveMessageAsync(fs, stream))
                        ended.Add(fs.Id);
                }

                // Only send offline notifications to guilds that have them enabled
                var eligibleStreams = allStreams
                    .Where(x => OfflineNotificationServers.Contains(x.GuildId) && !ended.Contains(x.Id)).ToList();

                // Group by guild to get custom message once per guild
                var guildGroups = eligibleStreams.GroupBy(fs => fs.GuildId);

                foreach (var guildGroup in guildGroups)
                {
                    var guildId = guildGroup.Key;

                    if (await IsSupersededByTwitchIntegrationAsync(guildId, key))
                        continue;

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

    /// <summary>
    ///     Checks whether the modern Twitch integration (chat bot + go-live, connected via OAuth on the
    ///     dashboard) already owns notifications for this streamer in this guild. When a guild has connected
    ///     its Twitch account and configured a go-live channel for the same channel name, the native
    ///     integration supersedes the generic stream-notifications entry to avoid duplicate messages.
    /// </summary>
    /// <param name="guildId">The guild to check.</param>
    /// <param name="key">The stream data key identifying the platform and username.</param>
    private async Task<bool> IsSupersededByTwitchIntegrationAsync(ulong guildId, StreamDataKey key)
    {
        if (key.Type != FType.Twitch || string.IsNullOrWhiteSpace(key.Name))
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        var twitchConfig = await db.TwitchGuildConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);

        return twitchConfig is { Enabled: true, GoLiveChannelId: not null } &&
               string.Equals(twitchConfig.TwitchChannel, key.Name, StringComparison.OrdinalIgnoreCase);
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

        streamTracker.RegisterSource((FType)fs.Type, key.Name, fs.SourceUrl);
        _ = streamTracker.CacheAddData(key, null, false);
    }

    /// <summary>
    ///     Removes a stream from the tracking system.
    /// </summary>
    /// <param name="fs">The followed stream to untrack.</param>
    private async Task UntrackStream(FollowedStream fs)
    {
        var key = fs.CreateKey();
        await ForgetLiveMessageAsync(fs.Id);

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
    ///     Reads the platform name given to the social follow commands.
    /// </summary>
    /// <param name="name">The name typed, such as youtube, bluesky, reddit or podcast.</param>
    /// <param name="type">The platform it names.</param>
    /// <returns>Whether the name is a known platform.</returns>
    public static bool TryParseSocialType(string name, out FType type)
    {
        switch (name.Trim().ToLowerInvariant())
        {
            case "youtube" or "yt":
                type = FType.YoutubeUploads;
                return true;
            case "bluesky" or "bsky":
                type = FType.Bluesky;
                return true;
            case "reddit":
                type = FType.Reddit;
                return true;
            case "podcast":
                type = FType.Podcast;
                return true;
            default:
                type = default;
                return false;
        }
    }

    /// <summary>
    ///     Follows a YouTube channel's uploads, a Bluesky account, a subreddit or a podcast for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="channelId">The ID of the channel to post in.</param>
    /// <param name="type">The platform to follow on.</param>
    /// <param name="input">The URL, handle or name of the source.</param>
    /// <param name="includeReposts">Whether reposts count as new posts, where the platform has them.</param>
    /// <returns>The source's data, or <see langword="null" /> when it was not found or is already followed here.</returns>
    public async Task<StreamData?> FollowSocial(ulong guildId, ulong channelId, FType type, string input,
        bool includeReposts)
    {
        var data = await streamTracker.ResolveAsync(type, input, includeReposts).ConfigureAwait(false);
        if (data is null)
            return null;

        await using var db = await dbFactory.CreateConnectionAsync();

        var name = data.UniqueName.ToLowerInvariant();
        if (await db.FollowedStreams.AnyAsync(x =>
                x.GuildId == guildId && x.Type == (int)type && x.Username == name))
            return null;

        var fs = new FollowedStream
        {
            Type = (int)type, Username = data.UniqueName, ChannelId = channelId, GuildId = guildId,
            SourceUrl = data.SourceUrl, DateAdded = DateTime.UtcNow
        };

        await db.InsertAsync(fs);

        var streams = GetLocalGuildStreams(data.CreateKey(), guildId);
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
    /// <param name="duration">How long the stream ran, shown when the stream has ended.</param>
    /// <returns>An embed builder with the stream status information.</returns>
    public EmbedBuilder GetEmbed(ulong guildId, StreamData status, TimeSpan? duration = null)
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
            .WithColor(status.IsLive ? GuildEmbedColorService.OkFor(guildId) : Mewdeko.ErrorColor);

        if (!string.IsNullOrWhiteSpace(status.Title))
            embed.WithAuthor(status.Title);

        if (!string.IsNullOrWhiteSpace(status.Game))
            embed.AddField(strings.Streaming(guildId), status.Game, true);

        if (duration is not null)
            embed.AddField(strings.StreamDuration(guildId), FormatDuration(duration.Value), true);

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
    private ReplacementBuilder CreateStreamReplacer(StreamData stream, FollowedStream followedStream, ulong guildId,
        TimeSpan? duration = null)
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
            .WithOverride("%stream.channelid%", () => stream.ChannelId ?? "")
            .WithOverride("%stream.duration%", () => duration is null ? "-" : FormatDuration(duration.Value))
            .WithOverride("%stream.video.title%", () => stream.LatestVideoTitle ?? "")
            .WithOverride("%stream.video.url%", () => stream.LatestVideoUrl ?? streamUrl)
            .WithOverride("%stream.video.thumbnail%", () => stream.LatestVideoThumbnail ?? "")
            .WithOverride("%stream.video.description%", () => stream.LatestVideoDescription ?? "")
            .WithOverride("%stream.video.published%", () => FormatPublished(stream.LatestVideoPublished))
            .WithOverride("%episode.title%", () => stream.LatestVideoTitle ?? "")
            .WithOverride("%episode.url%", () => stream.LatestVideoUrl ?? streamUrl)
            .WithOverride("%episode.description%", () => stream.LatestVideoDescription ?? "")
            .WithOverride("%episode.audio%", () => stream.LatestVideoMedia ?? "")
            .WithOverride("%episode.published%", () => FormatPublished(stream.LatestVideoPublished))
            .WithOverride("%episode.image%", () => stream.LatestVideoThumbnail ?? stream.AvatarUrl ?? "")
            .WithOverride("%podcast.name%", () => stream.Name ?? "");
    }

    private static string FormatPublished(DateTimeOffset? published)
    {
        return published is null ? "" : TimestampTag.FromDateTimeOffset(published.Value).ToString();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
            : $"{Math.Max(1, duration.Minutes)}m";
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
            var payload = BuildPayload(stream, followedStream, guildTemplate);
            var message = await textChannel.SendMessageAsync(payload.Text, embeds: payload.Embeds,
                components: payload.Components, options: new RequestOptions
                {
                    RetryMode = RetryMode.AlwaysRetry
                });

            if (TracksMessage(followedStream) && stream.IsLive)
            {
                var state = new LiveMessageState
                {
                    ChannelId = textChannel.Id,
                    MessageId = message.Id,
                    StartedAt = DateTime.UtcNow,
                    LastEdit = DateTime.UtcNow,
                    Signature = LiveSignature(stream),
                    LastStream = stream
                };

                liveMessages[followedStream.Id] = state;
                await SaveTrackedMessageAsync(followedStream, state);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending stream notification for {Platform}/{Username} in guild {GuildId}",
                stream.StreamType, stream.UniqueName, followedStream.GuildId);
        }
    }

    private static string LiveSignature(StreamData stream)
    {
        return $"{stream.Title}|{stream.Game}|{stream.Viewers}";
    }

    private MessagePayload BuildPayload(StreamData stream, FollowedStream followedStream, string? guildTemplate,
        TimeSpan? duration = null)
    {
        var guildId = followedStream.GuildId;
        var replacer = CreateStreamReplacer(stream, followedStream, guildId, duration).Build();

        // Priority 1: Per-streamer custom message (check both online/offline variants)
        var streamerMessage = GetStreamerCustomMessage(followedStream, stream.IsLive);
        if (!string.IsNullOrWhiteSpace(streamerMessage))
        {
            var processedMessage = replacer.Replace(streamerMessage);

            if (SmartEmbed.TryParse(processedMessage ?? string.Empty, guildId, out var embed, out var plainText,
                    out var components))
                return new MessagePayload(plainText, embed, components?.Build());

            // Not valid JSON - treat as text above default embed
            return new MessagePayload(processedMessage, [GetEmbed(guildId, stream, stream.IsLive ? null : duration).Build()],
                null);
        }

        // Priority 2: Guild-wide template
        if (!string.IsNullOrWhiteSpace(guildTemplate))
        {
            var processedTemplate = replacer.Replace(guildTemplate);

            if (SmartEmbed.TryParse(processedTemplate ?? string.Empty, guildId, out var embed, out var plainText,
                    out var components))
                return new MessagePayload(plainText, embed, components?.Build());

            return new MessagePayload(processedTemplate, null, null);
        }

        // Priority 3: Default embed
        return new MessagePayload(null, [GetEmbed(guildId, stream, stream.IsLive ? null : duration).Build()], null);
    }

    private MessagePayload BuildVideoPayload(StreamData stream, FollowedStream followedStream)
    {
        var guildId = followedStream.GuildId;
        var replacer = CreateStreamReplacer(stream, followedStream, guildId).Build();

        if (!string.IsNullOrWhiteSpace(followedStream.VideoMessage))
        {
            var processed = replacer.Replace(followedStream.VideoMessage);

            if (SmartEmbed.TryParse(processed ?? string.Empty, guildId, out var embed, out var plainText,
                    out var components))
                return new MessagePayload(plainText, embed, components?.Build());

            return new MessagePayload(processed, [BuildVideoEmbed(stream, guildId).Build()], null);
        }

        return new MessagePayload(null, [BuildVideoEmbed(stream, guildId).Build()], null);
    }

    private static EmbedBuilder BuildVideoEmbed(StreamData stream, ulong guildId)
    {
        var embed = new EmbedBuilder()
            .WithAuthor(stream.Name ?? stream.UniqueName, stream.AvatarUrl, stream.StreamUrl)
            .WithTitle((stream.LatestVideoTitle ?? stream.LatestVideoUrl ?? "").TrimTo(250))
            .WithColor(GuildEmbedColorService.OkFor(guildId));

        if (!string.IsNullOrWhiteSpace(stream.LatestVideoUrl))
            embed.WithUrl(stream.LatestVideoUrl);

        if (!string.IsNullOrWhiteSpace(stream.LatestVideoDescription))
            embed.WithDescription(stream.LatestVideoDescription.TrimTo(500));

        if (stream.LatestVideoPublished is { } published)
            embed.WithTimestamp(published);

        if (!string.IsNullOrWhiteSpace(stream.LatestVideoThumbnail))
            embed.WithImageUrl(stream.LatestVideoThumbnail);

        return embed;
    }

    private async Task EditLiveMessageAsync(FollowedStream fs, StreamData stream, string? guildTemplate, bool force)
    {
        if (!liveMessages.TryGetValue(fs.Id, out var state))
            return;

        state.LastStream = stream;
        var now = DateTime.UtcNow;
        var signature = LiveSignature(stream);
        if (!force && (now - state.LastEdit < LiveEditInterval || signature == state.Signature))
            return;

        var channel = client.GetGuild(fs.GuildId)?.GetTextChannel(fs.ChannelId);
        if (channel is null)
        {
            await ForgetLiveMessageAsync(fs.Id);
            return;
        }

        try
        {
            await ModifyAsync(channel, state.MessageId, BuildPayload(stream, fs, guildTemplate, now - state.StartedAt));
            state.LastEdit = now;
            state.Signature = signature;
            await TouchTrackedMessageAsync(fs.Id, now);
        }
        catch (Discord.Net.HttpException ex) when (ex.HttpCode == System.Net.HttpStatusCode.NotFound)
        {
            await ForgetLiveMessageAsync(fs.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not update live message for {Platform}/{Username} in guild {GuildId}",
                stream.StreamType, stream.UniqueName, fs.GuildId);
        }
    }

    private static bool TracksMessage(FollowedStream fs)
    {
        return fs.UpdateLiveMessage || fs.DeleteOnEnd;
    }

    private async Task<bool> EndLiveMessageAsync(FollowedStream fs, StreamData stream)
    {
        if (!liveMessages.TryRemove(fs.Id, out var state))
            return false;

        await DeleteTrackedMessageAsync(fs.Id);

        var channel = client.GetGuild(fs.GuildId)?.GetTextChannel(fs.ChannelId);
        if (channel is null)
            return false;

        if (fs.DeleteOnEnd)
        {
            try
            {
                await channel.DeleteMessageAsync(state.MessageId);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not delete stream message for {Platform}/{Username} in guild {GuildId}",
                    stream.StreamType, stream.UniqueName, fs.GuildId);
            }

            return true;
        }

        try
        {
            var last = state.LastStream ?? stream;
            var ended = last with
            {
                IsLive = false, Viewers = 0, Preview = "", StreamUrl = stream.StreamUrl ?? last.StreamUrl
            };

            await ModifyAsync(channel, state.MessageId,
                BuildPayload(ended, fs, null, DateTime.UtcNow - state.StartedAt));
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not end live message for {Platform}/{Username} in guild {GuildId}",
                stream.StreamType, stream.UniqueName, fs.GuildId);
            return false;
        }
    }

    private static Task ModifyAsync(ITextChannel channel, ulong messageId, MessagePayload payload)
    {
        return channel.ModifyMessageAsync(messageId, props =>
        {
            props.Content = payload.Text ?? string.Empty;
            props.Embeds = payload.Embeds ?? [];
            props.Components = payload.Components ?? new ComponentBuilder().Build();
        });
    }

    private sealed record MessagePayload(string? Text, Discord.Embed[]? Embeds, MessageComponent? Components);

    private sealed class LiveMessageState
    {
        public ulong ChannelId { get; init; }
        public ulong MessageId { get; init; }
        public DateTime StartedAt { get; init; }
        public DateTime LastEdit { get; set; }
        public string Signature { get; set; } = "";
        public StreamData? LastStream { get; set; }
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

        // Drop the cached GuildConfig so a subsequent write elsewhere does not
        // copy the stale in-memory value back over this one.
        guildSettings.ClearCacheForGuild(guildId);

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

        ReplaceLocal(fs);

        return (true, fs);
    }

    private void ReplaceLocal(FollowedStream fs)
    {
        var streams = GetLocalGuildStreams(fs.CreateKey(), fs.GuildId);
        lock (hashSetLock)
        {
            streams.RemoveWhere(x => x.Id == fs.Id);
            streams.Add(fs);
        }
    }

    /// <summary>
    ///     Sets the new video message for a specific stream.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="index">The stream index.</param>
    /// <param name="message">The video message. Empty resets it.</param>
    /// <returns>Whether the operation succeeded and the stream data.</returns>
    public async Task<(bool, FollowedStream)> SetStreamVideoMessage(ulong guildId, int index, string message)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var fss = await db.FollowedStreams
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Id)
            .ToListAsync();

        if (fss.Count <= index)
            return (false, null)!;

        var fs = fss[index];
        fs.VideoMessage = string.IsNullOrWhiteSpace(message) ? null : message;

        await db.UpdateAsync(fs);
        ReplaceLocal(fs);

        return (true, fs);
    }

    /// <summary>
    ///     Toggles whether a stream's live message is edited in place while the stream runs.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="index">The stream index.</param>
    /// <returns>Whether the stream exists, the stream, and whether the option is now on.</returns>
    public async Task<(bool, FollowedStream, bool)> ToggleLiveMessage(ulong guildId, int index)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var fss = await db.FollowedStreams
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Id)
            .ToListAsync();

        if (fss.Count <= index)
            return (false, null, false)!;

        var fs = fss[index];
        fs.UpdateLiveMessage = !fs.UpdateLiveMessage;

        await db.UpdateAsync(fs);
        ReplaceLocal(fs);

        if (!TracksMessage(fs))
            await ForgetLiveMessageAsync(fs.Id);

        return (true, fs, fs.UpdateLiveMessage);
    }

    /// <summary>
    ///     Toggles whether a stream's notification message is deleted when the stream ends.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="index">The stream index.</param>
    /// <returns>Whether the stream exists, the stream, and whether the option is now on.</returns>
    public async Task<(bool, FollowedStream, bool)> ToggleDeleteOnEnd(ulong guildId, int index)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var fss = await db.FollowedStreams
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Id)
            .ToListAsync();

        if (fss.Count <= index)
            return (false, null, false)!;

        var fs = fss[index];
        fs.DeleteOnEnd = !fs.DeleteOnEnd;

        await db.UpdateAsync(fs);
        ReplaceLocal(fs);

        if (!TracksMessage(fs))
            await ForgetLiveMessageAsync(fs.Id);

        return (true, fs, fs.DeleteOnEnd);
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

        ReplaceLocal(fs);

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
                ("%stream.channelid%", "Platform-specific channel ID"),
                ("%stream.duration%", "How long the stream ran (- until it ends)"),
                ("%stream.video.title%", "Title of the latest video, post or episode"),
                ("%stream.video.url%", "URL of the latest video, post or episode"),
                ("%stream.video.thumbnail%", "Thumbnail of the latest video, post or episode"),
                ("%stream.video.description%", "Text or show notes of the latest video, post or episode"),
                ("%stream.video.published%", "When the latest video, post or episode was published")
            ],
            ["Podcast Episodes"] =
            [
                ("%episode.title%", "Title of the new episode"),
                ("%episode.url%", "Link to the new episode"),
                ("%episode.description%", "Show notes of the new episode"),
                ("%episode.audio%", "Link to the episode's audio file"),
                ("%episode.published%", "When the episode was published"),
                ("%episode.image%", "Artwork of the new episode"),
                ("%podcast.name%", "Name of the podcast")
            ]
        };
    }
}