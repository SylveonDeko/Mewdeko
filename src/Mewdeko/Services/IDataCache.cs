using DataModel;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Searches.Services;
using Mewdeko.Modules.Utility.Common;
using StackExchange.Redis;

namespace Mewdeko.Services;

/// <summary>
///     Represents a data cache interface.
/// </summary>
public interface IDataCache
{
    #region StatusRoles Methods

    /// <summary>
    ///     Sets user status cache.
    /// </summary>
    public Task<bool> SetUserStatusCache(ulong id, string base64);

    #endregion

    #region Ratelimit Methods

    /// <summary>
    ///     Tries to add a ratelimit.
    /// </summary>
    public TimeSpan? TryAddRatelimit(ulong id, string name, int expireIn);

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the Redis connection multiplexer.
    /// </summary>
    public ConnectionMultiplexer Redis { get; }

    /// <summary>
    ///     Gets the local image cache.
    /// </summary>
    public IImageCache LocalImages { get; }

    /// <summary>
    ///     Gets the local data cache.
    /// </summary>
    public ILocalDataCache LocalData { get; }

    #endregion

    #region AFK Methods

    /// <summary>
    ///     Caches AFK status for a user in a guild.
    /// </summary>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="userId">The userid identifier.</param>
    /// <param name="afk">The afk parameter.</param>
    public Task CacheAfk(ulong guildId, ulong userId, Afk afk);

    /// <summary>
    ///     Retrieves AFK status for a user in a guild.
    /// </summary>
    public Task<Afk?> RetrieveAfk(ulong guildId, ulong userId);

    /// <summary>
    ///     Clears AFK status for a user in a guild.
    /// </summary>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="userId">The userid identifier.</param>
    public Task ClearAfk(ulong guildId, ulong userId);

    #endregion

    #region Music Methods

    /// <summary>
    ///     Saves a playlist for a user.
    /// </summary>
    /// <param name="userId">The guild ID.</param>
    /// <param name="playlist">The playlist to save.</param>
    public Task SavePlaylist(ulong userId, MusicPlaylist playlist);

    /// <summary>
    ///     Gets a specific playlist by name.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="name">The name of the playlist.</param>
    /// <returns>The playlist if found, null otherwise.</returns>
    public Task<MusicPlaylist?> GetPlaylist(ulong userId, string name);

    /// <summary>
    ///     Gets all playlists for a guild.
    /// </summary>
    /// <param name="userId">The guild ID.</param>
    /// <returns>The list of playlists.</returns>
    public Task<List<MusicPlaylist>> GetPlaylists(ulong userId);

    /// <summary>
    ///     Sets the player state for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="state">The player state to save.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task SetPlayerState(ulong guildId, MusicPlayerState state);

    /// <summary>
    ///     Retrieves the player state for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The player state if found; otherwise, null.</returns>
    public Task<MusicPlayerState?> GetPlayerState(ulong guildId);

    /// <summary>
    ///     Removes the player state for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task RemovePlayerState(ulong guildId);

    /// <summary>
    ///     Deletes a playlist.
    /// </summary>
    /// <param name="userId">The guild ID.</param>
    /// <param name="name">The name of the playlist to delete.</param>
    /// <returns>True if playlist was deleted, false if not found.</returns>
    public Task<bool> DeletePlaylist(ulong userId, string name);

    /// <summary>
    ///     Retrieves music queue for a server.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <returns>The music queue.</returns>
    public Task<List<MewdekoTrack>> GetMusicQueue(ulong id);

    /// <summary>
    ///     Sets music queue for a server.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <param name="tracks">The music queue.</param>
    /// <returns>A task representing the operation.</returns>
    public Task SetMusicQueue(ulong id, List<MewdekoTrack> tracks);

    /// <summary>
    ///     Sets the current track for a server.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <param name="track">The current track.</param>
    /// <returns>A task representing the operation.</returns>
    public Task SetCurrentTrack(ulong id, MewdekoTrack? track);

    /// <summary>
    ///     Retrieves the current track for a server.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <returns>The current track.</returns>
    public Task<MewdekoTrack?> GetCurrentTrack(ulong id);

    /// <summary>
    ///     Gets music player settings for a server.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <returns>The music player settings if found, null otherwise.</returns>
    public Task<MusicPlayerSetting?> GetMusicPlayerSettings(ulong id);

    /// <summary>
    ///     Sets music player settings for a server.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <param name="settings">The settings to cache.</param>
    /// <returns>A task representing the operation.</returns>
    public Task SetMusicPlayerSettings(ulong id, MusicPlayerSetting settings);

    /// <summary>
    ///     Gets the set of users who have voted to skip the current track.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <returns>The set of user IDs who have voted to skip, or null if no votes exist.</returns>
    public Task<HashSet<ulong>?> GetVoteSkip(ulong id);

    /// <summary>
    ///     Sets the current vote skip state for a server.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <param name="userIds">The set of user IDs who have voted to skip, or null to clear votes.</param>
    /// <returns>A task representing the operation.</returns>
    public Task SetVoteSkip(ulong id, HashSet<ulong>? userIds);

