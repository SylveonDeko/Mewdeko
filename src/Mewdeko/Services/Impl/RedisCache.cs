using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Searches.Services;
using Mewdeko.Modules.Utility.Common;
using Newtonsoft.Json;
using Serilog;
using StackExchange.Redis;

// ReSharper disable CollectionNeverQueried.Local

namespace Mewdeko.Services.Impl;

/// <summary>
/// Service for caching data in Redis.
/// </summary>
public class RedisCache : IDataCache
{
    private readonly JsonSerializerSettings settings = new()
    {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };

    private string redisKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCache"/> class.
    /// </summary>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="shardId">The shard ID.</param>
    public RedisCache(IBotCredentials creds, int shardId)
    {
        var conf = ConfigurationOptions.Parse(creds.RedisOptions);
        conf.SocketManager = new SocketManager("Main", true);
        LoadRedis(conf, creds, shardId).ConfigureAwait(false);
    }

    /// <summary>
    /// The Redis connection multiplexer.
    /// </summary>
    public ConnectionMultiplexer Redis { get; set; }

    /// <summary>
    /// The local image cache.
    /// </summary>
    public IImageCache LocalImages { get; set; }

    /// <summary>
    /// The local data cache.
    /// </summary>
    public ILocalDataCache LocalData { get; set; }


    // things here so far don't need the bot id
    // because it's a good thing if different bots
    // which are hosted on the same PC
    // can re-use the same image/anime data
    /// <summary>
    /// Tries to get image data from the cache.
    /// </summary>
    /// <param name="key">The key to get the image data for.</param>
    /// <returns>A tuple containing a boolean indicating whether the operation was successful and the image data.</returns>
    /// <remarks>
    /// things here so far don't need the bot id
    /// because it's a good thing if different bots
    /// which are hosted on the same PC
    /// can re-use the same image/anime data
    /// </remarks>
    public async Task<(bool Success, byte[] Data)> TryGetImageDataAsync(Uri key)
    {
        var db = Redis.GetDatabase();
        byte[] x = await db.StringGetAsync($"{redisKey}_image_{key}").ConfigureAwait(false);
        return (x != null, x);
    }

    /// <summary>
    /// Caaches a users afk status.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="afk">The afk status.</param>
    public async Task CacheAfk(ulong guildId, ulong userId, Afk afk)
    {
        try
        {
            var db = Redis.GetDatabase();
            await db.StringSetAsync($"{redisKey}_{guildId}_{userId}_afk", JsonConvert.SerializeObject(afk),
                flags: CommandFlags.FireAndForget);
        }
        catch (Exception e)
        {
            Log.Error(e, "An error occured while setting afk");
        }
    }

    /// <summary>
    /// Retrieves a users afk status.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The afk status.</returns>
    public async Task<Afk?> RetrieveAfk(ulong guildId, ulong userId)
    {
        var db = Redis.GetDatabase();
        var afkJson = await db.StringGetAsync($"{redisKey}_{guildId}_{userId}_afk");
        return afkJson.HasValue ? JsonConvert.DeserializeObject<Afk>(afkJson) : null;
    }

    /// <summary>
    /// Clears a users afk status.
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
    /// Caches all status roles.
    /// </summary>
    /// <param name="statusRoles">The status roles to cache.</param>
    public async Task SetStatusRoleCache(List<StatusRolesTable> statusRoles)
    {
        var db = Redis.GetDatabase();
        await db.StringSetAsync($"{redisKey}_statusroles", JsonConvert.SerializeObject(statusRoles));
    }

    /// <summary>
    /// Retrieves all status roles.
    /// </summary>
    /// <returns>The status roles.</returns>
    public async Task<List<StatusRolesTable>> GetStatusRoleCache()
    {
        var db = Redis.GetDatabase();
        var result = await db.StringGetAsync($"{redisKey}_statusroles");
        return result.HasValue
            ? JsonConvert.DeserializeObject<List<StatusRolesTable>>(result)
            : new List<StatusRolesTable>();
    }

