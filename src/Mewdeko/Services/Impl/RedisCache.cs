using System.Text.Json;
using System.Text.Json.Serialization;
using DataModel;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Searches.Services;
using Mewdeko.Modules.Utility.Common;
using Serilog;
using StackExchange.Redis;

// ReSharper disable CollectionNeverQueried.Local

namespace Mewdeko.Services.Impl;

/// <summary>
///     Service for caching data in Redis.
/// </summary>
public class RedisCache : IDataCache
{
    /// <summary>
    ///     How long snipes are retained. There is no cap on how many a guild may hold within this window.
    /// </summary>
    private static readonly TimeSpan SnipeExpiry = TimeSpan.FromDays(30);

    private static readonly JsonSerializerOptions options = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    private readonly string redisKey;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RedisCache" /> class.
    /// </summary>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="shardCount">The shard count.</param>
    public RedisCache(IBotCredentials creds, int shardCount)
    {
        RedisConnectionManager.Initialize(creds.RedisConnections, shardCount);
        LocalData = new RedisLocalDataCache(Redis, creds);
        redisKey = creds.RedisKey();
    }

    /// <summary>
    ///     The Redis connection multiplexer.
    /// </summary>
    public ConnectionMultiplexer Redis
    {
        get
        {
            return RedisConnectionManager.Connection;
        }
    }

    /// <summary>
    ///     The local image cache.
    /// </summary>
    public IImageCache LocalImages { get; set; }

    /// <summary>
    ///     The local data cache.
    /// </summary>
    public ILocalDataCache LocalData { get; set; }


    // things here so far don't need the bot id
    // because it's a good thing if different bots
    // which are hosted on the same PC
    // can re-use the same image/anime data
    /// <summary>
    ///     Tries to get image data from the cache.
    /// </summary>
    /// <param name="key">The key to get the image data for.</param>
    /// <returns>A tuple containing a boolean indicating whether the operation was successful and the image data.</returns>
    /// <remarks>
    ///     things here so far don't need the bot id
    ///     because it's a good thing if different bots
    ///     which are hosted on the same PC
    ///     can re-use the same image/anime data
    /// </remarks>
    public async Task<(bool Success, byte[] Data)> TryGetImageDataAsync(Uri key)
    {
        var db = Redis.GetDatabase();
        byte[] x = await db.StringGetAsync($"{redisKey}_image_{key}").ConfigureAwait(false);
        return (x != null, x);
    }

    /// <summary>
    ///     Caaches a users afk status.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="afk">The afk status.</param>
    public async Task CacheAfk(ulong guildId, ulong userId, Afk afk)
    {
        try
        {
            var db = Redis.GetDatabase();
            await db.StringSetAsync($"{redisKey}_{guildId}_{userId}_afk", JsonSerializer.Serialize(afk),
                flags: CommandFlags.FireAndForget);
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occured while setting afk");
        }
    }

    /// <summary>
    ///     Retrieves a users afk status.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The afk status.</returns>
    public async Task<Afk?> RetrieveAfk(ulong guildId, ulong userId)
    {
        var db = Redis.GetDatabase();
        var afkJson = await db.StringGetAsync($"{redisKey}_{guildId}_{userId}_afk");
        return afkJson.HasValue ? JsonSerializer.Deserialize<Afk>((string)afkJson) : null;
    }

    /// <summary>
    ///     Clears a users afk status.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    public async Task ClearAfk(ulong guildId, ulong userId)
    {
        var db = Redis.GetDatabase();
        await db.KeyDeleteAsync($"{redisKey}_{guildId}_{userId}_afk",
            CommandFlags.FireAndForget);
    }

    /// <summary>
    ///     Caches config for a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <param name="config">The config to cache.</param>
    public async Task SetGuildConfigCache(ulong id, GuildConfig config)
    {
        var db = Redis.GetDatabase();
        await db.StringSetAsync($"{redisKey}_guildconfig_{id}", JsonSerializer.Serialize(config, options));
    }

    /// <summary>
    ///     Retrieves config for a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <returns>If successfull, the guild config, if not, null.</returns>
    public async Task<GuildConfig?> GetGuildConfigCache(ulong id)
    {
        var db = Redis.GetDatabase();
        var result = await db.StringGetAsync($"{redisKey}_guildconfig_{id}");
        return result.HasValue ? JsonSerializer.Deserialize<GuildConfig>((string)result, options) : null;
    }