    #endregion

    #region Highlights Methods

    /// <summary>
    ///     Tries to add a highlight stagger for a user in a guild.
    /// </summary>
    public Task<bool> TryAddHighlightStagger(ulong guildId, ulong userId);

    /// <summary>
    ///     Gets the highlight stagger for a user in a guild.
    /// </summary>
    public Task<bool> GetHighlightStagger(ulong guildId, ulong userId);

    /// <summary>
    ///     Caches highlights for a guild.
    /// </summary>
    /// <param name="id">The id identifier.</param>
    /// <param name="highlights">The highlights parameter.</param>
    public Task CacheHighlights(ulong id, List<Highlight> highlights);

    /// <summary>
    ///     Caches highlight settings for a guild.
    /// </summary>
    /// <param name="id">The id identifier.</param>
    /// <param name="highlightSettings">The highlightSettings parameter.</param>
    public Task CacheHighlightSettings(ulong id, List<HighlightSetting> highlightSettings);

    /// <summary>
    ///     Adds highlights to cache for a guild.
    /// </summary>
    /// <param name="id">The id identifier.</param>
    /// <param name="newHighlight">The newHighlight parameter.</param>
    public Task AddHighlightToCache(ulong id, List<Highlight?> newHighlight);

    /// <summary>
    ///     Removes highlights from cache for a guild.
    /// </summary>
    /// <param name="id">The id identifier.</param>
    /// <param name="newHighlight">The newHighlight parameter.</param>
    public Task RemoveHighlightFromCache(ulong id, List<Highlight?> newHighlight);

    /// <summary>
    ///     Executes a Redis command.
    /// </summary>
    public Task<RedisResult> ExecuteRedisCommand(string command);

    /// <summary>
    ///     Adds a highlight setting to cache for a guild.
    /// </summary>
    /// <param name="id">The id identifier.</param>
    /// <param name="newHighlightSetting">The newHighlightSetting parameter.</param>
    public Task AddHighlightSettingToCache(ulong id, List<HighlightSetting?> newHighlightSetting);

    /// <summary>
    ///     Tries to add a highlight stagger for a user.
    /// </summary>
    public Task<bool> TryAddHighlightStaggerUser(ulong id);

    /// <summary>
    ///     Gets highlights for a guild.
    /// </summary>
    public List<Highlight?>? GetHighlightsForGuild(ulong id);

    /// <summary>
    ///     Gets highlight settings for a guild.
    /// </summary>
    public List<HighlightSetting>? GetHighlightSettingsForGuild(ulong id);

    /// <summary>
    ///     Gets a guilds snipes, oldest first. Returns an empty list when the guild has none.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    public Task<List<SnipeStore>> GetSnipesForGuild(ulong id);

    /// <summary>
    ///     Appends snipes to a guilds capped, expiring snipe list.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <param name="newSnipes">The snipes to append, in chronological order.</param>
    public Task AddSnipeToCache(ulong id, IReadOnlyCollection<SnipeStore> newSnipes);

    #endregion

    #region Image Methods

    /// <summary>
    ///     Tries to get image data asynchronously.
    /// </summary>
    public Task<(bool Success, byte[] Data)> TryGetImageDataAsync(Uri key);

    /// <summary>
    ///     Sets image data asynchronously.
    /// </summary>
    /// <param name="key">The key parameter.</param>
    /// <param name="data">The data parameter.</param>
    public Task SetImageDataAsync(Uri key, byte[] data);

    #endregion

    #region Ship Methods

    /// <summary>
    ///     Sets ship cache.
    /// </summary>
    /// <param name="user1">The user1 identifier.</param>
    /// <param name="user2">The user2 identifier.</param>
    /// <param name="score">The score parameter.</param>
    public Task SetShip(ulong user1, ulong user2, int score);

    /// <summary>
    ///     Gets ship cache.
    /// </summary>
    public Task<ShipCache?> GetShip(ulong user1, ulong user2);

    #endregion

    #region GuildConfig Methods

    /// <summary>
    ///     Caches config for a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <param name="config">The config to cache.</param>
    public Task SetGuildConfigCache(ulong id, GuildConfig config);

    /// <summary>
    ///     Retrieves config for a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    /// <returns>If successfull, the guild config, if not, null.</returns>
    public Task<GuildConfig?> GetGuildConfigCache(ulong id);

    #endregion

    #region Cached Data Methods

    /// <summary>
    ///     Gets or adds cached data asynchronously.
    /// </summary>
    public Task<TOut?> GetOrAddCachedDataAsync<TParam, TOut>(string key, Func<TParam?, Task<TOut?>> factory,
        TParam param,
        TimeSpan expiry) where TOut : class;

    /// <summary>
    ///     Sets status role cache.
    /// </summary>
    public Task SetStatusRoleCache(List<StatusRole> statusRoles);

    /// <summary>
    ///     Gets status role cache.
    /// </summary>
    public Task<List<StatusRole>?> GetStatusRoleCache();

    #endregion
}