    /// <summary>
    /// Sets a users status cache.
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
    /// Caches highlights for a user.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="objectList">The list of highlights.</param>
    /// <returns></returns>
    public Task CacheHighlights(ulong id, List<Highlights> objectList)
    {
        _ = Task.Run(() => new RedisDictionary<ulong, List<Highlights>>($"{redisKey}_Highlights", Redis)
        {
            {
                id, objectList
            }
        }).ConfigureAwait(false);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets the music queue for a guild.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <param name="tracks">The list of tracks.</param>
    /// <returns></returns>
    public async Task SetMusicQueue(ulong id, List<MewdekoTrack> tracks)
    {
        var db = Redis.GetDatabase();
        await db.StringSetAsync($"{redisKey}_musicqueue_{id}", JsonConvert.SerializeObject(tracks, settings));
    }

    /// <summary>
    /// Gets the music queue for a guild.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <returns>A list of tracks.</returns>
    public async Task<List<MewdekoTrack>> GetMusicQueue(ulong id)
    {
        var db = Redis.GetDatabase();
        var result = await db.StringGetAsync($"{redisKey}_musicqueue_{id}");
        return result.HasValue ? JsonConvert.DeserializeObject<List<MewdekoTrack>>(result, settings) : [];
    }

    /// <summary>
    /// Sets the current track for a guild.
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
            await db.StringSetAsync($"{redisKey}_currenttrack_{id}", JsonConvert.SerializeObject(track, settings));
    }

    /// <summary>
    /// Gets the current track for a guild.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <returns>The current track.</returns>
    public async Task<MewdekoTrack?> GetCurrentTrack(ulong id)
    {
        var db = Redis.GetDatabase();
        var result = await db.StringGetAsync($"{redisKey}_currenttrack_{id}");
        return result.HasValue ? JsonConvert.DeserializeObject<MewdekoTrack>(result, settings) : null;
    }

