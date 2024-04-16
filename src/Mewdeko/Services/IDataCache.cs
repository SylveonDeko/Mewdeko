using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Searches.Services;
using Mewdeko.Modules.Utility.Common;
using StackExchange.Redis;

namespace Mewdeko.Services
{
    /// <summary>
    /// Represents a data cache interface.
    /// </summary>
    public interface IDataCache
    {
        #region StatusRoles Methods

        /// <summary>
        /// Sets user status cache.
        /// </summary>
        Task<bool> SetUserStatusCache(ulong id, string base64);

        #endregion

        #region Ratelimit Methods

        /// <summary>
        /// Tries to add a ratelimit.
        /// </summary>
        TimeSpan? TryAddRatelimit(ulong id, string name, int expireIn);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the Redis connection multiplexer.
        /// </summary>
        ConnectionMultiplexer Redis { get; }

        /// <summary>
        /// Gets the local image cache.
        /// </summary>
        IImageCache LocalImages { get; }

        /// <summary>
        /// Gets the local data cache.
        /// </summary>
        ILocalDataCache LocalData { get; }

        #endregion

        #region AFK Methods

        /// <summary>
        /// Caches AFK status for a user in a guild.
        /// </summary>
        Task CacheAfk(ulong guildId, ulong userId, Afk afk);

        /// <summary>
        /// Retrieves AFK status for a user in a guild.
        /// </summary>
        Task<Afk?> RetrieveAfk(ulong guildId, ulong userId);

        /// <summary>
        /// Clears AFK status for a user in a guild.
        /// </summary>
        Task ClearAfk(ulong guildId, ulong userId);

        #endregion

        #region Music Methods

        /// <summary>
        /// Retrieves music queue for a server.
        /// </summary>
        /// <param name="id">The server ID.</param>
        /// <returns>The music queue.</returns>
        Task<List<MewdekoTrack>> GetMusicQueue(ulong id);

        /// <summary>
        /// Sets music queue for a server.
        /// </summary>
        /// <param name="id">The server ID.</param>
        /// <param name="tracks">The music queue.</param>
        /// <returns>A task representing the operation.</returns>
        Task SetMusicQueue(ulong id, List<MewdekoTrack> tracks);

        /// <summary>
        /// Sets the current track for a server.
        /// </summary>
        /// <param name="id">The server ID.</param>
        /// <param name="track">The current track.</param>
        /// <returns>A task representing the operation.</returns>
        Task SetCurrentTrack(ulong id, MewdekoTrack? track);

        /// <summary>
        /// Retrieves the current track for a server.
        /// </summary>
        /// <param name="id">The server ID.</param>
        /// <returns>The current track.</returns>
        Task<MewdekoTrack?> GetCurrentTrack(ulong id);

        #endregion

        #region Highlights Methods

        /// <summary>
        /// Tries to add a highlight stagger for a user in a guild.
        /// </summary>
        Task<bool> TryAddHighlightStagger(ulong guildId, ulong userId);

        /// <summary>
        /// Gets the highlight stagger for a user in a guild.
        /// </summary>
        Task<bool> GetHighlightStagger(ulong guildId, ulong userId);

        /// <summary>
        /// Caches highlights for a guild.
        /// </summary>
        Task CacheHighlights(ulong id, List<Highlights> highlights);

        /// <summary>
        /// Caches highlight settings for a guild.
        /// </summary>
        Task CacheHighlightSettings(ulong id, List<HighlightSettings> highlightSettings);

        /// <summary>
        /// Adds highlights to cache for a guild.
        /// </summary>
        Task AddHighlightToCache(ulong id, List<Highlights?> newHighlight);

        /// <summary>
        /// Removes highlights from cache for a guild.
        /// </summary>
        Task RemoveHighlightFromCache(ulong id, List<Highlights?> newHighlight);

        /// <summary>
        /// Executes a Redis command.
        /// </summary>
        Task<RedisResult> ExecuteRedisCommand(string command);

        /// <summary>
        /// Adds a highlight setting to cache for a guild.
        /// </summary>
        Task AddHighlightSettingToCache(ulong id, List<HighlightSettings?> newHighlightSetting);

        /// <summary>
        /// Tries to add a highlight stagger for a user.
        /// </summary>
        Task<bool> TryAddHighlightStaggerUser(ulong id);

        /// <summary>
        /// Gets highlights for a guild.
        /// </summary>
        List<Highlights?>? GetHighlightsForGuild(ulong id);

        /// <summary>
        /// Gets highlight settings for a guild.
        /// </summary>
        List<HighlightSettings>? GetHighlightSettingsForGuild(ulong id);

        /// <summary>
        /// Gets snipes for a guild.
        /// </summary>
        Task<List<SnipeStore>?> GetSnipesForGuild(ulong id);

        /// <summary>
        /// Caches snipes for a guild.
        /// </summary>
        Task AddSnipeToCache(ulong id, List<SnipeStore> newAfk);

        #endregion

        #region Image Methods

        /// <summary>
        /// Tries to get image data asynchronously.
        /// </summary>
        Task<(bool Success, byte[] Data)> TryGetImageDataAsync(Uri key);

        /// <summary>
        /// Sets image data asynchronously.
        /// </summary>
        Task SetImageDataAsync(Uri key, byte[] data);

        #endregion

        #region Ship Methods

        /// <summary>
        /// Sets ship cache.
        /// </summary>
        Task SetShip(ulong user1, ulong user2, int score);

        /// <summary>
        /// Gets ship cache.
        /// </summary>
        Task<ShipCache?> GetShip(ulong user1, ulong user2);

        #endregion

        #region GuildConfig Methods

        /// <summary>
        ///     Caches config for a guild.
        /// </summary>
        /// <param name="id">The guild ID.</param>
        /// <param name="config">The config to cache.</param>
        Task SetGuildConfigCache(ulong id, GuildConfig config);

        /// <summary>
        ///     Retrieves config for a guild.
        /// </summary>
        /// <param name="id">The guild ID.</param>
        /// <returns>If successfull, the guild config, if not, null.</returns>
        Task<GuildConfig?> GetGuildConfigCache(ulong id);

        #endregion

        #region Cached Data Methods

        /// <summary>
        /// Gets or adds cached data asynchronously.
        /// </summary>
        Task<TOut?> GetOrAddCachedDataAsync<TParam, TOut>(string key, Func<TParam?, Task<TOut?>> factory, TParam param,
            TimeSpan expiry) where TOut : class;

        /// <summary>
        /// Sets status role cache.
        /// </summary>
        Task SetStatusRoleCache(List<StatusRolesTable> statusRoles);

        /// <summary>
        /// Gets status role cache.
        /// </summary>
        Task<List<StatusRolesTable>?> GetStatusRoleCache();

        #endregion
    }
}