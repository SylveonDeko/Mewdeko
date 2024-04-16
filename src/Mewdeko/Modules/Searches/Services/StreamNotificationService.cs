#nullable enable
using System.Net.Http;
using System.Threading;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Database.Common;
using Mewdeko.Modules.Searches.Common.StreamNotifications;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Modules.Searches.Services;

/// <summary>
/// Service responsible for managing and tracking online stream notifications.
/// </summary>
public class StreamNotificationService : IReadyExecutor, INService
{
    private readonly DiscordSocketClient client;
    private readonly DbService db;
    private readonly List<ulong> offlineNotificationServers;

    private readonly IPubSub pubSub;
    private readonly Random rng = new MewdekoRandom();

    private readonly object shardLock = new();

    private readonly Dictionary<StreamDataKey, Dictionary<ulong, HashSet<FollowedStream>>> shardTrackedStreams;

    private readonly TypedKey<FollowStreamPubData> streamFollowKey;
    private readonly TypedKey<List<StreamData>> streamsOfflineKey;

    private readonly TypedKey<List<StreamData>> streamsOnlineKey;
    private readonly NotifChecker streamTracker;
    private readonly TypedKey<FollowStreamPubData> streamUnfollowKey;
    private readonly IBotStrings strings;

    private readonly Dictionary<StreamDataKey, HashSet<ulong>> trackCounter = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamNotificationService"/> class.
    /// </summary>
    /// <param name="db">The database service instance.</param>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="strings">The bot string service instance.</param>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="httpFactory">The HTTP client factory.</param>
    /// <param name="bot">The bot instance.</param>
    /// <param name="pubSub">The pub/sub service instance.</param>
    public StreamNotificationService(
        DbService db,
        DiscordSocketClient client,
        IBotStrings strings,
        ConnectionMultiplexer redis,
        IBotCredentials creds,
        IHttpClientFactory httpFactory,
        Mewdeko bot,
        IPubSub pubSub)
    {
        this.db = db;
        this.client = client;
        this.strings = strings;
        this.pubSub = pubSub;
        streamTracker = new NotifChecker(httpFactory, creds, redis, creds.RedisKey(), client.ShardId == 0);
        using var uow = this.db.GetDbContext();

        streamsOnlineKey = new TypedKey<List<StreamData>>("streams.online");
        streamsOfflineKey = new TypedKey<List<StreamData>>("streams.offline");

        streamFollowKey = new TypedKey<FollowStreamPubData>("stream.follow");
        streamUnfollowKey = new TypedKey<FollowStreamPubData>("stream.unfollow");

        offlineNotificationServers =
        [
            ..bot.AllGuildConfigs
                .Where(gc => gc.Value.NotifyStreamOffline != 0)
                .Select(x => x.Key)
                .ToList()
        ];

        var followedStreams = bot.AllGuildConfigs.SelectMany(x => x.Value.FollowedStreams).ToList();

        shardTrackedStreams = followedStreams.GroupBy(x => new
            {
                x.Type, Name = x.Username.ToLower()
            })
            .ToList()
            .ToDictionary(
                x => new StreamDataKey(x.Key.Type, x.Key.Name.ToLower()),
                x => x.GroupBy(y => y.GuildId)
                    .ToDictionary(y => y.Key,
                        y => y.AsEnumerable().ToHashSet()));

        // shard 0 will keep track of when there are no more guilds which track a stream
        if (client.ShardId == 0)
        {
            var allFollowedStreams = uow.Set<FollowedStream>().AsQueryable().ToList();

            foreach (var fs in allFollowedStreams)
                streamTracker.CacheAddData(fs.CreateKey(), null, false);

            trackCounter = allFollowedStreams.GroupBy(x => new
                {
                    x.Type, Name = x.Username.ToLower()
                })
                .ToDictionary(x => new StreamDataKey(x.Key.Type, x.Key.Name),
                    x => x.Select(fs => fs.GuildId).ToHashSet());
        }

        this.pubSub.Sub(streamsOfflineKey, HandleStreamsOffline);
        this.pubSub.Sub(streamsOnlineKey, HandleStreamsOnline);

        if (client.ShardId == 0)
        {
            // only shard 0 will run the tracker,
            // and then publish updates with redis to other shards
            streamTracker.OnStreamsOffline += OnStreamsOffline;
            streamTracker.OnStreamsOnline += OnStreamsOnline;
            _ = streamTracker.RunAsync();

            this.pubSub.Sub(streamFollowKey, HandleFollowStream);
            this.pubSub.Sub(streamUnfollowKey, HandleUnfollowStream);
        }

        bot.JoinedGuild += ClientOnJoinedGuild;
        client.LeftGuild += ClientOnLeftGuild;
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        if (client.ShardId != 0)
            return;

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

                var uow = db.GetDbContext();
                await using var _ = uow.ConfigureAwait(false);
                foreach (var kvp in deleteGroups)
                {
                    Log.Information(
                        "Deleting {StreamCount} {Platform} streams because they've been erroring for more than {ErrorLimit}: {RemovedList}",
                        kvp.Value.Count,
                        kvp.Key,
                        errorLimit,
                        string.Join(", ", kvp.Value));

                    var toDelete = uow.Set<FollowedStream>()
                        .AsQueryable()
                        .Where(x => x.Type == kvp.Key && kvp.Value.Contains(x.Username))
                        .ToList();

                    uow.RemoveRange(toDelete);
                    await uow.SaveChangesAsync().ConfigureAwait(false);

                    foreach (var loginToDelete in kvp.Value)
                        streamTracker.UntrackStreamByKey(new StreamDataKey(kvp.Key, loginToDelete));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error cleaning up FollowedStreams");
            }
        }
    }

    /// <summary>
    ///     Handles follow stream pubs to keep the counter up to date.
    ///     When counter reaches 0, stream is removed from tracking because
    ///     that means no guilds are subscribed to that stream anymore
    /// </summary>
    private ValueTask HandleFollowStream(FollowStreamPubData info)
    {
        streamTracker.CacheAddData(info.Key, null, false);
        lock (shardLock)
        {
            var key = info.Key;
            if (trackCounter.TryGetValue(key, out _))
            {
                trackCounter[key].Add(info.GuildId);
            }
            else
            {
                trackCounter[key] = [info.GuildId];
            }
        }

        return default;
    }

    /// <summary>
    ///     Handles unfollow pubs to keep the counter up to date.
    ///     When counter reaches 0, stream is removed from tracking because
    ///     that means no guilds are subscribed to that stream anymore
    /// </summary>
    private ValueTask HandleUnfollowStream(FollowStreamPubData info)
    {
        lock (shardLock)
        {
            var key = info.Key;
            if (!trackCounter.TryGetValue(key, out var set))
            {
                // it should've been removed already?
                streamTracker.UntrackStreamByKey(in key);
                return default;
            }

            set.Remove(info.GuildId);
            if (set.Count != 0)
                return default;

            trackCounter.Remove(key);
            // if no other guilds are following this stream
            // untrack the stream
            streamTracker.UntrackStreamByKey(in key);
        }

        return default;
    }

    private async ValueTask HandleStreamsOffline(List<StreamData> offlineStreams)
    {
        foreach (var stream in offlineStreams)
        {
            var key = stream.CreateKey();
            if (shardTrackedStreams.TryGetValue(key, out var fss))
            {
                await fss
                    // send offline stream notifications only to guilds which enable it with .stoff
                    .SelectMany(x => x.Value)
                    .Where(x => offlineNotificationServers.Contains(x.GuildId))
                    .Select(fs => client.GetGuild(fs.GuildId)
                        ?.GetTextChannel(fs.ChannelId)
                        ?.EmbedAsync(GetEmbed(fs.GuildId, stream)))
                    .WhenAll().ConfigureAwait(false);
            }
        }
    }

    private async ValueTask HandleStreamsOnline(List<StreamData> onlineStreams)
    {
        foreach (var stream in onlineStreams)
        {
            var key = stream.CreateKey();
            if (shardTrackedStreams.TryGetValue(key, out var fss))
            {
                await fss.SelectMany(x => x.Value)
                    .Select(fs =>
                    {
                        var textChannel = client.GetGuild(fs.GuildId)?.GetTextChannel(fs.ChannelId);

                        if (textChannel is null)
                            return Task.CompletedTask;

                        var rep = new ReplacementBuilder().WithOverride("%user%", () => fs.Username)
                            .WithOverride("%platform%", () => fs.Type.ToString())
                            .Build();

                        var message = string.IsNullOrWhiteSpace(fs.Message) ? "" : rep.Replace(fs.Message);

                        return textChannel.EmbedAsync(GetEmbed(fs.GuildId, stream), message);
                    })
                    .WhenAll().ConfigureAwait(false);
            }
        }
    }

    private Task OnStreamsOnline(List<StreamData> data)
        => pubSub.Pub(streamsOnlineKey, data);

    private Task OnStreamsOffline(List<StreamData> data)
        => pubSub.Pub(streamsOfflineKey, data);

    private Task ClientOnJoinedGuild(GuildConfig guildConfig)
    {
        using (var uow = db.GetDbContext())
        {
            var gc = uow.GuildConfigs.AsQueryable()
                .Include(x => x.FollowedStreams)
                .FirstOrDefault(x => x.GuildId == guildConfig.GuildId);

            if (gc is null)
                return Task.CompletedTask;

            if (false.ParseBoth(gc.NotifyStreamOffline.ToString()))
                offlineNotificationServers.Add(gc.GuildId);

            foreach (var followedStream in gc.FollowedStreams)
            {
                var key = followedStream.CreateKey();
                var streams = GetLocalGuildStreams(key, gc.GuildId);
                streams.Add(followedStream);
                PublishFollowStream(followedStream);
            }
        }

        return Task.CompletedTask;
    }

    private Task ClientOnLeftGuild(SocketGuild guild)
    {
        _ = Task.Run(async () =>
        {
            await using var uow = db.GetDbContext();
            var gc = await uow.ForGuildId(guild.Id, set => set.Include(x => x.FollowedStreams));

            if (offlineNotificationServers.Contains(gc.GuildId))
                offlineNotificationServers.Remove(gc.GuildId);

            foreach (var followedStream in gc.FollowedStreams)
            {
                var streams = GetLocalGuildStreams(followedStream.CreateKey(), guild.Id);
                streams.Remove(followedStream);

                await PublishUnfollowStream(followedStream);
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears all followed streams for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The number of streams removed.</returns>
    public async Task<int> ClearAllStreams(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set.Include(x => x.FollowedStreams));
        uow.RemoveRange(gc.FollowedStreams);
        var removedCount = gc.FollowedStreams.Count;
        foreach (var s in gc.FollowedStreams)
            await PublishUnfollowStream(s).ConfigureAwait(false);

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return removedCount;
    }

    /// <summary>
    /// Unfollows a stream for a guild and removes it from the tracked streams.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="index">The index of the stream to unfollow.</param>
    /// <returns>The unfollowed stream data if successful, otherwise <see langword="null"/>.</returns>
    public async Task<FollowedStream?> UnfollowStreamAsync(ulong guildId, int index)
    {
        FollowedStream fs;
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var fss = uow.Set<FollowedStream>()
                .AsQueryable()
                .Where(x => x.GuildId == guildId)
                .OrderBy(x => x.Id)
                .ToList();

            // out of range
            if (fss.Count <= index)
                return null;

            fs = fss[index];
            uow.Remove(fs);

            await uow.SaveChangesAsync().ConfigureAwait(false);

            // remove from local cache
            lock (shardLock)
            {
                var key = fs.CreateKey();
                var streams = GetLocalGuildStreams(key, guildId);
                streams.Remove(fs);
            }
        }

        await PublishUnfollowStream(fs).ConfigureAwait(false);

        return fs;
    }

    private void PublishFollowStream(FollowedStream fs)
        => pubSub.Pub(streamFollowKey,
            new FollowStreamPubData
            {
                Key = fs.CreateKey(), GuildId = fs.GuildId
            });

    private Task PublishUnfollowStream(FollowedStream fs)
        => pubSub.Pub(streamUnfollowKey,
            new FollowStreamPubData
            {
                Key = fs.CreateKey(), GuildId = fs.GuildId
            });

    /// <summary>
    /// Follows a stream for a guild and adds it to the tracked streams.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="channelId">The ID of the channel.</param>
    /// <param name="url">The URL of the stream.</param>
    /// <returns>The stream data if successful, otherwise <see langword="null"/>.</returns>
    public async Task<StreamData?> FollowStream(ulong guildId, ulong channelId, string url)
    {
        var data = await streamTracker.GetStreamDataByUrlAsync(url).ConfigureAwait(false);

        if (data is null)
            return null;

        FollowedStream fs;
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var gc = await uow.ForGuildId(guildId, set => set.Include(x => x.FollowedStreams));

            // add it to the database
            fs = new FollowedStream
            {
                Type = data.StreamType, Username = data.UniqueName, ChannelId = channelId, GuildId = guildId
            };

            if (gc.FollowedStreams.Count >= 10)
                return null;

            gc.FollowedStreams.Add(fs);
            await uow.SaveChangesAsync().ConfigureAwait(false);

            // add it to the local cache of tracked streams
            // this way this shard will know it needs to post a message to discord
            // when shard 0 publishes stream status changes for this stream
            lock (shardLock)
            {
                var key = data.CreateKey();
                var streams = GetLocalGuildStreams(key, guildId);
                streams.Add(fs);
            }
        }

        PublishFollowStream(fs);
        return data;
    }

    /// <summary>
    /// Gets the embed for a stream status.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="status">The stream status data.</param>
    /// <returns></returns>
    public EmbedBuilder GetEmbed(ulong guildId, StreamData status)
    {
        var embed = new EmbedBuilder()
            .WithTitle(status.Name)
            .WithUrl(status.StreamUrl)
            .WithDescription(status.StreamUrl)
            .AddField(efb => efb.WithName(GetText(guildId, "status"))
                .WithValue(status.IsLive ? "🟢 Online" : "🔴 Offline")
                .WithIsInline(true))
            .AddField(efb => efb.WithName(GetText(guildId, "viewers"))
                .WithValue(status.IsLive ? status.Viewers.ToString() : "-")
                .WithIsInline(true))
            .WithColor(status.IsLive ? Mewdeko.OkColor : Mewdeko.ErrorColor);

        if (!string.IsNullOrWhiteSpace(status.Title))
            embed.WithAuthor(status.Title);

        if (!string.IsNullOrWhiteSpace(status.Game))
            embed.AddField(GetText(guildId, "streaming"), status.Game, true);

        if (!string.IsNullOrWhiteSpace(status.AvatarUrl))
            embed.WithThumbnailUrl(status.AvatarUrl);

        if (!string.IsNullOrWhiteSpace(status.Preview))
            embed.WithImageUrl($"{status.Preview}?dv={rng.Next()}");

        return embed;
    }

    private string GetText(ulong guildId, string key, params object[] replacements)
        => strings.GetText(key, guildId, replacements);

    /// <summary>
    /// Toggles the notification for offline streams for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns><see langword="true"/> if notifications are enabled, <see langword="false"/> otherwise.</returns>
    public async Task<bool> ToggleStreamOffline(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);
        var newValue = gc.NotifyStreamOffline == 0L ? 1L : 0L;
        gc.NotifyStreamOffline = newValue;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        if (newValue == 1L)
            offlineNotificationServers.Add(guildId);
        else
            offlineNotificationServers.Remove(guildId);

        return newValue == 1L;
    }

    /// <summary>
    /// Retrieves stream data for a given URL.
    /// </summary>
    /// <param name="url">The URL of the stream.</param>
    /// <returns>The stream data if available, otherwise <see langword="null"/>.</returns>
    public Task<StreamData?> GetStreamDataAsync(string url)
        => streamTracker.GetStreamDataByUrlAsync(url);

    private HashSet<FollowedStream> GetLocalGuildStreams(in StreamDataKey key, ulong guildId)
    {
        if (shardTrackedStreams.TryGetValue(key, out var map))
        {
            if (map.TryGetValue(guildId, out var set))
                return set;
            return map[guildId] = [];
        }

        shardTrackedStreams[key] = new Dictionary<ulong, HashSet<FollowedStream>>
        {
            {
                guildId, []
            }
        };
        return shardTrackedStreams[key][guildId];
    }

    /// <summary>
    /// Sets a custom message for a followed stream.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="index">The index of the stream to set the message for.</param>
    /// <param name="message">The custom message to set.</param>
    /// <param name="fs">The followed stream object if successful, otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the message was successfully set, <see langword="false"/> otherwise.</returns>
    public bool SetStreamMessage(
        ulong guildId,
        int index,
        string message,
        out FollowedStream fs)
    {
        using var uow = db.GetDbContext();
        var fss = uow.Set<FollowedStream>().AsQueryable().Where(x => x.GuildId == guildId).OrderBy(x => x.Id).ToList();

        if (fss.Count <= index)
        {
            fs = null;
            return false;
        }

        fs = fss[index];
        fs.Message = message;
        lock (shardLock)
        {
            var streams = GetLocalGuildStreams(fs.CreateKey(), guildId);

            // message doesn't participate in equality checking
            // removing and adding = update
            streams.Remove(fs);
            streams.Add(fs);
        }

        uow.SaveChanges();

        return true;
    }

    /// <summary>
    /// Sets a custom message for all followed streams in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="message">The custom message to set.</param>
    /// <returns>The number of streams for which the message was set.</returns>
    public int SetStreamMessageForAll(ulong guildId, string message)
    {
        using var uow = db.GetDbContext();

        var all = uow.Set<FollowedStream>().ToList();

        if (all.Count == 0)
            return 0;

        all.ForEach(x => x.Message = message);

        uow.SaveChanges();

        return all.Count;
    }

    /// <summary>
    /// Retrieves the followed streams for a guild.
    /// </summary>
    public sealed class FollowStreamPubData
    {
        /// <summary>
        /// Gets or sets the key of the stream data.
        /// </summary>
        public StreamDataKey Key { get; init; }

        /// <summary>
        /// Gets or sets the ID of the guild.
        /// </summary>
        public ulong GuildId { get; init; }
    }
}