    /// <summary>
    /// Caches highlight settings for a user.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="objectList">The list of highlight settings.</param>
    /// <returns></returns>
    public Task CacheHighlightSettings(ulong id, List<HighlightSettings> objectList)
    {
        _ = Task.Run(() => new RedisDictionary<ulong, List<HighlightSettings>>($"{redisKey}_highlightSettings", Redis)
        {
            {
                id, objectList
            }
        }).ConfigureAwait(false);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds a snipe to the cache
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <param name="newSnipes">The list of snipes.</param>
    /// <returns></returns>
    public Task AddSnipeToCache(ulong id, List<SnipeStore> newSnipes)
    {
        var customers = new RedisDictionary<ulong, List<SnipeStore>>($"{id}_{redisKey}_snipes", Redis);
        customers.Remove(id);
        customers.Add(id, newSnipes);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds a users highlights to the cache.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="newHighlight">The list of highlights.</param>
    /// <returns></returns>
    public Task AddHighlightToCache(ulong id, List<Highlights?> newHighlight)
    {
        var customers = new RedisDictionary<ulong, List<Highlights?>>($"{redisKey}_highlights", Redis);
        customers.Remove(id);
        customers.Add(id, newHighlight);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes a Redis command.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <returns></returns>
    public Task<RedisResult> ExecuteRedisCommand(string command)
    {
        var db = Redis.GetDatabase();
        return db.ExecuteAsync(command);
    }

    /// <summary>
    /// Tries to add a highlight stagger. Used to prevent spamming highlights.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns></returns>
    public Task<bool> TryAddHighlightStaggerUser(ulong userId)
    {
        var db = Redis.GetDatabase();
        return Task.FromResult(db.StringSet($"{redisKey}_hstagger_{userId}", 0, TimeSpan.FromMinutes(2),
            when: When.NotExists, flags: CommandFlags.FireAndForget));
    }

    /// <summary>
    /// Removes a highlight from a users cache.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="newHighlight">The list of highlights.</param>
    /// <returns></returns>
    public Task RemoveHighlightFromCache(ulong id, List<Highlights?> newHighlight)
    {
        var customers = new RedisDictionary<ulong, List<Highlights?>>($"{redisKey}_highlights", Redis);
        customers.Remove(id);
        customers.Add(id, newHighlight);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds a highlight setting to the cache.
    /// </summary>
    /// <param name="id">The user ID.</param>
    /// <param name="newHighlight">The list of highlight settings.</param>
    /// <returns></returns>
    public Task AddHighlightSettingToCache(ulong id, List<HighlightSettings?> newHighlight)
    {
        var customers = new RedisDictionary<ulong, List<HighlightSettings?>>($"{redisKey}_highlightSettings", Redis);
        customers.Remove(id);
        customers.Add(id, newHighlight);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets all snipes for a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <returns></returns>
    public Task<List<SnipeStore>?> GetSnipesForGuild(ulong id)
    {
        var customers = new RedisDictionary<ulong, List<SnipeStore>>($"{id}_{redisKey}_snipes", Redis);
        return Task.FromResult(customers[id]);
    }

    /// <summary>
    /// Gets all highlights for a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <returns></returns>
    public List<Highlights?>? GetHighlightsForGuild(ulong id)
    {
        var customers = new RedisDictionary<ulong, List<Highlights?>?>($"{redisKey}_highlights", Redis);
        return customers[id];
    }

    /// <summary>
    /// Gets all highlight settings for a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <returns></returns>
    public List<HighlightSettings>? GetHighlightSettingsForGuild(ulong id)
    {
        var customers = new RedisDictionary<ulong, List<HighlightSettings?>?>($"{redisKey}_highlightSettings", Redis);
        return customers[id];
    }

    /// <summary>
    /// Sets the image data in the cache.
    /// </summary>
    /// <param name="key">The key to set the image data for.</param>
    /// <param name="data">The image data.</param>
    public async Task SetImageDataAsync(Uri key, byte[] data)
    {
        var db = Redis.GetDatabase();
        await db.StringSetAsync($"image_{key}", data, flags: CommandFlags.FireAndForget).ConfigureAwait(false);
    }

    /// <summary>
    /// Tries to add a highlight stagger. Used to prevent spamming highlights.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns></returns>
    public Task<bool> TryAddHighlightStagger(ulong guildId, ulong userId)
    {
        var db = Redis.GetDatabase();
        return Task.FromResult(db.StringSet($"{redisKey}_hstagger_{guildId}_{userId}", 0, TimeSpan.FromMinutes(3),
            when: When.NotExists, flags: CommandFlags.FireAndForget));
    }

    /// <summary>
    /// Gets a highlight stagger.
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
    /// Trues to add a rate limit for commands.
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
            when: When.NotExists)
            ? null
            : db.KeyTimeToLive($"{redisKey}_ratelimit_{id}_{name}");
    }

    /// <summary>
    /// Sets ship data in the cache.
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
        await db.StringSetAsync($"{redisKey}_shipcache:{user1}:{user2}", JsonConvert.SerializeObject(toCache),
            expiry: TimeSpan.FromHours(12));
    }

    /// <summary>
    /// Gets ship data from the cache.
    /// </summary>
    /// <param name="user1">The first user ID.</param>
    /// <param name="user2">The second user ID.</param>
    /// <returns></returns>
    public async Task<ShipCache?> GetShip(ulong user1, ulong user2)
    {
        var db = Redis.GetDatabase();
        var result = await db.StringGetAsync($"{redisKey}_shipcache:{user1}:{user2}");
        return !result.HasValue ? null : JsonConvert.DeserializeObject<ShipCache>(result);
    }

    /// <summary>
    /// Dynamically gets or adds cached data.
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
        if (data.HasValue) return (TOut)JsonConvert.DeserializeObject(data, typeof(TOut));
        var obj = await factory(param).ConfigureAwait(false);

        if (obj == null)
            return default;

        await db.StringSetAsync($"{redisKey}_{key}", JsonConvert.SerializeObject(obj),
            expiry, flags: CommandFlags.FireAndForget).ConfigureAwait(false);

        return obj;
    }


    private async Task LoadRedis(ConfigurationOptions options, IBotCredentials creds, int shardId)
    {
        options.AsyncTimeout = 20000;
        options.SyncTimeout = 20000;
        Redis = await ConnectionMultiplexer.ConnectAsync(options).ConfigureAwait(false);
        LocalImages = new RedisImagesCache(Redis, creds);
        LocalData = new RedisLocalDataCache(Redis, creds, shardId);
        redisKey = creds.RedisKey();
    }
}