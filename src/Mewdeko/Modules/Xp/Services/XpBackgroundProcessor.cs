using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Data;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Xp.Events;
using Mewdeko.Modules.Xp.Models;
using StackExchange.Redis;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     Handles background processing for XP-related tasks.
/// </summary>
public class XpBackgroundProcessor : INService, IDisposable
{
    // Constants for processing configuration
    private const int QueueCapacity = 50000;
    private const int OptimalBatchSize = 500;
    private const int MaxConcurrentDbOps = 16;
    private const int DbMaxRetries = 3;
    private const int MaxBatchingBufferSize = 10000;

    private readonly NonBlocking.ConcurrentDictionary<ulong, byte> activeUserIds = new();

    private readonly NonBlocking.ConcurrentDictionary<(ulong GuildId, ulong UserId), List<XpGainItem>> batchingBuffer =
        new();

    private readonly XpCacheManager cacheManager;
    private readonly Timer cleanupTimer;
    private readonly DiscordShardedClient client;
    private readonly XpCompetitionManager competitionManager;
    private readonly IDataConnectionFactory dbFactory;
    private readonly SemaphoreSlim dbThrottle;
    private readonly Timer decayTimer;
    private readonly EventHandler eventHandler;
    private readonly ILogger<XpBackgroundProcessor> logger;
    private readonly TimeSpan maxProcessingInterval = TimeSpan.FromSeconds(5);
    private readonly Timer memoryMonitorTimer;

    // Processing parameters
    private readonly TimeSpan minProcessingInterval = TimeSpan.FromMilliseconds(200);

    // Timers
    private readonly Timer processingTimer;
    private readonly NonBlocking.ConcurrentDictionary<ulong, DateTime> recentlyActiveUsers = new();

    // Multi-threaded queue for XP updates with reduced capacity
    private readonly BlockingCollection<XpGainItem> xpQueue;
    private TimeSpan currentProcessingInterval = TimeSpan.FromSeconds(1);

    private volatile int isProcessing;
    private DateTime lastActiveUserCleanup = DateTime.UtcNow;

