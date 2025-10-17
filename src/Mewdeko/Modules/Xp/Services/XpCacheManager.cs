using System.Globalization;
using System.Text.Json;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Xp.Models;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     Manages caching for XP-related data.
/// </summary>
public class XpCacheManager : INService
{
    // Constants for Redis keys and cache parameters
    private const string RedisKeyPrefix = "xp:";
    private const string GuildSettingsKey = "guild_settings";
    private const string GuildUserKey = "guild_user";
    private const string CooldownKey = "cooldown";
    private const string ExclusionKey = "exclusion";
    private const string MultiplierKey = "multiplier";
    private const string FirstMsgKey = "first_msg";

    // Batch operation parameters
    private const int MaxBatchSize = 100;

    // Cache TTLs
    private static readonly TimeSpan GuildSettingsTtl = TimeSpan.FromHours(2);
    private static readonly TimeSpan UserXpTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ExclusionTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MultiplierTtl = TimeSpan.FromMinutes(5);

    // Cached JsonSerializerOptions for performance - critical for Redis operations
    private static readonly JsonSerializerOptions CachedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly DiscordShardedClient client;
    private readonly IDataCache dataCache;
    private readonly IDataConnectionFactory dbFactory;

    private readonly MemoryCacheOptions hotCacheOptions = new()
    {
        ExpirationScanFrequency = TimeSpan.FromMinutes(2)
    };

