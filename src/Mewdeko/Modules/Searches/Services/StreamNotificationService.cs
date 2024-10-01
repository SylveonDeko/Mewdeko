#nullable enable
using System.Net.Http;
using System.Threading;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Database.Common;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Searches.Common.StreamNotifications;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Searches.Services;

/// <summary>
///     Service responsible for managing and tracking online stream notifications.
/// </summary>
public class StreamNotificationService : IReadyExecutor, INService
{
    private readonly DiscordShardedClient client;
    private readonly DbContextProvider dbProvider;

    private readonly IPubSub pubSub;
    private readonly Random rng = new MewdekoRandom();

    private readonly object shardLock = new();

    private readonly TypedKey<FollowStreamPubData> streamFollowKey;
    private readonly TypedKey<List<StreamData>> streamsOfflineKey;

    private readonly TypedKey<List<StreamData>> streamsOnlineKey;
    private readonly NotifChecker streamTracker;
    private readonly TypedKey<FollowStreamPubData> streamUnfollowKey;
    private readonly IBotStrings strings;

    private Dictionary<StreamDataKey, Dictionary<ulong, HashSet<FollowedStream>>> shardTrackedStreams;

    private Dictionary<StreamDataKey, HashSet<ulong>> trackCounter = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="StreamNotificationService" /> class.
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
        DbContextProvider dbProvider,
        DiscordShardedClient client,
        IBotStrings strings,
        IDataCache redis,
        IBotCredentials creds,
        IHttpClientFactory httpFactory,
        Mewdeko bot,
        IPubSub pubSub, EventHandler eventHandler)
    {
        this.dbProvider = dbProvider;
        this.client = client;
        this.strings = strings;
        this.pubSub = pubSub;
        streamTracker = new NotifChecker(httpFactory, creds, redis, creds.RedisKey(), true);


        streamsOnlineKey = new TypedKey<List<StreamData>>("streams.online");
        streamsOfflineKey = new TypedKey<List<StreamData>>("streams.offline");

        streamFollowKey = new TypedKey<FollowStreamPubData>("stream.follow");
        streamUnfollowKey = new TypedKey<FollowStreamPubData>("stream.unfollow");


        // shard 0 will keep track of when there are no more guilds which track a stream
        _ = Task.Run(async () =>
        {
            await using var dbContext = await dbProvider.GetContextAsync();

            OfflineNotificationServers = await dbContext.Set<GuildConfig>()
                .AsNoTracking()
                .Where(x => x.NotifyStreamOffline)
                .Select(x => x.GuildId)
                .ToListAsync();

            var followedStreams = await dbContext.Set<FollowedStream>()
                .AsNoTracking()
                .ToListAsync();

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

            var allFollowedStreams = await dbContext.Set<FollowedStream>()
                .AsNoTracking()
                .ToListAsync();

            foreach (var fs in allFollowedStreams)
                await streamTracker.CacheAddData(fs.CreateKey(), null, false);

            trackCounter = allFollowedStreams.GroupBy(x => new
                {
                    x.Type, Name = x.Username.ToLower()
                })
                .ToDictionary(x => new StreamDataKey(x.Key.Type, x.Key.Name),
                    x => x.Select(fs => fs.GuildId).ToHashSet());
        });

        _ = Task.Run(async () =>
        {
            await this.pubSub.Sub(streamsOnlineKey, async data =>
            {
                await HandleStreamsOnline(data);
            });
            await this.pubSub.Sub(streamsOfflineKey, async data =>
            {
                await HandleStreamsOffline(data);
            });
        });

        _ = Task.Run(async () =>
        {
            // only shard 0 will run the tracker,
            // and then publish updates with redis to other shards
            streamTracker.OnStreamsOffline +=
                async data => await OnStreamsOffline(data).ConfigureAwait(false);
            streamTracker.OnStreamsOnline +=
                async data => await OnStreamsOnline(data).ConfigureAwait(false);
            _ = streamTracker.RunAsync();

            await this.pubSub.Sub(streamFollowKey, async data =>
            {
                await HandleFollowStream(data);
            });
            await this.pubSub.Sub(streamUnfollowKey, async data =>
            {
                await HandleUnfollowStream(data);
            });
        });

        bot.JoinedGuild += ClientOnJoinedGuild;
        eventHandler.LeftGuild += ClientOnLeftGuild;
    }

    private List<ulong> OfflineNotificationServers { get; set; }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        _ = Task.Run(async () =>
        {
            await using var dbContext = await dbProvider.GetContextAsync();

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
                        Log.Information(
                            "Deleting {StreamCount} {Platform} streams because they've been erroring for more than {ErrorLimit}: {RemovedList}",
                            kvp.Value.Count,
                            kvp.Key,
                            errorLimit,
                            string.Join(", ", kvp.Value));

                        var toDelete = await dbContext.Set<FollowedStream>()
                            .Where(x => x.Type == kvp.Key && kvp.Value.Contains(x.Username))
                            .ToListAsync();

                        dbContext.RemoveRange(toDelete);
                        await dbContext.SaveChangesAsync().ConfigureAwait(false);

                        foreach (var loginToDelete in kvp.Value)
                            await streamTracker.UntrackStreamByKey(new StreamDataKey(kvp.Key, loginToDelete));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error cleaning up FollowedStreams");
                }
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles follow stream pubs to keep the counter up to date.
    ///     When counter reaches 0, stream is removed from tracking because
    ///     that means no guilds are subscribed to that stream anymore
    /// </summary>
    private async Task HandleFollowStream(FollowStreamPubData info)
    {
        await streamTracker.CacheAddData(info.Key, null, false);
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

    /// <summary>
    ///     Handles unfollow pubs to keep the counter up to date.
    ///     When counter reaches 0, stream is removed from tracking because
    ///     that means no guilds are subscribed to that stream anymore
    /// </summary>
    private async Task HandleUnfollowStream(FollowStreamPubData info)
    {
        var key = info.Key;
        if (!trackCounter.TryGetValue(key, out var set))
        {
            // it should've been removed already?
            await streamTracker.UntrackStreamByKey(key);
            return;
        }

        set.Remove(info.GuildId);
        if (set.Count != 0)
            return;

        trackCounter.Remove(key);
        // if no other guilds are following this stream
        // untrack the stream
        await streamTracker.UntrackStreamByKey(key);
    }

    private async Task HandleStreamsOffline(List<StreamData> offlineStreams)
    {
        foreach (var stream in offlineStreams)
        {
            var key = stream.CreateKey();
            if (shardTrackedStreams.TryGetValue(key, out var fss))
            {
                await fss
                    // send offline stream notifications only to guilds which enable it with .stoff
                    .SelectMany(x => x.Value)
                    .Where(x => OfflineNotificationServers.Contains(x.GuildId))
                    .Select(fs => client.GetGuild(fs.GuildId)
                        ?.GetTextChannel(fs.ChannelId)
                        ?.EmbedAsync(GetEmbed(fs.GuildId, stream)))
                    .WhenAll().ConfigureAwait(false);
            }
        }
    }

    private async Task HandleStreamsOnline(List<StreamData> onlineStreams)
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

    private async Task OnStreamsOnline(List<StreamData> data)
    {
        await pubSub.Pub(streamsOnlineKey, data);
    }

    private async Task OnStreamsOffline(List<StreamData> data)
    {
        await pubSub.Pub(streamsOfflineKey, data);
    }

    private async Task ClientOnJoinedGuild(GuildConfig guildConfig)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var gc = await dbContext.GuildConfigs.AsQueryable()
            .AsNoTracking()
            .Include(x => x.FollowedStreams)
            .FirstOrDefaultAsync(x => x.GuildId == guildConfig.GuildId);

        if (gc is null)
            return;

        if (gc.NotifyStreamOffline)
            OfflineNotificationServers.Add(gc.GuildId);

        foreach (var followedStream in gc.FollowedStreams)
        {
            var key = followedStream.CreateKey();
            var streams = GetLocalGuildStreams(key, gc.GuildId);
            streams.Add(followedStream);
            PublishFollowStream(followedStream);
        }
    }

    private async Task ClientOnLeftGuild(SocketGuild guild)
    {
        await using var db = await dbProvider.GetContextAsync();
        var gc = await db.ForGuildId(guild.Id, set => set.Include(x => x.FollowedStreams));

        if (OfflineNotificationServers.Contains(gc.GuildId))
            OfflineNotificationServers.Remove(gc.GuildId);

        foreach (var followedStream in gc.FollowedStreams)
        {
            var streams = GetLocalGuildStreams(followedStream.CreateKey(), guild.Id);
            streams.Remove(followedStream);

            await PublishUnfollowStream(followedStream);
        }
    }

    /// <summary>
    ///     Clears all followed streams for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The number of streams removed.</returns>
    public async Task<int> ClearAllStreams(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        await using var db = await dbProvider.GetContextAsync();
        var gc = await db.ForGuildId(guildId, set => set.Include(x => x.FollowedStreams));
        dbContext.RemoveRange(gc.FollowedStreams);
        var removedCount = gc.FollowedStreams.Count;
        foreach (var s in gc.FollowedStreams)
            await PublishUnfollowStream(s).ConfigureAwait(false);

        await dbContext.SaveChangesAsync().ConfigureAwait(false);

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
        FollowedStream fs;

        await using var dbContext = await dbProvider.GetContextAsync();
        {
            var fss = await dbContext.Set<FollowedStream>()
                .Where(x => x.GuildId == guildId)
                .OrderBy(x => x.Id)
                .ToListAsync();

            // out of range
            if (fss.Count <= index)
                return null;

            fs = fss[index];
            dbContext.Remove(fs);

            await dbContext.SaveChangesAsync().ConfigureAwait(false);

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
    {
        pubSub.Pub(streamFollowKey,
            new FollowStreamPubData
            {
                Key = fs.CreateKey(), GuildId = fs.GuildId
            });
    }

    private Task PublishUnfollowStream(FollowedStream fs)
    {
        return pubSub.Pub(streamUnfollowKey,
            new FollowStreamPubData
            {
                Key = fs.CreateKey(), GuildId = fs.GuildId
            });
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

        FollowedStream fs;

        await using var dbContext = await dbProvider.GetContextAsync();
        {
            await using var db = await dbProvider.GetContextAsync();
            var gc = await db.ForGuildId(guildId, set => set.Include(x => x.FollowedStreams));

            // add it to the database
            fs = new FollowedStream
            {
                Type = data.StreamType, Username = data.UniqueName, ChannelId = channelId, GuildId = guildId
            };

            if (gc.FollowedStreams.Count >= 10)
                return null;

            gc.FollowedStreams.Add(fs);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);

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
    ///     Gets the embed for a stream status.
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
    {
        return strings.GetText(key, guildId, replacements);
    }

    /// <summary>
    ///     Toggles the notification for offline streams for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns><see langword="true" /> if notifications are enabled, <see langword="false" /> otherwise.</returns>
    public async Task<bool> ToggleStreamOffline(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        await using var db = await dbProvider.GetContextAsync();
        var gc = await db.ForGuildId(guildId, set => set);
        gc.NotifyStreamOffline = !gc.NotifyStreamOffline;
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

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
    ///     Sets a custom message for a followed stream.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="index">The index of the stream to set the message for.</param>
    /// <param name="message">The custom message to set.</param>
    /// <param name="fs">The followed stream object if successful, otherwise <see langword="null" />.</param>
    /// <returns><see langword="true" /> if the message was successfully set, <see langword="false" /> otherwise.</returns>
    public async Task<(bool, FollowedStream)> SetStreamMessage(
        ulong guildId,
        int index,
        string message)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var fss = await dbContext.Set<FollowedStream>().Where(x => x.GuildId == guildId).OrderBy(x => x.Id)
            .ToListAsync();

        if (fss.Count <= index)
        {
            return (false, null);
        }

        var fs = fss[index];
        fs.Message = message;
        lock (shardLock)
        {
            var streams = GetLocalGuildStreams(fs.CreateKey(), guildId);

            // message doesn't participate in equality checking
            // removing and adding = update
            streams.Remove(fs);
            streams.Add(fs);
        }

        await dbContext.SaveChangesAsync();

        return (true, fs);
    }


    /// <summary>
    ///     Retrieves the followed streams for a guild.
    /// </summary>
    public sealed class FollowStreamPubData
    {
        /// <summary>
        ///     Gets or sets the key of the stream data.
        /// </summary>
        public StreamDataKey Key { get; init; }

        /// <summary>
        ///     Gets or sets the ID of the guild.
        /// </summary>
        public ulong GuildId { get; init; }
    }
}