    // Processing state
    private long totalProcessed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpBackgroundProcessor" /> class.
    /// </summary>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="cacheManager">The cache manager for XP operations.</param>
    /// <param name="rewardManager">The reward manager for handling XP rewards.</param>
    /// <param name="competitionManager">The competition manager for handling XP competitions.</param>
    /// <param name="client">The Discord sharded client.</param>
    public XpBackgroundProcessor(
        IDataConnectionFactory dbFactory,
        XpCacheManager cacheManager,
        XpCompetitionManager competitionManager, DiscordShardedClient client, EventHandler eventHandler,
        ILogger<XpBackgroundProcessor> logger)
    {
        this.dbFactory = dbFactory;
        this.cacheManager = cacheManager;
        this.competitionManager = competitionManager;
        this.client = client;
        this.eventHandler = eventHandler;
        this.logger = logger;

        // Initialize thread-safe bounded queue with reduced capacity
        xpQueue = new BlockingCollection<XpGainItem>(QueueCapacity);

        // Initialize database throttling with optimal concurrency
        dbThrottle = new SemaphoreSlim(MaxConcurrentDbOps, MaxConcurrentDbOps);

        // Initialize timers
        processingTimer = new Timer(ProcessXpBatches, null, TimeSpan.FromSeconds(1), currentProcessingInterval);
        decayTimer = new Timer(ProcessXpDecay, null, TimeSpan.FromHours(12), TimeSpan.FromHours(12));
        cleanupTimer = new Timer(CleanupCaches, null, TimeSpan.FromHours(2), TimeSpan.FromHours(2));
        memoryMonitorTimer = new Timer(LogMemoryStats, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        logger.LogInformation(
            "XP Background Processor initialized with queue capacity {Capacity} and {Concurrency} concurrent DB operations",
            QueueCapacity, MaxConcurrentDbOps);

        // Start background consumer task
        Task.Run(BackgroundConsumer);
    }

    /// <summary>
    ///     Disposes resources used by the XP background processor.
    /// </summary>
    public void Dispose()
    {
        try
        {
            // Mark queue as complete for consumer
            xpQueue.CompleteAdding();

            // Stop timers
            processingTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            decayTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            cleanupTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            memoryMonitorTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            // Process remaining items
            ProcessXpBatches(null);

            // Wait briefly for processing to complete
            Thread.Sleep(500);

            // Dispose resources
            processingTimer.Dispose();
            decayTimer.Dispose();
            cleanupTimer.Dispose();
            memoryMonitorTimer.Dispose();
            dbThrottle.Dispose();
            xpQueue.Dispose();

            logger.LogInformation("XP Background Processor successfully disposed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during XP Background Processor disposal");
        }
    }

    /// <summary>
    ///     Background task that constantly takes items from the blocking queue and adds them to the batching buffer.
    /// </summary>
    private async Task BackgroundConsumer()
    {
        while (!xpQueue.IsCompleted)
        {
            try
            {
                // Take one item at a time from the blocking queue
                if (xpQueue.TryTake(out var item, 100))
                {
                    var key = (item.GuildId, item.UserId);

                    // Check for memory pressure and force cleanup
                    if (batchingBuffer.Count > MaxBatchingBufferSize)
                    {
                        logger.LogWarning("Batching buffer exceeded max size ({MaxSize}), forcing processing",
                            MaxBatchingBufferSize);
                        ProcessXpBatches(null);

                        // If still too large after processing, start dropping items
                        if (batchingBuffer.Count > MaxBatchingBufferSize * 1.5)
                        {
                            logger.LogWarning("Dropping XP item due to memory pressure");
                            continue;
                        }
                    }

                    // Add to batching buffer, grouped by guild and user
                    batchingBuffer.AddOrUpdate(
                        key,
                        _ => [item],
                        (_, existing) =>
                        {
                            lock (existing)
                            {
                                existing.Add(item);
                                return existing;
                            }
                        });

                    // Track activity
                    activeUserIds[item.UserId] = 0;
                    recentlyActiveUsers[item.UserId] = DateTime.UtcNow;
                }

                // Trigger processing if buffer is getting full, regardless of timer
                if (batchingBuffer.Count > OptimalBatchSize * 2 && isProcessing != 1)
                {
                    ProcessXpBatches(null);
                }

                // Yield to other tasks occasionally
                await Task.Delay(1).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in XP background consumer");
            }
        }
    }

    /// <summary>
    ///     Queues an XP gain for processing. Uses backpressure for high load.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="amount">The amount of XP to add.</param>
    /// <param name="channelId">The channel ID where the XP was earned.</param>
    /// <param name="source">The source of the XP gain.</param>
    public void QueueXpGain(ulong guildId, ulong userId, int amount, ulong channelId, XpSource source)
    {
        if (amount <= 0)
            return;

        try
        {
            var item = new XpGainItem
            {
                GuildId = guildId,
                UserId = userId,
                Amount = amount,
                ChannelId = channelId,
                Source = source,
                Timestamp = DateTime.UtcNow
            };

            // Try to add with timeout - if we can't add within 100ms, we aggregate with existing items
            if (!xpQueue.TryAdd(item, 100))
            {
                var key = (guildId, userId);

                // If queue is full, try to combine with existing items in the batching buffer
                if (batchingBuffer.TryGetValue(key, out var existingItems))
                {
                    lock (existingItems)
                    {
                        existingItems.Add(item);
                    }
                }
                else
                {
                    // If we can't add to the queue and no existing batch, log warning
                    if (Random.Shared.Next(100) == 0) // Only log occasionally to prevent log spam
                    {
                        logger.LogWarning("XP queue at capacity ({QueueSize}), using fallback buffering",
                            xpQueue.Count);
                    }

                    // Create new batch (could still fail but last resort)
                    batchingBuffer.TryAdd(key, [item]);
                }
            }

            // If queue is getting too full, adjust processing interval to be more aggressive
            if (!(xpQueue.Count > QueueCapacity * 0.8) || currentProcessingInterval <= minProcessingInterval) return;
            currentProcessingInterval = minProcessingInterval;
            processingTimer.Change(TimeSpan.Zero, currentProcessingInterval);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error queueing XP gain for user {UserId} in guild {GuildId}", userId, guildId);
        }
    }

    /// <summary>
    ///     Processes the first message of the day for a user.
    /// </summary>
    /// <param name="user">The guild user.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ProcessFirstMessageOfDay(SocketGuildUser user, ulong channelId)
    {
        // Quick check for server exclusion
        if (await cacheManager.IsServerExcludedAsync(user.Guild.Id).ConfigureAwait(false))
            return;

        var key = $"xp:first_msg:{user.Guild.Id}:{user.Id}";
        var dateString = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var redis = cacheManager.GetRedisDatabase();

        // Atomic operation: Only set if the key doesn't exist
        var wasSet = await redis.StringSetAsync(
            key,
            dateString,
            TimeSpan.FromDays(2),
            When.NotExists,
            CommandFlags.FireAndForget).ConfigureAwait(false);

        if (wasSet)
        {
            // Award first message bonus
            var settings = await cacheManager.GetGuildXpSettingsAsync(user.Guild.Id).ConfigureAwait(false);
            if (settings.FirstMessageBonus > 0)
            {
                QueueXpGain(
                    user.Guild.Id,
                    user.Id,
                    settings.FirstMessageBonus,
                    channelId,
                    XpSource.FirstMessage
                );
            }
        }
    }

    /// <summary>
    ///     Main processing method for batched XP updates.
    /// </summary>
    /// <param name="state">The state object (not used).</param>
    private async void ProcessXpBatches(object state)
    {
        if (Interlocked.Exchange(ref isProcessing, 1) != 0)
            return;

        var startTime = DateTime.UtcNow;
        var itemsProcessed = 0;

        try
        {
            // Take a snapshot of the current batching buffer to process
            var snapshot = new Dictionary<(ulong, ulong), List<XpGainItem>>();

            foreach (var kvp in batchingBuffer)
            {
                var items = kvp.Value;
                // Skip empty batches
                if (items.Count == 0)
                    continue;

                // Take the batch and replace with empty list atomically
                batchingBuffer.TryUpdate(kvp.Key, [], items);
                snapshot.Add(kvp.Key, items);
            }

            if (snapshot.Count == 0)
            {
                // If no items to process, adjust interval to be less aggressive
                if (currentProcessingInterval >= maxProcessingInterval) return;
                currentProcessingInterval = TimeSpan.FromMilliseconds(
                    Math.Min(maxProcessingInterval.TotalMilliseconds,
                        currentProcessingInterval.TotalMilliseconds * 1.5));
                processingTimer.Change(currentProcessingInterval, currentProcessingInterval);

                return;
            }

            // Group by guild for efficient processing
            var guildGroups = snapshot
                .SelectMany(kvp => kvp.Value.Select(item => (item.GuildId, item, kvp.Key)))
                .GroupBy(x => x.GuildId)
                .Select(g => (GuildId: g.Key, Items: g.Select(x => (x.item, x.Key)).ToList()))
                .ToList();

            // Process each guild's data in parallel with throttling
            await Task.WhenAll(guildGroups.Select(async group =>
            {
                // Wait for available DB connection slot
                await dbThrottle.WaitAsync().ConfigureAwait(false);
                try
                {
                    // Process this guild's XP updates
                    var processed = await ProcessGuildXpBatch(group.GuildId, group.Items).ConfigureAwait(false);
                    Interlocked.Add(ref itemsProcessed, processed);
                }
                finally
                {
                    dbThrottle.Release();
                }
            })).ConfigureAwait(false);

            // Update statistics
            Interlocked.Add(ref totalProcessed, itemsProcessed);

            // Adjust processing interval based on load and performance
            AdjustProcessingInterval(startTime, itemsProcessed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing XP batches");
        }
        finally
        {
            // Reset processing flag - use 0 to indicate not processing
            Interlocked.Exchange(ref isProcessing, 0);
        }
    }

    /// <summary>
    ///     Adjusts the processing interval based on load and performance.
    /// </summary>
    /// <param name="startTime">The time processing started.</param>
    /// <param name="itemsProcessed">The number of items processed.</param>
    private void AdjustProcessingInterval(DateTime startTime, int itemsProcessed)
    {
        if (itemsProcessed == 0)
            return;

        var processingTime = DateTime.UtcNow - startTime;
        var queueSize = xpQueue.Count;

        // Calculate optimal interval based on current conditions
        TimeSpan targetInterval;

        if (queueSize > QueueCapacity * 0.5)
        {
            // High load - process more frequently
            targetInterval = minProcessingInterval;
        }
        else if (queueSize > QueueCapacity * 0.2)
        {
            // Medium load
            targetInterval = TimeSpan.FromMilliseconds(Math.Min(1000, processingTime.TotalMilliseconds * 2));
        }
        else
        {
            // Light load - conserve resources
            targetInterval = TimeSpan.FromMilliseconds(Math.Min(maxProcessingInterval.TotalMilliseconds,
                processingTime.TotalMilliseconds * 5));
        }

        // Only change timer if interval changes significantly
        if (!(Math.Abs((targetInterval - currentProcessingInterval).TotalMilliseconds) > 200)) return;
        currentProcessingInterval = targetInterval;
        processingTimer.Change(currentProcessingInterval, currentProcessingInterval);
    }

    /// <summary>
    ///     Processes XP updates for a single guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="items">The list of items to process with their keys.</param>
    /// <returns>The number of items processed.</returns>
    private async Task<int> ProcessGuildXpBatch(ulong guildId,
        List<(XpGainItem Item, (ulong GuildId, ulong UserId) Key)> items)
    {
        // Skip if no items
        if (items.Count == 0)
            return 0;

        try
        {
            // Get a single DB connection for the entire batch
            await using var db = await dbFactory.CreateConnectionAsync();

            // Get guild settings from cache manager
            var settings = await cacheManager.GetGuildXpSettingsAsync(guildId).ConfigureAwait(false);

            // Skip if XP is disabled for this guild
            if (settings.XpGainDisabled)
                return 0;

            // Get active competitions
            var activeCompetitions = await competitionManager.GetActiveCompetitionsAsync(guildId).ConfigureAwait(false);

            // Aggregation step: Sum XP gains by user and track last items
            var userXpAggregation = new Dictionary<ulong, (int TotalXp, XpGainItem LastItem, List<XpSource> Sources)>();

            foreach (var (item, _) in items)
            {
                if (!userXpAggregation.TryGetValue(item.UserId, out var existing))
                {
                    userXpAggregation[item.UserId] = (item.Amount, item, [item.Source]);
                }
                else
                {
                    // Sum XP and keep track of last message per timestamp
                    var sources = existing.Sources;
                    if (!sources.Contains(item.Source))
                    {
                        sources.Add(item.Source);
                    }

                    userXpAggregation[item.UserId] = (
                        existing.TotalXp + item.Amount,
                        item.Timestamp > existing.LastItem.Timestamp ? item : existing.LastItem,
                        sources);
                }
            }

            // Prepare result collections
            var competitionUpdates = new List<CompetitionUpdateItem>();

            // Get all users in one query
            var userIds = userXpAggregation.Keys.ToList();
            var existingUserXp = await db.GuildUserXps
                .Where(x => x.GuildId == guildId && userIds.Contains(x.UserId))
                .ToDictionaryAsync(k => k.UserId, v => v)
                .ConfigureAwait(false);

            // Prepare collections for DB operations
            var insertRecords = new List<GuildUserXp>();
            var updateRecords = new List<GuildUserXp>();
            var now = DateTime.UtcNow;

            // Process each user
            foreach (var (userId, (totalXp, lastItem, sources)) in userXpAggregation)
            {
                try
                {
                    var sourcesText = string.Join(", ", sources.Select(s => s.ToString()));

                    // Check if user exists
                    if (existingUserXp.TryGetValue(userId, out var userXp))
                    {
                        // Update existing user
                        var oldLevel = XpCalculator.CalculateLevel(userXp.TotalXp, (XpCurveType)settings.XpCurveType);

                        // Update XP and last activity
                        userXp.TotalXp += totalXp;
                        userXp.LastActivity = now;

                        // Calculate new level
                        var newLevel = XpCalculator.CalculateLevel(userXp.TotalXp, (XpCurveType)settings.XpCurveType);

                        // Handle level up
                        if (newLevel > oldLevel)
                        {
                            userXp.LastLevelUp = now;

                            // Publish level change event
                            _ = eventHandler.PublishEventAsync("XpLevelChanged", new XpLevelChangedEventArgs
                            {
                                GuildId = guildId,
                                UserId = userId,
                                OldLevel = oldLevel,
                                NewLevel = newLevel,
                                TotalXp = userXp.TotalXp,
                                ChannelId = lastItem.ChannelId,
                                Source = lastItem.Source,
                                IsLevelUp = true,
                                NotificationType = (XpNotificationType)userXp.NotifyType
                            });
                        }

                        // Add to update collection
                        updateRecords.Add(userXp);

                        // Add competition updates
                        if (activeCompetitions.Count <= 0) continue;
                        competitionUpdates.AddRange(activeCompetitions.Select(competition => new CompetitionUpdateItem
                        {
                            CompetitionId = competition.Id, UserId = userId, XpGained = totalXp, CurrentLevel = newLevel
                        }));
                    }
                    else
                    {
                        // Create new user record
                        var newUserXp = new GuildUserXp
                        {
                            GuildId = guildId,
                            UserId = userId,
                            TotalXp = totalXp,
                            LastActivity = now,
                            LastLevelUp = now,
                            NotifyType = (int)XpNotificationType.None
                        };

                        // Calculate level
                        var newLevel =
                            XpCalculator.CalculateLevel(newUserXp.TotalXp, (XpCurveType)settings.XpCurveType);

                        // Handle initial level for new user
                        if (newLevel > 0)
                        {
                            // Publish level change event for new user
                            _ = eventHandler.PublishEventAsync("XpLevelChanged", new XpLevelChangedEventArgs
                            {
                                GuildId = guildId,
                                UserId = userId,
                                OldLevel = 0,
                                NewLevel = newLevel,
                                TotalXp = newUserXp.TotalXp,
                                ChannelId = lastItem.ChannelId,
                                Source = lastItem.Source,
                                IsLevelUp = true,
                                NotificationType =
                                    XpNotificationType.None // New users don't get notifications by default
                            });
                        }

                        // Add to insert collection
                        insertRecords.Add(newUserXp);

                        // Add competition updates
                        if (activeCompetitions.Count <= 0) continue;
                        competitionUpdates.AddRange(activeCompetitions.Select(competition => new CompetitionUpdateItem
                        {
                            CompetitionId = competition.Id, UserId = userId, XpGained = totalXp, CurrentLevel = newLevel
                        }));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing XP for user {UserId} in guild {GuildId}", userId, guildId);
                }
            }

            // Execute database operations with retry
            await ExecuteDatabaseOperationsWithRetry(db, guildId, insertRecords, updateRecords).ConfigureAwait(false);

            return items.Count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing guild XP batch for {GuildId}", guildId);
            return 0;
        }
    }

    /// <summary>
    ///     Executes database operations with retry logic for reliability.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="insertRecords">Records to insert.</param>
    /// <param name="updateRecords">Records to update.</param>
    /// <returns>A task representing the operation.</returns>
    private async Task ExecuteDatabaseOperationsWithRetry(
        MewdekoDb db,
        ulong guildId,
        List<GuildUserXp> insertRecords,
        List<GuildUserXp> updateRecords)
    {
        // Process inserts
        if (insertRecords.Count > 0)
        {
            for (var attempt = 0; attempt < DbMaxRetries; attempt++)
            {
                try
                {
                    // Process in optimal batch sizes
                    for (var i = 0; i < insertRecords.Count; i += OptimalBatchSize)
                    {
                        var chunk = insertRecords.Skip(i).Take(OptimalBatchSize).ToList();
                        await db.BulkCopyAsync(chunk).ConfigureAwait(false);

                        // Update cache
                        foreach (var record in chunk)
                        {
                            await cacheManager.UpdateUserXpCacheAsync(record).ConfigureAwait(false);
                        }
                    }

                    break;
                }
                catch (Exception ex)
                {
                    if (attempt == DbMaxRetries - 1)
                    {
                        logger.LogError(ex, "Failed to insert XP records for guild {GuildId} after {Attempts} attempts",
                            guildId, DbMaxRetries);
                    }
                    else
                    {
                        await Task.Delay(50 * (attempt + 1)).ConfigureAwait(false);
                    }
                }
            }
        }

        // Process updates
        if (updateRecords.Count > 0)
        {
            for (var attempt = 0; attempt < DbMaxRetries; attempt++)
            {
                try
                {
                    for (var i = 0; i < updateRecords.Count; i += OptimalBatchSize)
                    {
                        var chunk = updateRecords.Skip(i).Take(OptimalBatchSize).ToList();

                        await db.GuildUserXps
                            .Merge()
                            .Using(chunk.AsQueryable())
                            .OnTargetKey()
                            .UpdateWhenMatched((target, source) => new GuildUserXp
                            {
                                TotalXp = source.TotalXp,
                                LastActivity = source.LastActivity,
                                LastLevelUp = source.LastLevelUp,
                                NotifyType = source.NotifyType
                            })
                            .MergeAsync()
                            .ConfigureAwait(false);

                        // Update cache
                        foreach (var record in chunk)
                        {
                            await cacheManager.UpdateUserXpCacheAsync(record).ConfigureAwait(false);
                        }
                    }

                    break;
                }
                catch (Exception ex)
                {
                    if (attempt == DbMaxRetries - 1)
                    {
                        logger.LogError(ex, "Failed to update XP records for guild {GuildId} after {Attempts} attempts",
                            guildId, DbMaxRetries);
                    }
                    else
                    {
                        await Task.Delay(50 * (attempt + 1)).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Processes XP decay for inactive users.
    /// </summary>
    /// <param name="state">The state object (not used).</param>
    private async void ProcessXpDecay(object state)
    {
        try
        {
            logger.LogInformation("Starting XP decay processing");
            var startTime = DateTime.UtcNow;
            var totalGuildsProcessed = 0;
            var totalUsersDecayed = 0;

            await using var db = await dbFactory.CreateConnectionAsync();

            // Get guilds with decay enabled
            var guildsWithDecay = await db.GuildXpSettings
                .Where(g => g.EnableXpDecay)
                .ToListAsync()
                .ConfigureAwait(false);

            if (guildsWithDecay.Count == 0)
                return;

            logger.LogInformation("Processing XP decay for {Count} guilds with decay enabled", guildsWithDecay.Count);

            // Process guilds sequentially with throttling
            foreach (var settings in guildsWithDecay)
            {
                await dbThrottle.WaitAsync().ConfigureAwait(false);
                try
                {
                    var inactiveThreshold = DateTime.UtcNow.AddDays(-settings.InactivityDaysBeforeDecay);
                    var guildDecayed = 0;

                    // Process in batches to avoid memory issues with large guilds
                    const int decayBatchSize = 1000;
                    var hasMoreRecords = true;
                    var lastUserId = 0UL;

                    while (hasMoreRecords)
                    {
                        // Get batch of inactive users using LinqToDB
                        var id = lastUserId;
                        var inactiveUsers = await db.GuildUserXps
                            .Where(x =>
                                x.GuildId == settings.GuildId &&
                                x.LastActivity < inactiveThreshold &&
                                x.UserId > id)
                            .OrderBy(x => x.UserId)
                            .Take(decayBatchSize)
                            .ToListAsync()
                            .ConfigureAwait(false);

                        hasMoreRecords = inactiveUsers.Count == decayBatchSize;

                        if (inactiveUsers.Count == 0)
                            break;

                        // Update last user ID for pagination
                        lastUserId = inactiveUsers[^1].UserId;

                        // Apply decay in memory
                        foreach (var user in inactiveUsers)
                        {
                            var decayAmount = (long)(user.TotalXp * (settings.DailyDecayPercentage / 100.0));
                            if (decayAmount <= 0) continue;
                            user.TotalXp -= decayAmount;

                            // Ensure total XP doesn't go below 0
                            if (user.TotalXp < 0)
                                user.TotalXp = 0;

                            guildDecayed++;
                        }

                        // Use BulkCopy for the update
                        await db.BulkCopyAsync(inactiveUsers).ConfigureAwait(false);

                        // Update cache for each decayed user
                        foreach (var user in inactiveUsers)
                        {
                            await cacheManager.UpdateUserXpCacheAsync(user).ConfigureAwait(false);
                        }
                    }

                    if (guildDecayed > 0)
                    {
                        totalGuildsProcessed++;
                        totalUsersDecayed += guildDecayed;
                        logger.LogInformation("Applied XP decay to {Count} inactive users in guild {GuildId}",
                            guildDecayed, settings.GuildId);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing XP decay for guild {GuildId}", settings.GuildId);
                }
                finally
                {
                    dbThrottle.Release();
                }
            }

            var elapsedTime = DateTime.UtcNow - startTime;
            logger.LogInformation("XP decay processing completed in {ElapsedTime}s. " +
                                  "Processed {Guilds} guilds and decayed {Users} users.",
                elapsedTime.TotalSeconds, totalGuildsProcessed, totalUsersDecayed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in XP decay processing");
        }
    }

    /// <summary>
    ///     Cleans up expired cache entries.
    /// </summary>
    /// <param name="state">The state object (not used).</param>
    private async void CleanupCaches(object state)
    {
        try
        {
            logger.LogDebug("Running cache cleanup task");

            // Clean up Redis caches
            var cleanupStats = await cacheManager.CleanupCachesAsync().ConfigureAwait(false);

            // Clean up active user tracking to prevent memory leaks
            var timeSinceLastCleanup = DateTime.UtcNow - lastActiveUserCleanup;
            if (timeSinceLastCleanup > TimeSpan.FromHours(4) || activeUserIds.Count > 50000)
            {
                var oldCount = activeUserIds.Count;

                // Clean inactive users
                var currentTime = DateTime.UtcNow;
                var inactiveUsers = recentlyActiveUsers
                    .Where(pair => (currentTime - pair.Value).TotalHours > 1)
                    .Select(pair => pair.Key)
                    .ToList();

                foreach (var userId in inactiveUsers)
                {
                    activeUserIds.TryRemove(userId, out _);
                    recentlyActiveUsers.TryRemove(userId, out _);
                }

                lastActiveUserCleanup = DateTime.UtcNow;

                logger.LogDebug("Cleared {Count} inactive users from tracking (had {OldCount} entries, now {NewCount})",
                    inactiveUsers.Count, oldCount, activeUserIds.Count);
            }

            // Log statistics
            logger.LogInformation("XP Background Processor stats: Queue size: {QueueSize}, " +
                                  "Total processed: {TotalProcessed}, Active users tracked: {ActiveUsers}, " +
                                  "Redis cache entries cleaned: {RedisCachesCleared}",
                xpQueue.Count, totalProcessed, activeUserIds.Count, cleanupStats.keysRemoved);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up caches");
        }
    }

    /// <summary>
    ///     Logs memory statistics and current system status.
    /// </summary>
    /// <param name="state">The state object (not used).</param>
    private void LogMemoryStats(object state)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var memoryMb = process.WorkingSet64 / 1024 / 1024;
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);

            // Get Discord cache stats
            var cachedUsers = 0;
            var cachedMessages = 0;
            var cachedChannels = 0;

            foreach (var guild in client.Guilds)
            {
                cachedUsers += guild.Users.Count;
                cachedChannels += guild.Channels.Count;

                foreach (var channel in guild.TextChannels)
                {
                    if (channel is { } textChannel)
                    {
                        cachedMessages += textChannel.CachedMessages.Count;
                    }
                }
            }

            var bufferItemCount = batchingBuffer.Sum(kvp => kvp.Value.Count);

            logger.LogInformation("XP Processor Memory Stats: Working Set: {MemoryMB}MB, " +
                                  "Queue: {QueueSize}, Batching Buffer Items: {BufferItems} (Keys: {BufferKeys}), " +
                                  "Active Users: {ActiveUsers}, Processing: {IsProcessing}, " +
                                  "GC Gen0: {Gen0}, Gen1: {Gen1}, Gen2: {Gen2}",
                memoryMb, xpQueue.Count, bufferItemCount, batchingBuffer.Count,
                activeUserIds.Count, isProcessing == 1,
                gen0, gen1, gen2);

            logger.LogInformation(
                "Discord Cache: Guilds: {Guilds}, Users: {Users}, Channels: {Channels}, Messages: {Messages}",
                client.Guilds.Count, cachedUsers, cachedChannels, cachedMessages);

            // Force a Gen0 collection to see if memory can be reclaimed
            if (memoryMb > 600)
            {
                logger.LogWarning("High memory usage detected, forcing GC");
                GC.Collect(0, GCCollectionMode.Forced);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error logging memory stats");
        }
    }
}