    // Small hot cache for frequently accessed guilds only
    private readonly MemoryCache hotGuildCache;
    private readonly ILogger<XpCacheManager> logger;
    private readonly IDatabase redisCache;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpCacheManager" /> class.
    /// </summary>
    /// <param name="dataCache">The data cache.</param>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="client">The current sharded client</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public XpCacheManager(
        IDataCache dataCache,
        IDataConnectionFactory dbFactory,
        DiscordShardedClient client, ILogger<XpCacheManager> logger)
    {
        this.dataCache = dataCache;
        this.dbFactory = dbFactory;
        this.client = client;
        this.logger = logger;
        redisCache = dataCache.Redis.GetDatabase();

        // Initialize small hot cache for frequently accessed guilds
        hotGuildCache = new MemoryCache(hotCacheOptions);
    }

    /// <summary>
    ///     Gets the Redis database instance.
    /// </summary>
    /// <returns>The Redis database.</returns>
    public IDatabase GetRedisDatabase()
    {
        return redisCache;
    }

    /// <summary>
    ///     Gets or creates a guild user XP record.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The guild user XP record.</returns>
    public async Task<GuildUserXp> GetOrCreateGuildUserXpAsync(MewdekoDb db, ulong guildId, ulong userId)
    {
        var cacheKey = $"{RedisKeyPrefix}{GuildUserKey}:{guildId}:{userId}";

        // Try to get from Redis
        var redisData = await redisCache.StringGetAsync(cacheKey).ConfigureAwait(false);
        if (redisData.HasValue)
        {
            try
            {
                var userData = JsonSerializer.Deserialize<GuildUserXp>((string)redisData, CachedJsonOptions);
                if (userData is not null)
                    return userData;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to deserialize user XP data from Redis");
            }
        }

        // Get from database using LinqToDB
        var userXp = await db.GuildUserXps
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId)
            .ConfigureAwait(false);

        if (userXp == null)
        {
            // Create new user entry
            userXp = new GuildUserXp
            {
                GuildId = guildId,
                UserId = userId,
                LastActivity = DateTime.UtcNow,
                LastLevelUp = DateTime.UtcNow,
                NotifyType = (int)XpNotificationType.Channel
            };

            // Insert using LinqToDB
            await db.InsertAsync(userXp).ConfigureAwait(false);
        }

        // Cache in Redis only
        var serializedData = JsonSerializer.Serialize(userXp, CachedJsonOptions);
        await redisCache.StringSetAsync(cacheKey, serializedData, UserXpTtl).ConfigureAwait(false);

        return userXp;
    }

    /// <summary>
    ///     Updates the user XP cache.
    /// </summary>
    /// <param name="userXp">The user XP record to cache.</param>
    public async Task UpdateUserXpCacheAsync(GuildUserXp userXp)
    {
        var cacheKey = $"{RedisKeyPrefix}{GuildUserKey}:{userXp.GuildId}:{userXp.UserId}";

        try
        {
            var serializedData = JsonSerializer.Serialize(userXp, CachedJsonOptions);
            await redisCache.StringSetAsync(cacheKey, serializedData, UserXpTtl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating XP cache for user {UserId} in guild {GuildId}",
                userXp.UserId, userXp.GuildId);
        }
    }

    /// <summary>
    ///     Gets the guild XP settings with hot cache and Redis fallback.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The guild XP settings.</returns>
    public async Task<GuildXpSetting> GetGuildXpSettingsAsync(ulong guildId)
    {
        // Try hot cache first (fastest)
        var hotCacheKey = $"guild:{guildId}";
        if (hotGuildCache.TryGetValue(hotCacheKey, out GuildXpSetting cachedSettings))
        {
            return cachedSettings;
        }

        // Try Redis cache
        var cacheKey = $"{RedisKeyPrefix}{GuildSettingsKey}:{guildId}";
        var redisData = await redisCache.StringGetAsync(cacheKey).ConfigureAwait(false);

        GuildXpSetting settings;
        if (redisData.HasValue)
        {
            try
            {
                settings = JsonSerializer.Deserialize<GuildXpSetting>((string)redisData, CachedJsonOptions);
                if (settings != null)
                {
                    // Update hot cache
                    var options = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));
                    hotGuildCache.Set(hotCacheKey, settings, options);
                    return settings;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to deserialize guild settings from Redis");
            }
        }

        // Get from database
        await using var db = await dbFactory.CreateConnectionAsync();

        settings = await db.GuildXpSettings
            .FirstOrDefaultAsync(x => x.GuildId == guildId)
            .ConfigureAwait(false);

        if (settings == null)
        {
            // Create default settings
            settings = new GuildXpSetting
            {
                GuildId = guildId,
                XpPerMessage = XpService.DefaultXpPerMessage,
                MessageXpCooldown = XpService.DefaultMessageXpCooldown,
                VoiceXpPerMinute = XpService.DefaultVoiceXpPerMinute,
                VoiceXpTimeout = XpService.DefaultVoiceXpTimeout,
                XpMultiplier = 1.0,
                FirstMessageBonus = 0,
                CustomXpImageUrl = "",
                LevelUpMessage = "{UserMention} has reached level {Level}!"
            };

            // Insert using LinqToDB
            await db.InsertAsync(settings).ConfigureAwait(false);
        }

        // Update both caches
        var serializedData = JsonSerializer.Serialize(settings, CachedJsonOptions);
        await redisCache.StringSetAsync(cacheKey, serializedData, GuildSettingsTtl).ConfigureAwait(false);

        var hotOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(10))
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));
        hotGuildCache.Set(hotCacheKey, settings, hotOptions);

        return settings;
    }

    /// <summary>
    ///     Updates the guild XP settings and refreshes caches.
    /// </summary>
    /// <param name="settings">The guild XP settings to update.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UpdateGuildXpSettingsAsync(GuildXpSetting settings)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Update using LinqToDB
        await db.UpdateAsync(settings).ConfigureAwait(false);

        // Update both caches
        var cacheKey = $"{RedisKeyPrefix}{GuildSettingsKey}:{settings.GuildId}";
        var serializedData = JsonSerializer.Serialize(settings, CachedJsonOptions);

        // Update hot cache
        var hotCacheKey = $"guild:{settings.GuildId}";
        var hotOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(10))
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));
        hotGuildCache.Set(hotCacheKey, settings, hotOptions);

        // Update Redis cache
        await redisCache.StringSetAsync(cacheKey, serializedData, GuildSettingsTtl).ConfigureAwait(false);
    }

    /// <summary>
    ///     Checks if a server is excluded from XP gain.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>True if the server is excluded, false otherwise.</returns>
    public async Task<bool> IsServerExcludedAsync(ulong guildId)
    {
        var settings = await GetGuildXpSettingsAsync(guildId).ConfigureAwait(false);
        return settings.XpGainDisabled;
    }

    /// <summary>
    ///     Gets the effective XP multiplier for a user in a channel with caching.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>The effective XP multiplier to apply.</returns>
    public async Task<double> GetEffectiveMultiplierAsync(ulong userId, ulong guildId, ulong channelId)
    {
        var cacheKey = $"{RedisKeyPrefix}{MultiplierKey}:{guildId}:{userId}:{channelId}";

        // Try Redis cache
        var redisValue = await redisCache.StringGetAsync(cacheKey).ConfigureAwait(false);
        if (redisValue.HasValue && double.TryParse((string)redisValue, out var multiplier))
        {
            return multiplier;
        }

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Get guild settings from cache
            var settings = await GetGuildXpSettingsAsync(guildId).ConfigureAwait(false);
            multiplier = settings.XpMultiplier;

            // Apply channel multiplier if exists
            var channelMultiplier = await db.XpChannelMultipliers
                .FirstOrDefaultAsync(c => c.GuildId == guildId && c.ChannelId == channelId)
                .ConfigureAwait(false);

            if (channelMultiplier != null)
            {
                multiplier *= channelMultiplier.Multiplier;
            }

            // Get guild and user for role checks
            var guild = client.GetGuild(guildId);
            var user = guild?.GetUser(userId);

            if (user != null)
            {
                // Get user's role IDs
                var userRoleIds = user.Roles.Select(x => x.Id).ToList();

                if (userRoleIds.Count > 0)
                {
                    // Get role multipliers in a single query
                    var roleMultipliers = await db.XpRoleMultipliers
                        .Where(r => r.GuildId == guildId && userRoleIds.Contains(r.RoleId))
                        .ToListAsync()
                        .ConfigureAwait(false);

                    if (roleMultipliers.Count != 0)
                    {
                        multiplier *= roleMultipliers.Max(r => r.Multiplier);
                    }
                }
            }

            // Apply active boost events in a single query
            var now = DateTime.UtcNow;
            var boostEvents = await db.XpBoostEvents
                .Where(b => b.GuildId == guildId && b.StartTime <= now && b.EndTime >= now)
                .ToListAsync()
                .ConfigureAwait(false);

            // Process all boost events
            foreach (var boost in boostEvents)
            {
                // Parse channel and role restrictions once
                var channelIds = string.IsNullOrEmpty(boost.ApplicableChannels)
                    ? new List<ulong>()
                    : boost.ApplicableChannels.Split(',').Select(ulong.Parse).ToList();

                var roleIds = string.IsNullOrEmpty(boost.ApplicableRoles)
                    ? new List<ulong>()
                    : boost.ApplicableRoles.Split(',').Select(ulong.Parse).ToList();

                // Check if this boost applies
                var applyBoost = !(channelIds.Count > 0 && !channelIds.Contains(channelId));

                // Role restriction check
                if (applyBoost && roleIds.Count > 0)
                {
                    if (user == null || !user.Roles.Select(x => x.Id).Any(r => roleIds.Contains(r)))
                    {
                        applyBoost = false;
                    }
                }

                // Apply multiplier if all checks passed
                if (applyBoost)
                {
                    multiplier *= boost.Multiplier;
                }
            }

            // Cache the result in Redis only
            await redisCache.StringSetAsync(cacheKey, multiplier.ToString(CultureInfo.InvariantCulture), MultiplierTtl)
                .ConfigureAwait(false);

            return multiplier;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calculating effective multiplier");
            return 1.0; // Default multiplier on error
        }
    }

    /// <summary>
    ///     Checks if a user can gain XP with caching.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>True if the user can gain XP, false otherwise.</returns>
    public async Task<bool> CanUserGainXpAsync(IGuildUser user, ulong channelId)
    {
        var exclusionCacheKey = $"{RedisKeyPrefix}{ExclusionKey}:{user.GuildId}:{user.Id}:{channelId}";

        // Check Redis cache
        var redisValue = await redisCache.StringGetAsync(exclusionCacheKey).ConfigureAwait(false);
        if (redisValue.HasValue && bool.TryParse(redisValue, out var isExcluded))
        {
            return !isExcluded;
        }

        // Need to check database
        await using var db = await dbFactory.CreateConnectionAsync();

        // Check channel exclusion first (most granular)
        var channelExcluded = await db.XpExcludedItems
            .AnyAsync(x => x.GuildId == user.GuildId &&
                           x.ItemId == channelId &&
                           x.ItemType == (int)ExcludedItemType.Channel)
            .ConfigureAwait(false);

        if (channelExcluded)
        {
            await redisCache.StringSetAsync(exclusionCacheKey, "true", ExclusionTtl).ConfigureAwait(false);
            return false;
        }

        // Check user exclusion
        var userExcluded = await db.XpExcludedItems
            .AnyAsync(x => x.GuildId == user.GuildId &&
                           x.ItemId == user.Id &&
                           x.ItemType == (int)ExcludedItemType.User)
            .ConfigureAwait(false);

        if (userExcluded)
        {
            await redisCache.StringSetAsync(exclusionCacheKey, "true", ExclusionTtl).ConfigureAwait(false);
            return false;
        }

        // Check role exclusions
        var excludedRoles = await db.XpExcludedItems
            .Where(x => x.GuildId == user.GuildId && x.ItemType == (int)ExcludedItemType.Role)
            .Select(x => x.ItemId)
            .ToListAsync()
            .ConfigureAwait(false);

        if (user.RoleIds.Any(r => excludedRoles.Contains(r)))
        {
            await redisCache.StringSetAsync(exclusionCacheKey, "true", ExclusionTtl).ConfigureAwait(false);
            return false;
        }

        // User is not excluded
        await redisCache.StringSetAsync(exclusionCacheKey, "false", ExclusionTtl).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    ///     Checks and sets the XP cooldown for a user.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="cooldownSeconds">The cooldown in seconds.</param>
    /// <returns>True if the user is not on cooldown, false otherwise.</returns>
    public async Task<bool> CheckAndSetCooldownAsync(ulong guildId, ulong userId, int cooldownSeconds)
    {
        var cooldownKey = $"{RedisKeyPrefix}{CooldownKey}:{guildId}:{userId}";

        // Use Redis SETNX for atomic check-and-set
        var notOnCooldown = await redisCache.StringSetAsync(
            cooldownKey,
            "1",
            TimeSpan.FromSeconds(cooldownSeconds),
            When.NotExists).ConfigureAwait(false);

        return notOnCooldown;
    }

    /// <summary>
    ///     Cleans up expired caches in batches to reduce system impact.
    /// </summary>
    /// <returns>A tuple containing the number of keys removed and examined.</returns>
    public async Task<(int keysRemoved, int keysExamined)> CleanupCachesAsync()
    {
        var keysRemoved = 0;
        var keysExamined = 0;

        try
        {
            // Get a Redis server for scanning
            var server = dataCache.Redis.GetServer(dataCache.Redis.GetEndPoints().First());

            // Clear hot cache periodically to prevent unbounded growth
            if (hotGuildCache.Count > 400)
            {
                // Remove only expired entries instead of clearing all
                hotGuildCache.Compact(0.25);
                logger.LogInformation("Compacted hot guild cache");
            }

            // Process Redis keys in batches to prevent long-running operations
            var disconnectedStats = await ProcessDisconnectedGuildsAsync(server).ConfigureAwait(false);
            var multiplierStats = await RefreshMultiplierCachesAsync(server).ConfigureAwait(false);

            keysRemoved = disconnectedStats.keysRemoved + multiplierStats.keysRemoved;
            keysExamined = disconnectedStats.keysExamined + multiplierStats.keysExamined;

            logger.LogInformation("Cache cleanup stats: {Removed} keys removed, {Examined} keys examined",
                keysRemoved, keysExamined);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up caches");
        }

        return (keysRemoved, keysExamined);
    }

    /// <summary>
    ///     Processes guilds that are no longer connected.
    /// </summary>
    /// <param name="server">The Redis server.</param>
    /// <returns>A tuple containing the number of keys removed and examined.</returns>
    private async Task<(int keysRemoved, int keysExamined)> ProcessDisconnectedGuildsAsync(IServer server)
    {
        var keysRemoved = 0;
        var keysExamined = 0;

        // Build list of connected guild IDs for quick lookup
        var connectedGuildIds = new HashSet<ulong>(
            client.Guilds.Select(g => g.Id));

        // Process guild settings keys in batches
        var keysToDelete = new List<RedisKey>();
        var pattern = $"{RedisKeyPrefix}{GuildSettingsKey}:*";

        await foreach (var key in server.KeysAsync(pattern: pattern))
        {
            keysExamined++;

            var keyString = key.ToString();
            if (string.IsNullOrEmpty(keyString)) continue;

            var guildIdString = keyString.Split(':').LastOrDefault();
            if (string.IsNullOrEmpty(guildIdString) || !ulong.TryParse(guildIdString, out var guildId))
                continue;

            if (connectedGuildIds.Contains(guildId)) continue;
            keysToDelete.Add(key);

            // Remove from hot cache too
            hotGuildCache.Remove($"guild:{guildId}");

            // Process in batches
            if (keysToDelete.Count < MaxBatchSize) continue;
            await redisCache.KeyDeleteAsync(keysToDelete.ToArray()).ConfigureAwait(false);
            keysRemoved += keysToDelete.Count;
            logger.LogInformation("Deleted {Count} Redis keys for disconnected guilds", keysToDelete.Count);
            keysToDelete.Clear();
        }

        // Delete any remaining keys
        if (keysToDelete.Count <= 0) return (keysRemoved, keysExamined);
        await redisCache.KeyDeleteAsync(keysToDelete.ToArray()).ConfigureAwait(false);
        keysRemoved += keysToDelete.Count;
        logger.LogInformation("Deleted {Count} Redis keys for disconnected guilds", keysToDelete.Count);

        return (keysRemoved, keysExamined);
    }

    /// <summary>
    ///     Refreshes multiplier caches to ensure they don't expire.
    /// </summary>
    /// <param name="server">The Redis server.</param>
    /// <returns>A tuple containing the number of keys refreshed and examined.</returns>
    private async Task<(int keysRemoved, int keysExamined)> RefreshMultiplierCachesAsync(IServer server)
    {
        var keysRefreshed = 0;
        var keysExamined = 0;

        // Process multiplier keys to ensure they have proper expiration
        var keysToRefresh = new List<RedisKey>();
        var pattern = $"{RedisKeyPrefix}{MultiplierKey}:*";

        await foreach (var key in server.KeysAsync(pattern: pattern))
        {
            keysExamined++;
            keysToRefresh.Add(key);

            // Process in batches
            if (keysToRefresh.Count < MaxBatchSize) continue;
            var tasks = keysToRefresh.Select(redisKey => redisCache.KeyExpireAsync(redisKey, MultiplierTtl)).ToArray();
            await Task.WhenAll(tasks).ConfigureAwait(false);
            keysRefreshed += keysToRefresh.Count;

            keysToRefresh.Clear();
        }

        // Process any remaining keys
        if (keysToRefresh.Count <= 0) return (keysRefreshed, keysExamined);
        {
            var tasks = keysToRefresh.Select(key => redisCache.KeyExpireAsync(key, MultiplierTtl)).ToArray();
            await Task.WhenAll(tasks).ConfigureAwait(false);
            keysRefreshed += keysToRefresh.Count;
        }

        return (keysRefreshed, keysExamined);
    }

    /// <summary>
    ///     Clears all XP-related caches for a specific guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ClearGuildXpCachesAsync(ulong guildId)
    {
        try
        {
            // Clear hot cache
            hotGuildCache.Remove($"guild:{guildId}");

            // Get the Redis server instance
            var server = dataCache.Redis.GetServer(dataCache.Redis.GetEndPoints().First());

            // Define patterns for Redis keys to remove
            var patterns = new[]
            {
                $"{RedisKeyPrefix}{GuildUserKey}:{guildId}:*", $"{RedisKeyPrefix}{GuildSettingsKey}:{guildId}",
                $"{RedisKeyPrefix}{ExclusionKey}:{guildId}:*", $"{RedisKeyPrefix}{MultiplierKey}:{guildId}:*",
                $"{RedisKeyPrefix}{CooldownKey}:{guildId}:*", $"{RedisKeyPrefix}{FirstMsgKey}:{guildId}:*",
                $"{RedisKeyPrefix}leaderboard:{guildId}:*", $"{RedisKeyPrefix}leaderboard:count:{guildId}"
            };

            // Gather all keys to delete
            var redisKeysToDelete = new List<RedisKey>();

            foreach (var pattern in patterns)
            {
                await foreach (var key in server.KeysAsync(pattern: pattern))
                {
                    redisKeysToDelete.Add(key);

                    // Delete in batches to avoid large operations
                    if (redisKeysToDelete.Count >= MaxBatchSize)
                    {
                        await redisCache.KeyDeleteAsync(redisKeysToDelete.ToArray()).ConfigureAwait(false);
                        logger.LogInformation("Deleted {Count} Redis keys for guild {GuildId}",
                            redisKeysToDelete.Count, guildId);
                        redisKeysToDelete.Clear();
                    }
                }
            }

            // Delete any remaining keys
            if (redisKeysToDelete.Count > 0)
            {
                await redisCache.KeyDeleteAsync(redisKeysToDelete.ToArray()).ConfigureAwait(false);
                logger.LogInformation("Deleted {Count} remaining Redis keys for guild {GuildId}",
                    redisKeysToDelete.Count, guildId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing XP caches for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Invalidates the leaderboard cache for a specific guild.
    ///     This is called after XP updates to ensure leaderboard reflects changes immediately.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvalidateLeaderboardCacheAsync(ulong guildId)
    {
        try
        {
            // Get the Redis server instance
            var server = dataCache.Redis.GetServer(dataCache.Redis.GetEndPoints().First());

            // Define patterns for leaderboard cache keys
            var patterns = new[]
            {
                $"{RedisKeyPrefix}leaderboard:{guildId}:*", $"{RedisKeyPrefix}leaderboard:count:{guildId}"
            };

            // Gather all keys to delete
            var redisKeysToDelete = new List<RedisKey>();

            foreach (var pattern in patterns)
            {
                await foreach (var key in server.KeysAsync(pattern: pattern))
                {
                    redisKeysToDelete.Add(key);
                }
            }

            // Delete all matching keys in one operation
            if (redisKeysToDelete.Count > 0)
            {
                await redisCache.KeyDeleteAsync(redisKeysToDelete.ToArray()).ConfigureAwait(false);
                logger.LogDebug("Invalidated {Count} leaderboard cache keys for guild {GuildId}",
                    redisKeysToDelete.Count, guildId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error invalidating leaderboard cache for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Load role rewards into redis on start.
    /// </summary>
    private async Task PreloadRoleRewardsAsync()
    {
        try
        {
            logger.LogInformation("Starting role reward cache preload...");
            var startTime = DateTime.UtcNow;

            await using var db = await dbFactory.CreateConnectionAsync();

            var allRoleRewards = await db.XpRoleRewards.ToListAsync().ConfigureAwait(false);

            if (allRoleRewards.Count == 0)
            {
                logger.LogInformation("No role rewards found to preload");
                return;
            }

            logger.LogInformation("Preloading {Count} role rewards into cache", allRoleRewards.Count);

            var redis = GetRedisDatabase();
            var cacheOperations = new List<Task>();
            const int batchSize = 100;

            for (var i = 0; i < allRoleRewards.Count; i += batchSize)
            {
                var batch = allRoleRewards.Skip(i).Take(batchSize);
                cacheOperations.AddRange(from roleReward in batch
                    let cacheKey = $"{RedisKeyPrefix}rewards:{roleReward.GuildId}:role:{roleReward.Level}"
                    let serializedReward = JsonSerializer.Serialize(roleReward, CachedJsonOptions)
                    select redis.StringSetAsync(cacheKey, serializedReward, null, When.Always));

                await Task.WhenAll(cacheOperations).ConfigureAwait(false);
                cacheOperations.Clear();

                if (i + batchSize < allRoleRewards.Count)
                {
                    await Task.Delay(10).ConfigureAwait(false);
                }
            }

            var elapsedTime = DateTime.UtcNow - startTime;
            logger.LogInformation("Successfully preloaded {Count} role rewards into cache in {ElapsedMs}ms",
                allRoleRewards.Count, elapsedTime.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error preloading role rewards into cache");
        }
    }

    /// <summary>
    ///     Loads currency rewards on start to redis.
    /// </summary>
    private async Task PreloadCurrencyRewardsAsync()
    {
        try
        {
            logger.LogInformation("Starting currency reward cache preload...");
            var startTime = DateTime.UtcNow;

            await using var db = await dbFactory.CreateConnectionAsync();

            // Get all currency rewards from database
            var allCurrencyRewards = await db.XpCurrencyRewards.ToListAsync().ConfigureAwait(false);

            if (allCurrencyRewards.Count == 0)
            {
                logger.LogInformation("No currency rewards found to preload");
                return;
            }

            logger.LogInformation("Preloading {Count} currency rewards into cache", allCurrencyRewards.Count);

            var redis = GetRedisDatabase();
            var cacheOperations = new List<Task>();
            const int batchSize = 100;

            for (var i = 0; i < allCurrencyRewards.Count; i += batchSize)
            {
                var batch = allCurrencyRewards.Skip(i).Take(batchSize);

                cacheOperations.AddRange(from currencyReward in batch
                    let cacheKey = $"{RedisKeyPrefix}rewards:{currencyReward.GuildId}:currency:{currencyReward.Level}"
                    let serializedReward = JsonSerializer.Serialize(currencyReward, CachedJsonOptions)
                    select redis.StringSetAsync(cacheKey, serializedReward, null, When.Always));

                await Task.WhenAll(cacheOperations).ConfigureAwait(false);
                cacheOperations.Clear();

                if (i + batchSize < allCurrencyRewards.Count)
                {
                    await Task.Delay(10).ConfigureAwait(false);
                }
            }

            var elapsedTime = DateTime.UtcNow - startTime;
            logger.LogInformation("Successfully preloaded {Count} currency rewards into cache in {ElapsedMs}ms",
                allCurrencyRewards.Count, elapsedTime.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error preloading currency rewards into cache");
        }
    }

    /// <summary>
    ///     Combined method to preload all reward caches
    /// </summary>
    public async Task PreloadAllRewardCachesAsync()
    {
        logger.LogInformation("Starting reward cache preload...");
        var startTime = DateTime.UtcNow;

        await Task.WhenAll(
            PreloadRoleRewardsAsync(),
            PreloadCurrencyRewardsAsync()
        ).ConfigureAwait(false);

        var elapsedTime = DateTime.UtcNow - startTime;
        logger.LogInformation("Completed reward cache preload in {ElapsedMs}ms", elapsedTime.TotalMilliseconds);
    }
}