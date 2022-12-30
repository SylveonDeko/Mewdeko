using System.Net;
using System.Threading.Tasks;
using Mewdeko.Modules.Searches.Services;
using Mewdeko.Modules.Utility.Common;
using Newtonsoft.Json;
using StackExchange.Redis;

// ReSharper disable CollectionNeverQueried.Local

namespace Mewdeko.Services.Impl;

public class RedisCache : IDataCache
{
    private readonly EndPoint redisEndpoint;

    private readonly string redisKey;

    private readonly object timelyLock = new();
    private readonly object voteLock = new();

    public RedisCache(IBotCredentials creds, int shardId)
    {
        var conf = ConfigurationOptions.Parse(creds.RedisOptions);
        conf.SocketManager = SocketManager.ThreadPool;
        Redis = ConnectionMultiplexer.Connect(conf);
        redisEndpoint = Redis.GetEndPoints().First();
        LocalImages = new RedisImagesCache(Redis, creds);
        LocalData = new RedisLocalDataCache(Redis, creds, shardId);
        redisKey = creds.RedisKey();
    }

    public ConnectionMultiplexer Redis { get; }

    public IImageCache LocalImages { get; }
    public ILocalDataCache LocalData { get; }

    // things here so far don't need the bot id
    // because it's a good thing if different bots
    // which are hosted on the same PC
    // can re-use the same image/anime data
    public async Task<(bool Success, byte[] Data)> TryGetImageDataAsync(Uri key)
    {
        var db = Redis.GetDatabase();
        byte[] x = await db.StringGetAsync($"image_{key}").ConfigureAwait(false);
        return (x != null, x);
    }

    public Task CacheAfk(ulong id, List<Afk> objectList)
    {
        _ = Task.Run(() => new RedisDictionary<ulong, List<Afk>>($"{redisKey}_afk", Redis)
        {
            {
                id, objectList
            }
        }).ConfigureAwait(false);
        return Task.CompletedTask;
    }

    public async Task SetStatusRoleCache(List<StatusRolesTable> statusRoles)
    {
        var db = Redis.GetDatabase();
        await db.StringSetAsync($"{redisKey}_statusroles", JsonConvert.SerializeObject(statusRoles));
    }

    public async Task<List<StatusRolesTable>> GetStatusRoleCache()
    {
        var db = Redis.GetDatabase();
        var result = await db.StringGetAsync($"{redisKey}_statusroles");
        return result.HasValue ? JsonConvert.DeserializeObject<List<StatusRolesTable>>(result) : new List<StatusRolesTable>();
    }