    /// <summary>
    ///     Caches all status roles.
    /// </summary>
    /// <param name="statusRoles">The status roles to cache.</param>
    public async Task SetStatusRoleCache(List<StatusRole> statusRoles)
    {
        var db = Redis.GetDatabase();
        await db.StringSetAsync($"{redisKey}_statusroles", JsonSerializer.Serialize(statusRoles));
    }

    /// <summary>
    ///     Retrieves all status roles.
    /// </summary>
    /// <returns>The status roles.</returns>
    public async Task<List<StatusRole>> GetStatusRoleCache()
    {
        var db = Redis.GetDatabase();
        var result = await db.StringGetAsync($"{redisKey}_statusroles");
        return result.HasValue
            ? JsonSerializer.Deserialize<List<StatusRole>>((string)result)
            : [];
    }

    /// <summary>
    ///     Sets a users status cache.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="base64">The base64 string of users status.</param>
    /// <returns></returns>
    public async Task<bool> SetUserStatusCache(ulong id, string base64)
    {
        var db = Redis.GetDatabase();
        var value = await db.StringGetAsync($"{redisKey}:statushash:{id}");
        if (value.HasValue)
        {
            var returned = (string)value;
            if (returned == base64)
                return false;
            await db.StringSetAsync($"{redisKey}:statushash:{id}", base64);
            return true;
        }

        await db.StringSetAsync($"{redisKey}:statushash:{id}", base64);
        return true;
    }

