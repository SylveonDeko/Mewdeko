using System.Net;
using Mewdeko._Extensions;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Mewdeko.Services.Impl;

public class RedisCache : IDataCache
{
    private readonly EndPoint _redisEndpoint;

    private readonly string _redisKey;

    private readonly object _timelyLock = new();
    private readonly object _voteLock = new();

    public RedisCache(IBotCredentials creds, int shardId)
    {
        var conf = ConfigurationOptions.Parse(creds.RedisOptions);

        Redis = ConnectionMultiplexer.Connect(conf);
        _redisEndpoint = Redis.GetEndPoints().First();
        LocalImages = new RedisImagesCache(Redis, creds);
        LocalData = new RedisLocalDataCache(Redis, creds, shardId);
        _redisKey = creds.RedisKey();
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
        byte[] x = await db.StringGetAsync("image_" + key).ConfigureAwait(false);
        return (x != null, x);
    }

    public Task SetImageDataAsync(Uri key, byte[] data)
    {
        var db = Redis.GetDatabase();
        return db.StringSetAsync("image_" + key, data);
    }


    public TimeSpan? AddTimelyClaim(ulong id, int period)
    {
        if (period == 0)
            return null;
        lock (_timelyLock)
        {
            var time = TimeSpan.FromHours(period);
            var db = Redis.GetDatabase();
            if ((bool?) db.StringGet($"{_redisKey}_timelyclaim_{id}") == null)
            {
                db.StringSet($"{_redisKey}_timelyclaim_{id}", true, time);
                return null;
            }

            return db.KeyTimeToLive($"{_redisKey}_timelyclaim_{id}");
        }
    }

    public TimeSpan? AddVoteClaim(ulong id, int period)
    {
        if (period == 0)
            return null;
        lock (_voteLock)
        {
            var time = TimeSpan.FromHours(period);
            var db = Redis.GetDatabase();
            if ((bool?) db.StringGet($"{_redisKey}_voteclaim_{id}") == null)
            {
                db.StringSet($"{_redisKey}_voteclaim_{id}", true, time);
                return null;
            }

            return db.KeyTimeToLive($"{_redisKey}_voteclaim_{id}");
        }
    }

    public void RemoveAllTimelyClaims()
    {
        var server = Redis.GetServer(_redisEndpoint);
        var db = Redis.GetDatabase();
        foreach (var k in server.Keys(pattern: $"{_redisKey}_timelyclaim_*"))
            db.KeyDelete(k, CommandFlags.FireAndForget);
    }

    public bool TryAddAffinityCooldown(ulong userId, out TimeSpan? time)
    {
        var db = Redis.GetDatabase();
        time = db.KeyTimeToLive($"{_redisKey}_affinity_{userId}");
        if (time == null)
        {
            time = TimeSpan.FromMinutes(30);
            db.StringSet($"{_redisKey}_affinity_{userId}", true, time);
            return true;
        }

        return false;
    }

    public bool TryAddDivorceCooldown(ulong userId, out TimeSpan? time)
    {
        var db = Redis.GetDatabase();
        time = db.KeyTimeToLive($"{_redisKey}_divorce_{userId}");
        if (time == null)
        {
            time = TimeSpan.FromHours(6);
            db.StringSet($"{_redisKey}_divorce_{userId}", true, time);
            return true;
        }

        return false;
    }

    public TimeSpan? TryAddRatelimit(ulong id, string name, int expireIn)
    {
        var db = Redis.GetDatabase();
        if (db.StringSet($"{_redisKey}_ratelimit_{id}_{name}",
                0, // i don't use the value
                TimeSpan.FromSeconds(expireIn),
                When.NotExists))
            return null;

        return db.KeyTimeToLive($"{_redisKey}_ratelimit_{id}_{name}");
    }

    public bool TryGetEconomy(out string data)
    {
        var db = Redis.GetDatabase();
        if ((data = db.StringGet($"{_redisKey}_economy")) != null) return true;

        return false;
    }

    public void SetEconomy(string data)
    {
        var db = Redis.GetDatabase();
        db.StringSet($"{_redisKey}_economy",
            data,
            TimeSpan.FromMinutes(3));
    }

    public async Task<TOut> GetOrAddCachedDataAsync<TParam, TOut>(string key, Func<TParam, Task<TOut>> factory,
        TParam param, TimeSpan expiry) where TOut : class
    {
        var db = Redis.GetDatabase();

        var data = await db.StringGetAsync(key).ConfigureAwait(false);
        if (!data.HasValue)
        {
            var obj = await factory(param).ConfigureAwait(false);

            if (obj == null)
                return default;

            await db.StringSetAsync(key, JsonConvert.SerializeObject(obj),
                expiry).ConfigureAwait(false);

            return obj;
        }

        return (TOut) JsonConvert.DeserializeObject(data, typeof(TOut));
    }

    public DateTime GetLastCurrencyDecay()
    {
        var db = Redis.GetDatabase();

        var str = (string) db.StringGet($"{_redisKey}_last_currency_decay");
        if (string.IsNullOrEmpty(str))
            return DateTime.MinValue;

        return JsonConvert.DeserializeObject<DateTime>(str);
    }

    public void SetLastCurrencyDecay()
    {
        var db = Redis.GetDatabase();

        db.StringSet($"{_redisKey}_last_currency_decay", JsonConvert.SerializeObject(DateTime.UtcNow));
    }

    public Task SetStreamDataAsync(string url, string data)
    {
        var db = Redis.GetDatabase();
        return db.StringSetAsync($"{_redisKey}_stream_{url}", data, TimeSpan.FromHours(6));
    }

    public bool TryGetStreamData(string url, out string dataStr)
    {
        var db = Redis.GetDatabase();
        dataStr = db.StringGet($"{_redisKey}_stream_{url}");

        return !string.IsNullOrWhiteSpace(dataStr);
    }
}