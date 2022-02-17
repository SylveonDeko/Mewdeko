using Mewdeko._Extensions;
using Mewdeko.Database.Models;
using StackExchange.Redis;
using System.Collections.Generic;

namespace Mewdeko.Services;

public interface IDataCache
{
    ConnectionMultiplexer Redis { get; }
    IImageCache LocalImages { get; }
    ILocalDataCache LocalData { get; }

    void CacheAfk(ulong Id, List<AFK> objectList);
    List<AFK> GetAfkForGuild(ulong Id);
    Task AddAfkToCache(ulong Id, List<AFK> newAfk);
    void CacheSnipes(ulong Id, List<SnipeStore> objectList);
    List<SnipeStore> GetSnipesForGuild(ulong Id);
    Task AddSnipesToCache(ulong Id, List<SnipeStore> newSnipes);
    Task<(bool Success, byte[] Data)> TryGetImageDataAsync(Uri key);
    Task SetImageDataAsync(Uri key, byte[] data);
    TimeSpan? AddTimelyClaim(ulong id, int period);
    TimeSpan? AddVoteClaim(ulong id, int period);
    TimeSpan? TryAddRatelimit(ulong id, string name, int expireIn);
    void RemoveAllTimelyClaims();
    bool TryAddAffinityCooldown(ulong userId, out TimeSpan? time);
    bool TryAddDivorceCooldown(ulong userId, out TimeSpan? time);
    bool TryGetEconomy(out string data);
    void SetEconomy(string data);
    

    Task<TOut> GetOrAddCachedDataAsync<TParam, TOut>(string key, Func<TParam, Task<TOut>> factory, TParam param,
        TimeSpan expiry) where TOut : class;

    DateTime GetLastCurrencyDecay();
    void SetLastCurrencyDecay();
}