    /// <summary>
    ///     Caches highlights for a user.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="objectList">The list of highlights.</param>
    /// <returns></returns>
    public Task CacheHighlights(ulong id, List<Highlight> objectList)
    {
        _ = Task.Run(() => new RedisDictionary<ulong, List<Highlight>>($"{redisKey}_Highlights", Redis)
        {
            {
                id, objectList
            }
        }).ConfigureAwait(false);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Sets the music queue for a guild.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <param name="tracks">The list of tracks.</param>
    /// <returns></returns>
    public async Task SetMusicQueue(ulong id, List<MewdekoTrack> tracks)
    {
        var db = Redis.GetDatabase();
        await db.StringSetAsync($"{redisKey}_musicqueue_{id}", JsonSerializer.Serialize(tracks, options));
    }

    /// <summary>
    ///     Saves a playlist for a guild.
    /// </summary>
    /// <param name="userId">The userid identifier.</param>
    /// <param name="playlist">The playlist parameter.</param>
    public async Task SavePlaylist(ulong userId, MusicPlaylist playlist)
    {
        var db = Redis.GetDatabase();

        // Save the playlist
        await db.StringSetAsync(
            $"{redisKey}_playlist_{userId}_{playlist.Name.ToLower()}",
            JsonSerializer.Serialize(playlist, options));

        // Update the list of playlists for this guild
        await db.SetMembersAsync($"{redisKey}_playlists_{userId}");
        await db.SetAddAsync(
            $"{redisKey}_playlists_{userId}",
            playlist.Name.ToLower());
    }

    /// <summary>
    ///     Sets the player state for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="state">The player state to save.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task SetPlayerState(ulong guildId, MusicPlayerState state)
    {
        return Redis.GetDatabase().StringSetAsync(
            $"{redisKey}:music:player_state:{guildId}",
            JsonSerializer.Serialize(state),
            TimeSpan.FromHours(1));
    }

    /// <summary>
    ///     Retrieves the player state for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The player state if found; otherwise, null.</returns>
    public async Task<MusicPlayerState?> GetPlayerState(ulong guildId)
    {
        var result = await Redis.GetDatabase().StringGetAsync($"{redisKey}:music:player_state:{guildId}");
        if (result.IsNull)
            return null;

        return JsonSerializer.Deserialize<MusicPlayerState>((string)result);
    }

    /// <summary>
    ///     Removes the player state for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task RemovePlayerState(ulong guildId)
    {
        return Redis.GetDatabase().KeyDeleteAsync($"{redisKey}:music:player_state:{guildId}");
    }

    /// <summary>
    ///     Gets a specific playlist by name.
    /// </summary>
    public async Task<MusicPlaylist?> GetPlaylist(ulong userId, string name)
    {
        var db = Redis.GetDatabase();
        var result = await db.StringGetAsync($"{redisKey}_playlist_{userId}_{name.ToLower()}");
        return result.HasValue ? JsonSerializer.Deserialize<MusicPlaylist>((string)result, options) : null;
    }

    /// <summary>
    ///     Gets all playlists for a guild.
    /// </summary>
    public async Task<List<MusicPlaylist>> GetPlaylists(ulong userId)
    {
        var db = Redis.GetDatabase();
        var playlistNames = await db.SetMembersAsync($"{redisKey}_playlists_{userId}");
        var playlists = new List<MusicPlaylist>();

        foreach (var name in playlistNames)
        {
            var playlist = await GetPlaylist(userId, name);
            if (playlist != null)
                playlists.Add(playlist);
        }

        return playlists;
    }

    /// <summary>
    ///     Deletes a playlist.
    /// </summary>
    public async Task<bool> DeletePlaylist(ulong userId, string name)
    {
        var db = Redis.GetDatabase();

        // Try to remove the playlist
        var deleted = await db.KeyDeleteAsync($"{redisKey}_playlist_{userId}_{name.ToLower()}");
        if (!deleted) return false;

        // Remove from the guild's playlist set
        await db.SetRemoveAsync($"{redisKey}_playlists_{userId}", name.ToLower());
        return true;
    }

    /// <summary>
    ///     Gets the music queue for a guild.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <returns>A list of tracks.</returns>
    public async Task<List<MewdekoTrack>> GetMusicQueue(ulong id)
    {
        var db = Redis.GetDatabase();
        var result = await db.StringGetAsync($"{redisKey}_musicqueue_{id}");
        return result.HasValue ? JsonSerializer.Deserialize<List<MewdekoTrack>>((string)result, options) : [];
    }

    /// <summary>
    ///     Sets the current track for a guild.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <param name="track">The track to set.</param>
    /// <return>A task representing the operation.</return>
    public async Task SetCurrentTrack(ulong id, MewdekoTrack? track)
    {
        var db = Redis.GetDatabase();
        if (track is null)
            await db.KeyDeleteAsync($"{redisKey}_currenttrack_{id}");
        else
            await db.StringSetAsync($"{redisKey}_currenttrack_{id}", JsonSerializer.Serialize(track, options));
    }

    /// <summary>
    ///     Gets the current track for a guild.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <returns>The current track.</returns>
    public async Task<MewdekoTrack?> GetCurrentTrack(ulong id)
    {
        var db = Redis.GetDatabase();
        var result = await db.StringGetAsync($"{redisKey}_currenttrack_{id}");
        return result.HasValue ? JsonSerializer.Deserialize<MewdekoTrack>((string)result, options) : null;
    }

    /// <summary>
    ///     Gets music player settings for a server.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <returns>The music player settings if found, null otherwise.</returns>
    public async Task<MusicPlayerSetting?> GetMusicPlayerSettings(ulong id)
    {
        var db = Redis.GetDatabase();
        var result = await db.StringGetAsync($"{redisKey}_musicSettings_{id}");
        return result.HasValue ? JsonSerializer.Deserialize<MusicPlayerSetting>((string)result, options) : null;
    }

    /// <summary>
    ///     Sets music player settings for a server.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <param name="settings">The settings to cache.</param>
    public async Task SetMusicPlayerSettings(ulong id, MusicPlayerSetting settings)
    {
        var db = Redis.GetDatabase();
        await db.StringSetAsync(
            $"{redisKey}_musicSettings_{id}",
            JsonSerializer.Serialize(settings, options));
    }

    /// <summary>
    ///     Gets the set of users who have voted to skip the current track.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <returns>The set of user IDs who have voted to skip, or null if no votes exist.</returns>
    public async Task<HashSet<ulong>?> GetVoteSkip(ulong id)
    {
        var db = Redis.GetDatabase();
        var result = await db.StringGetAsync($"{redisKey}_voteSkip_{id}");
        return result.HasValue ? JsonSerializer.Deserialize<HashSet<ulong>>((string)result, options) : null;
    }

    /// <summary>
    ///     Sets the current vote skip state for a server.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <param name="userIds">The set of user IDs who have voted to skip, or null to clear votes.</param>
    public async Task SetVoteSkip(ulong id, HashSet<ulong>? userIds)
    {
        var db = Redis.GetDatabase();
        if (userIds is null)
        {
            await db.KeyDeleteAsync($"{redisKey}_voteSkip_{id}");
            return;
        }

        await db.StringSetAsync(
            $"{redisKey}_voteSkip_{id}",
            JsonSerializer.Serialize(userIds, options));
    }

    /// <summary>
    ///     Caches highlight settings for a user.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="objectList">The list of highlight settings.</param>
    /// <returns></returns>
    public Task CacheHighlightSettings(ulong id, List<HighlightSetting> objectList)
    {
        _ = Task.Run(() => new RedisDictionary<ulong, List<HighlightSetting>>($"{redisKey}_highlightSettings", Redis)
        {
            {
                id, objectList
            }
        }).ConfigureAwait(false);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Appends snipes to a guilds snipe set, dropping any that have aged past <see cref="SnipeExpiry" /> and refreshing
    ///     the key TTL.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <param name="newSnipes">The snipes to append.</param>
    /// <returns></returns>
    public async Task AddSnipeToCache(ulong id, IReadOnlyCollection<SnipeStore> newSnipes)
    {
        if (newSnipes.Count == 0)
            return;

        var db = Redis.GetDatabase();
        var key = SnipeKey(id);
        var entries = newSnipes
            .Select(x => new SortedSetEntry(JsonSerializer.Serialize(x, options), SnipeScore(x.DateAdded)))
            .ToArray();

        var batch = db.CreateBatch();
        var add = batch.SortedSetAddAsync(key, entries);
        var prune = batch.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity,
            SnipeScore(DateTime.UtcNow - SnipeExpiry), Exclude.Stop);
        var expire = batch.KeyExpireAsync(key, SnipeExpiry);
        batch.Execute();

        await Task.WhenAll(add, prune, expire).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a users highlights to the cache.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="newHighlight">The list of highlights.</param>
    /// <returns></returns>
    public Task AddHighlightToCache(ulong id, List<Highlight?> newHighlight)
    {
        var customers = new RedisDictionary<ulong, List<Highlight?>>($"{redisKey}_highlights", Redis);
        customers.Remove(id);
        customers.Add(id, newHighlight);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Executes a Redis command.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <returns></returns>
    public Task<RedisResult> ExecuteRedisCommand(string command)
    {
        var db = Redis.GetDatabase();
        return db.ExecuteAsync(command);
    }

    /// <summary>
    ///     Tries to add a highlight stagger. Used to prevent spamming highlights.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns></returns>
    public Task<bool> TryAddHighlightStaggerUser(ulong userId)
    {
        var db = Redis.GetDatabase();
        return Task.FromResult(db.StringSet($"{redisKey}_hstagger_{userId}", 0, TimeSpan.FromMinutes(2),
            When.NotExists, CommandFlags.FireAndForget));
    }

    /// <summary>
    ///     Removes a highlight from a users cache.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="newHighlight">The list of highlights.</param>
    /// <returns></returns>
    public Task RemoveHighlightFromCache(ulong id, List<Highlight?> newHighlight)
    {
        var customers = new RedisDictionary<ulong, List<Highlight?>>($"{redisKey}_highlights", Redis);
        customers.Remove(id);
        customers.Add(id, newHighlight);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Adds a highlight setting to the cache.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="newHighlight">The list of highlight settings.</param>
    /// <returns></returns>
    public Task AddHighlightSettingToCache(ulong id, List<HighlightSetting?> newHighlight)
    {
        var customers = new RedisDictionary<ulong, List<HighlightSetting?>>($"{redisKey}_highlightSettings", Redis);
        customers.Remove(id);
        customers.Add(id, newHighlight);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Gets a guilds unexpired snipes, oldest first.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <returns></returns>
    public async Task<List<SnipeStore>> GetSnipesForGuild(ulong id)
    {
        var entries = await Redis.GetDatabase()
            .SortedSetRangeByScoreAsync(SnipeKey(id), SnipeScore(DateTime.UtcNow - SnipeExpiry),
                exclude: Exclude.Start)
            .ConfigureAwait(false);

        var snipes = new List<SnipeStore>(entries.Length);
        foreach (var entry in entries)
        {
            try
            {
                var snipe = JsonSerializer.Deserialize<SnipeStore>(entry.ToString(), options);
                if (snipe is not null)
                    snipes.Add(snipe);
            }
            catch (JsonException ex)
            {
                Log.Warning(ex, "Discarding malformed snipe entry for guild {GuildId}", id);
            }
        }

        return snipes;
    }

    /// <summary>
    ///     Gets all highlights for a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <returns></returns>
    public List<Highlight?>? GetHighlightsForGuild(ulong id)
    {
        var customers = new RedisDictionary<ulong, List<Highlight?>?>($"{redisKey}_highlights", Redis);
        return customers[id];
    }

    /// <summary>
    ///     Gets all highlight settings for a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <returns></returns>
    public List<HighlightSetting>? GetHighlightSettingsForGuild(ulong id)
    {
        var customers = new RedisDictionary<ulong, List<HighlightSetting?>?>($"{redisKey}_highlightSettings", Redis);
        return customers[id];
    }

    /// <summary>
    ///     Sets the image data in the cache.
    /// </summary>
    /// <param name="key">The key to set the image data for.</param>
    /// <param name="data">The image data.</param>
    public async Task SetImageDataAsync(Uri key, byte[] data)
    {
        var db = Redis.GetDatabase();
        await db.StringSetAsync($"image_{key}", data, flags: CommandFlags.FireAndForget).ConfigureAwait(false);
    }

    /// <summary>
    ///     Tries to add a highlight stagger. Used to prevent spamming highlights.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns></returns>
    public Task<bool> TryAddHighlightStagger(ulong guildId, ulong userId)
    {
        var db = Redis.GetDatabase();
        return Task.FromResult(db.StringSet($"{redisKey}_hstagger_{guildId}_{userId}", 0, TimeSpan.FromMinutes(3),
            When.NotExists, CommandFlags.FireAndForget));
    }

    /// <summary>
    ///     Gets a highlight stagger.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns></returns>
    public Task<bool> GetHighlightStagger(ulong guildId, ulong userId)
    {
        var db = Redis.GetDatabase();
        return Task.FromResult(db.StringGet($"{redisKey}_hstagger_{guildId}_{userId}").HasValue);
    }

    /// <summary>
    ///     Trues to add a rate limit for commands.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="name">The name of the command.</param>
    /// <param name="expireIn">The time the cooldown ends.</param>
    /// <returns></returns>
    public TimeSpan? TryAddRatelimit(ulong id, string name, int expireIn)
    {
        var db = Redis.GetDatabase();
        return db.StringSet($"{redisKey}_ratelimit_{id}_{name}",
            0, // i don't use the value
            TimeSpan.FromSeconds(expireIn),
            When.NotExists)
            ? null
            : db.KeyTimeToLive($"{redisKey}_ratelimit_{id}_{name}");
    }

    /// <summary>
    ///     Sets ship data in the cache.
    /// </summary>
    /// <param name="user1">The first user ID.</param>
    /// <param name="user2">The second user ID.</param>
    /// <param name="score">The ship score.</param>
    public async Task SetShip(ulong user1, ulong user2, int score)
    {
        var db = Redis.GetDatabase();
        var toCache = new ShipCache
        {
            User1 = user1, User2 = user2, Score = score
        };
        await db.StringSetAsync($"{redisKey}_shipcache:{user1}:{user2}", JsonSerializer.Serialize(toCache),
            TimeSpan.FromHours(12));
    }

    /// <summary>
    ///     Gets ship data from the cache.
    /// </summary>
    /// <param name="user1">The first user ID.</param>
    /// <param name="user2">The second user ID.</param>
    /// <returns></returns>
    public async Task<ShipCache?> GetShip(ulong user1, ulong user2)
    {
        var db = Redis.GetDatabase();
        var result = await db.StringGetAsync($"{redisKey}_shipcache:{user1}:{user2}");
        return !result.HasValue ? null : JsonSerializer.Deserialize<ShipCache>((string)result);
    }

    /// <summary>
    ///     Dynamically gets or adds cached data.
    /// </summary>
    /// <param name="key">The key to get the data for.</param>
    /// <param name="factory">The factory to create the data.</param>
    /// <param name="param">The parameter to pass to the factory.</param>
    /// <param name="expiry">The expiry time for the data.</param>
    /// <typeparam name="TParam">The type of the parameter.</typeparam>
    /// <typeparam name="TOut">The type of the data.</typeparam>
    /// <returns></returns>
    public async Task<TOut?> GetOrAddCachedDataAsync<TParam, TOut>(string key, Func<TParam?, Task<TOut?>> factory,
        TParam param, TimeSpan expiry) where TOut : class
    {
        var db = Redis.GetDatabase();

        var data = await db.StringGetAsync($"{redisKey}_{key}").ConfigureAwait(false);
        if (data.HasValue) return (TOut)JsonSerializer.Deserialize((string)data, typeof(TOut));
        var obj = await factory(param).ConfigureAwait(false);

        if (obj == null)
            return default;

        await db.StringSetAsync($"{redisKey}_{key}", JsonSerializer.Serialize(obj),
            expiry, flags: CommandFlags.FireAndForget).ConfigureAwait(false);

        return obj;
    }

    /// <summary>
    ///     Scores a snipe by its age so the set stays ordered by time and can be pruned by score.
    /// </summary>
    /// <param name="dateAdded">The time the snipe was captured, in UTC.</param>
    private static double SnipeScore(DateTime dateAdded)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(dateAdded, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
    }

    /// <summary>
    ///     Builds the Redis key holding a guilds snipe set.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    private RedisKey SnipeKey(ulong id)
    {
        return $"{redisKey}_snipes_v2_{id}";
    }
}