    public void AddOrUpdateGuildConfig(ulong guildId, GuildConfig guildConfig)
    {
        var db = Redis.GetDatabase();
        db.StringSet($"{redisKey}_{guildId}_config", JsonConvert.SerializeObject(guildConfig, new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        }), flags: CommandFlags.FireAndForget);
    }

    public async Task<bool> SetUserStatusCache(ulong id, int hashCode)
    {
        var db = Redis.GetDatabase();
        var value = await db.StringGetAsync($"{redisKey}:statushash:{id}");
        if (value.HasValue)
        {
            var returned = JsonConvert.DeserializeObject<int>(value);
            if (returned == hashCode)
                return false;
            await db.StringSetAsync($"{redisKey}:statushash:{id}", JsonConvert.SerializeObject(hashCode));
            return true;
        }

        await db.StringSetAsync($"{redisKey}:statushash:{id}", JsonConvert.SerializeObject(hashCode));
        return true;
    }

    public GuildConfig? GetGuildConfig(ulong guildId)
    {
        var db = Redis.GetDatabase();
        var toDeserialize = db.StringGet($"{redisKey}_{guildId}_config");
        return toDeserialize.IsNull ? null : JsonConvert.DeserializeObject<GuildConfig>(toDeserialize);
    }

    public void DeleteGuildConfig(ulong guildId)
    {
        var db = Redis.GetDatabase();
        db.KeyDelete($"{redisKey}_{guildId}_config", flags: CommandFlags.FireAndForget);
    }

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

    public List<Afk?>? GetAfkForGuild(ulong id)
    {
        var customers = new RedisDictionary<ulong, List<Afk?>?>($"{redisKey}_afk", Redis);
        return customers[id];
    }

    public Task AddAfkToCache(ulong id, List<Afk?> newAfk)
    {
        var customers = new RedisDictionary<ulong, List<Afk?>>($"{redisKey}_afk", Redis);
        customers.Remove(id);
        customers.Add(id, newAfk);
        return Task.CompletedTask;
    }

    public Task AddSnipeToCache(ulong id, List<SnipeStore> newAfk)
    {
        var customers = new RedisDictionary<ulong, List<SnipeStore>>($"{id}_{redisKey}_snipes", Redis);
        customers.Remove(id);
        customers.Add(id, newAfk);
        return Task.CompletedTask;
    }

    public Task AddHighlightToCache(ulong id, List<Highlights?> newHighlight)
    {
        var customers = new RedisDictionary<ulong, List<Highlights?>>($"{redisKey}_highlights", Redis);
        customers.Remove(id);
        customers.Add(id, newHighlight);
        return Task.CompletedTask;
    }

    public Task AddIgnoredChannels(ulong guildId, ulong userId, string ignored)
    {
        var db = Redis.GetDatabase();
        db.StringSet($"{redisKey}_ignoredchannels_{guildId}_{userId}", ignored, flags: CommandFlags.FireAndForget);
        return Task.CompletedTask;
    }

    public Task<RedisResult> ExecuteRedisCommand(string command)
    {
        var db = Redis.GetDatabase();
        return db.ExecuteAsync(command);
    }

    public string GetIgnoredChannels(ulong guildId, ulong userId)
    {
        var db = Redis.GetDatabase();
        var value = db.StringGet($"{redisKey}_ignoredchannels_{guildId}_{userId}");
        return JsonConvert.DeserializeObject<string>(value);
    }

    public Task AddIgnoredUsers(ulong guildId, ulong userId, string ignored)
    {
        var db = Redis.GetDatabase();
        db.StringSet($"{redisKey}_ignoredchannels_{guildId}_{userId}", ignored, flags: CommandFlags.FireAndForget);
        return Task.CompletedTask;
    }

    public Task<bool> TryAddHighlightStaggerUser(ulong userId)
    {
        var db = Redis.GetDatabase();
        return Task.FromResult(db.StringSet($"{redisKey}_hstagger_{userId}", 0, TimeSpan.FromMinutes(2), when: When.NotExists, flags: CommandFlags.FireAndForget));
    }

    public string GetIgnoredUsers(ulong guildId, ulong userId)
    {
        var db = Redis.GetDatabase();
        var value = db.StringGet($"{redisKey}_ignoredchannels_{guildId}_{userId}");
        return JsonConvert.DeserializeObject<string>(value);
    }

    public Task RemoveHighlightFromCache(ulong id, List<Highlights?> newHighlight)
    {
        var customers = new RedisDictionary<ulong, List<Highlights?>>($"{redisKey}_highlights", Redis);
        customers.Remove(id);
        customers.Add(id, newHighlight);
        return Task.CompletedTask;
    }

    public Task AddHighlightSettingToCache(ulong id, List<HighlightSettings?> newHighlight)
    {
        var customers = new RedisDictionary<ulong, List<HighlightSettings?>>($"{redisKey}_highlightSettings", Redis);
        customers.Remove(id);
        customers.Add(id, newHighlight);
        return Task.CompletedTask;
    }

    public Task<List<SnipeStore>?> GetSnipesForGuild(ulong id)
    {
        var customers = new RedisDictionary<ulong, List<SnipeStore>>($"{id}_{redisKey}_snipes", Redis);
        return Task.FromResult(customers[id]);
    }

    public List<Highlights?>? GetHighlightsForGuild(ulong id)
    {
        var customers = new RedisDictionary<ulong, List<Highlights?>?>($"{redisKey}_highlights", Redis);
        return customers[id];
    }

    public List<HighlightSettings>? GetHighlightSettingsForGuild(ulong id)
    {
        var customers = new RedisDictionary<ulong, List<HighlightSettings?>?>($"{redisKey}_highlightSettings", Redis);
        return customers[id];
    }

    public async Task SetImageDataAsync(Uri key, byte[] data)
    {
        var db = Redis.GetDatabase();
        await db.StringSetAsync($"image_{key}", data, flags: CommandFlags.FireAndForget).ConfigureAwait(false);
    }

    public TimeSpan? AddTimelyClaim(ulong id, int period)
    {
        if (period == 0)
            return null;
        lock (timelyLock)
        {
            var time = TimeSpan.FromHours(period);
            var db = Redis.GetDatabase();
            if ((bool?)db.StringGet($"{redisKey}_timelyclaim_{id}") == null)
            {
                db.StringSet($"{redisKey}_timelyclaim_{id}", true, time, flags: CommandFlags.FireAndForget);
                return null;
            }

            return db.KeyTimeToLive($"{redisKey}_timelyclaim_{id}");
        }
    }

    public TimeSpan? AddVoteClaim(ulong id, int period)
    {
        if (period == 0)
            return null;
        lock (voteLock)
        {
            var time = TimeSpan.FromHours(period);
            var db = Redis.GetDatabase();
            if ((bool?)db.StringGet($"{redisKey}_voteclaim_{id}") == null)
            {
                db.StringSet($"{redisKey}_voteclaim_{id}", true, time, flags: CommandFlags.FireAndForget);
                return null;
            }

            return db.KeyTimeToLive($"{redisKey}_voteclaim_{id}");
        }
    }

    public void RemoveAllTimelyClaims()
    {
        var server = Redis.GetServer(redisEndpoint);
        var db = Redis.GetDatabase();
        foreach (var k in server.Keys(pattern: $"{redisKey}_timelyclaim_*"))
            db.KeyDelete(k, CommandFlags.FireAndForget);
    }

    public bool TryAddAffinityCooldown(ulong userId, out TimeSpan? time)
    {
        var db = Redis.GetDatabase();
        time = db.KeyTimeToLive($"{redisKey}_affinity_{userId}");
        if (time == null)
        {
            time = TimeSpan.FromMinutes(30);
            db.StringSet($"{redisKey}_affinity_{userId}", true, time, flags: CommandFlags.FireAndForget);
            return true;
        }

        return false;
    }

    public bool TryAddDivorceCooldown(ulong userId, out TimeSpan? time)
    {
        var db = Redis.GetDatabase();
        time = db.KeyTimeToLive($"{redisKey}_divorce_{userId}");
        if (time == null)
        {
            time = TimeSpan.FromHours(6);
            db.StringSet($"{redisKey}_divorce_{userId}", true, time);
            return true;
        }

        return false;
    }

    public Task<bool> TryAddHighlightStagger(ulong guildId, ulong userId)
    {
        var db = Redis.GetDatabase();
        return Task.FromResult(db.StringSet($"{redisKey}_hstagger_{guildId}_{userId}", 0, TimeSpan.FromMinutes(3), when: When.NotExists, flags: CommandFlags.FireAndForget));
    }

    public Task<bool> GetHighlightStagger(ulong guildId, ulong userId)
    {
        var db = Redis.GetDatabase();
        return Task.FromResult(db.StringGet($"{redisKey}_hstagger_{guildId}_{userId}").HasValue);
    }

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

    public bool TryGetEconomy(out string data)
    {
        var db = Redis.GetDatabase();
        if ((data = db.StringGet($"{redisKey}_economy")) != null) return true;

        return false;
    }

    public void SetEconomy(string data)
    {
        var db = Redis.GetDatabase();
        db.StringSet($"{redisKey}_economy",
            data,
            TimeSpan.FromMinutes(3), flags: CommandFlags.FireAndForget);
    }

    public async Task SetGuildSettingBool(ulong guildId, string setting, bool value)
    {
        var db = Redis.GetDatabase();
        await db.StringSetAsync($"{redisKey}_{setting}_{guildId}", JsonConvert.SerializeObject(value), flags: CommandFlags.FireAndForget).ConfigureAwait(false);
    }

    public async Task<bool> GetGuildSettingBool(ulong guildId, string setting)
    {
        var db = Redis.GetDatabase();
        var toget = await db.StringGetAsync($"{redisKey}_{setting}_{guildId}").ConfigureAwait(false);
        return JsonConvert.DeserializeObject<bool>(toget);
    }

    public async Task SetGuildSettingInt(ulong guildId, string setting, int value)
    {
        var db = Redis.GetDatabase();
        await db.StringSetAsync($"{redisKey}_{setting}_{guildId}", JsonConvert.SerializeObject(value), flags: CommandFlags.FireAndForget).ConfigureAwait(false);
    }

    public async Task<int> GetGuildSettingInt(ulong guildId, string setting)
    {
        var db = Redis.GetDatabase();
        var toget = await db.StringGetAsync($"{redisKey}_{setting}_{guildId}").ConfigureAwait(false);
        return JsonConvert.DeserializeObject<int>(toget);
    }

    public async Task SetShip(ulong user1, ulong user2, int score)
    {
        var db = Redis.GetDatabase();
        var toCache = new ShipCache
        {
            User1 = user1, User2 = user2, Score = score
        };
        await db.StringSetAsync($"{redisKey}_shipcache:{user1}:{user2}", JsonConvert.SerializeObject(toCache), expiry: TimeSpan.FromHours(12));
    }

    public async Task<ShipCache?> GetShip(ulong user1, ulong user2)
    {
        var db = Redis.GetDatabase();
        var result = await db.StringGetAsync($"{redisKey}_shipcache:{user1}:{user2}");
        return !result.HasValue ? null : JsonConvert.DeserializeObject<ShipCache>(result);
    }

    public async Task SetGuildSettingString(ulong guildId, string setting, string value)
    {
        var db = Redis.GetDatabase();
        await db.StringSetAsync($"{redisKey}_{setting}_{guildId}", JsonConvert.SerializeObject(value), flags: CommandFlags.FireAndForget).ConfigureAwait(false);
    }

    public async Task<string> GetGuildSettingString(ulong guildId, string setting)
    {
        var db = Redis.GetDatabase();
        var toget = await db.StringGetAsync($"{redisKey}_{setting}_{guildId}").ConfigureAwait(false);
        return JsonConvert.DeserializeObject<string>(toget);
    }

    public async Task<TOut?> GetOrAddCachedDataAsync<TParam, TOut>(string key, Func<TParam?, Task<TOut?>> factory,
        TParam param, TimeSpan expiry) where TOut : class
    {
        var db = Redis.GetDatabase();

        var data = await db.StringGetAsync(key).ConfigureAwait(false);
        if (data.HasValue) return (TOut)JsonConvert.DeserializeObject(data, typeof(TOut));
        var obj = await factory(param).ConfigureAwait(false);

        if (obj == null)
            return default;

        await db.StringSetAsync(key, JsonConvert.SerializeObject(obj),
            expiry, flags: CommandFlags.FireAndForget).ConfigureAwait(false);

        return obj;
    }

    public DateTime GetLastCurrencyDecay()
    {
        var db = Redis.GetDatabase();

        var str = (string)db.StringGet($"{redisKey}_last_currency_decay");
        if (string.IsNullOrEmpty(str))
            return DateTime.MinValue;

        return JsonConvert.DeserializeObject<DateTime>(str);
    }

    public void SetLastCurrencyDecay()
    {
        var db = Redis.GetDatabase();

        db.StringSet($"{redisKey}_last_currency_decay", JsonConvert.SerializeObject(DateTime.UtcNow), flags: CommandFlags.FireAndForget);
    }

    public Task SetStreamDataAsync(string url, string data)
    {
        var db = Redis.GetDatabase();
        return db.StringSetAsync($"{redisKey}_stream_{url}", data, TimeSpan.FromHours(6), flags: CommandFlags.FireAndForget);
    }

    public bool TryGetStreamData(string url, out string dataStr)
    {
        var db = Redis.GetDatabase();
        dataStr = db.StringGet($"{redisKey}_stream_{url}");

        return !string.IsNullOrWhiteSpace(dataStr);
    }
}