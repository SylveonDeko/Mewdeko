using Mewdeko.Database.Models;
using StackExchange.Redis;
using System.Collections.Generic;

namespace Mewdeko.Services;

public interface IDataCache
{
    ConnectionMultiplexer Redis { get; }
    IImageCache LocalImages { get; }
    ILocalDataCache LocalData { get; }

    Task CacheAfk(ulong Id, List<AFK> objectList);
    List<AFK> GetAfkForGuild(ulong Id);
    Task AddAfkToCache(ulong Id, List<AFK> newAfk);
    Task CacheHighlights(ulong id, List<Highlights> highlights);
    Task CacheHighlightSettings(ulong id, List<HighlightSettings> highlightSettings);
    void CacheSnipes(ulong Id, List<SnipeStore> objectList);
    List<SnipeStore> GetSnipesForGuild(ulong Id);
    Task SetGuildSettingInt(ulong guildId, string setting, int value);
    Task<int> GetGuildSettingInt(ulong guildId, string setting);
    Task SetGuildSettingString(ulong guildId, string setting, string value);
    Task<string> GetGuildSettingString(ulong guildId, string setting);
    Task SetGuildSettingBool(ulong guildId, string setting, bool value);
    Task<bool> GetGuildSettingBool(ulong guildId, string setting);
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