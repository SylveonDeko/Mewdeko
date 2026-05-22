using System.Data;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Serilog;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Service for counting messages
/// </summary>
public class MessageCountService : INService, IDisposable
{
    /// <summary>
    ///     Whether the query is for a channel, user, or guild
    /// </summary>
    public enum CountQueryType
    {
        /// <summary>
        ///     Guild
        /// </summary>
        Guild,

        /// <summary>
        ///     Channel
        /// </summary>
        Channel,

        /// <summary>
        ///     User
        /// </summary>
        User
    }

    private const int CacheMinutes = 30;
    private const int BatchSize = 100;

    private static readonly AsyncCircuitBreakerPolicy<MessageCount> CircuitBreaker =
        Policy<MessageCount>
            .Handle<Exception>()
            .CircuitBreakerAsync(
                10,
                TimeSpan.FromSeconds(30),
                (ex, duration) =>
                    Log.Error("Circuit breaker opened for {Duration}s due to: {Error}",
                        duration.TotalSeconds, ex.Exception.Message),
                () =>
                    Log.Information("Circuit breaker reset"),
                () =>
                    Log.Information("Circuit breaker half-open"));

    private static readonly IAsyncPolicy<MessageCount> DatabasePolicy =
        Policy<MessageCount>
            .Handle<LinqToDBException>()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    Log.Warning(
                        "Retry {RetryCount} after {Delay}ms for {Key}. Error: {Error}",
                        retryCount,
                        timeSpan.TotalMilliseconds,
                        context["CacheKey"],
                        exception.Exception.Message);
                })
            .WrapAsync(Policy.TimeoutAsync<MessageCount>(TimeSpan.FromSeconds(30)))
            .WrapAsync(Policy.BulkheadAsync<MessageCount>(100, 500));

    private readonly IMemoryCache cache;
    private readonly HashSet<ulong> countGuilds = [];
    private readonly CancellationTokenSource cts = new();

    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<MessageCountService> logger;
    private readonly ConcurrentDictionary<ulong, int> minCounts = [];
    private readonly Channel<(ulong GuildId, ulong ChannelId, ulong UserId, DateTime Timestamp)> updateChannel;
    private readonly SemaphoreSlim updateLock = new(1);

    /// <summary>
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="handler">The handler parameter.</param>
    /// <param name="cache">The cache service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public MessageCountService(IDataConnectionFactory dbFactory, EventHandler handler, IMemoryCache cache,
        ILogger<MessageCountService> logger)
    {
        this.dbFactory = dbFactory;
        this.cache = cache;
        this.logger = logger;
        _ = InitializeGuildSettings();
        handler.Subscribe("MessageReceived", "MessageCountService", HandleCount);
        updateChannel = Channel.CreateUnbounded<(ulong, ulong, ulong, DateTime)>();
        _ = ProcessUpdatesAsync(); // Start background processor
    }

    private async Task InitializeGuildSettings()
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var enabledGuilds = await db.GuildConfigs
                .Where(gc => gc.UseMessageCount)
                .Select(gc => new
                {
                    gc.GuildId, gc.MinMessageLength
                })
                .ToListAsync();

            foreach (var guild in enabledGuilds)
            {
                countGuilds.Add(guild.GuildId);
                minCounts[guild.GuildId] = guild.MinMessageLength;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize guild message count settings");
        }
    }

    private async Task ProcessUpdatesAsync()
    {
        var batch = new List<(ulong, ulong, ulong, DateTime)>();

        try
        {
            while (await updateChannel.Reader.WaitToReadAsync(cts.Token))
            {
                while (batch.Count < BatchSize &&
                       updateChannel.Reader.TryRead(out var update))
                {
                    batch.Add(update);
                }

                if (batch.Count <= 0) continue;
                await ProcessBatchAsync(batch);
                batch.Clear();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ProcessBatchAsync(List<(ulong GuildId, ulong ChannelId, ulong UserId, DateTime Timestamp)> batch)
    {
        await updateLock.WaitAsync();
        try
        {
            var updates = batch.GroupBy(x => (x.GuildId, x.ChannelId, x.UserId))
                .ToDictionary(g => g.Key, g => g.Count());

            // Dictionary to store MessageCount objects that need updating
            var messageCountsToUpdate = new List<MessageCount>();
            var messageCountIds = new Dictionary<(ulong GuildId, ulong ChannelId, ulong UserId), long>();

            // First, process each group to get the corresponding MessageCount
            foreach (var ((guildId, channelId, userId), count) in updates)
            {
                var key = $"msgcount:{guildId}:{channelId}:{userId}";
                var current = await GetOrCreateMessageCountAsync(guildId, channelId, userId);

                // Update the count
                current.Count += (ulong)count;

                // Add to list for database update
                messageCountsToUpdate.Add(current);

                // Update the cache
                cache.Set(key, current, TimeSpan.FromMinutes(CacheMinutes));

                // Store the ID for timestamps
                messageCountIds[(guildId, channelId, userId)] = current.Id;
            }

            // Update the database
            await using var db = await dbFactory.CreateConnectionAsync();

            // Bulk update the message counts
            foreach (var countRecord in messageCountsToUpdate)
            {
                await db.UpdateAsync(countRecord);
            }

            // Create and insert timestamps
            var timestamps = batch.Select(x => new MessageTimestamp
            {
                GuildId = x.GuildId,
                ChannelId = x.ChannelId,
                UserId = x.UserId,
                Timestamp = x.Timestamp,
                MessageCountId = messageCountIds[(x.GuildId, x.ChannelId, x.UserId)]
            }).ToList();

            // Use bulk insert for timestamps
            await db.BulkCopyAsync(timestamps);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing message count batch: {ErrorMessage}", ex.Message);
        }
        finally
        {
            updateLock.Release();
        }
    }

    private async Task HandleCount(SocketMessage msg)
    {
        if (!IsValidMessage(msg))
            return;

        var channel = (IGuildChannel)msg.Channel;
        await updateChannel.Writer.WriteAsync((
            channel.GuildId,
            channel.Id,
            msg.Author.Id,
            msg.Timestamp.UtcDateTime
        ));
    }

    /// <summary>
    ///     Private method to retrieve or create a message count record from cache/database.
    /// </summary>
    /// <param name="guildId">The Discord ID of the guild.</param>
    /// <param name="channelId">The Discord ID of the channel.</param>
    /// <param name="userId">The Discord ID of the user.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The existing or newly created MessageCount record.</returns>
    private async Task<MessageCount> GetOrCreateMessageCountAsync(
        ulong guildId,
        ulong channelId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        var key = $"msgcount:{guildId}:{channelId}:{userId}";

        try
        {
            // Try cache first, outside of retry policy
            if (cache.TryGetValue(key, out MessageCount cachedCount))
            {
                logger.LogDebug("Cache hit for key {Key}", key);
                return cachedCount;
            }

            logger.LogDebug("Cache miss for key {Key}", key);

            // Combine policies
            var policy = DatabasePolicy.WrapAsync(CircuitBreaker);

            var count = await policy.ExecuteAsync(async _ =>
            {
                await using var db = await dbFactory.CreateConnectionAsync(cancellationToken);

                var record = await db.MessageCounts
                    .LoadWithAsTable(x => x.MessageTimestamps)
                    .FirstOrDefaultAsync(x =>
                            x.GuildId == guildId &&
                            x.ChannelId == channelId &&
                            x.UserId == userId,
                        cancellationToken);

                if (record == null)
                {
                    record = new MessageCount
                    {
                        GuildId = guildId, ChannelId = channelId, UserId = userId, Count = 0
                    };

                    record.Id = await db.InsertWithInt32IdentityAsync(record, token: cancellationToken);
                }

                // Cache the result
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(CacheMinutes))
                    .RegisterPostEvictionCallback((k, v, r, s) =>
                        logger.LogDebug("Cache entry {Key} evicted due to {Reason}", k, r));

                cache.Set(key, record, cacheEntryOptions);

                return record;
            }, new Context
            {
                ["CacheKey"] = key
            });

            return count;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to get/create message count for {Key}", key);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError($"This shouldnt happen in message counts: {ex}");
            throw;
        }
    }

    /// <summary>
    ///     Checks if a message is valid for counting based on guild settings and message properties.
    /// </summary>
    /// <param name="message">The Discord message to validate.</param>
    /// <returns>True if the message should be counted, false otherwise.</returns>
    private bool IsValidMessage(SocketMessage message)
    {
        if (countGuilds.Count == 0 ||
            message.Channel is IDMChannel ||
            message.Channel is not IGuildChannel channel ||
            !countGuilds.Contains(channel.GuildId) ||
            message.Author.IsBot)
            return false;

        return !minCounts.TryGetValue(channel.GuildId, out var minValue) ||
               message.Content.Length >= minValue;
    }

    /// <summary>
    ///     Toggles message counting for a guild. If enabled, starts tracking messages.
    ///     If disabled, optionally cleans up existing message data.
    /// </summary>
    /// <param name="guildId">The ID of the guild to toggle message counting for</param>
    /// <returns>True if message counting was enabled, false if it was disabled</returns>
    public async Task<bool> ToggleGuildMessageCount(ulong guildId)
    {
        var wasAdded = false;

        await using var db = await dbFactory.CreateConnectionAsync();
        await using var transaction = await db.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            CancellationToken.None);

        try
        {
            var guildConfig = await db.GuildConfigs
                .Where(x => x.GuildId == guildId)
                .Select(x => new
                {
                    x.UseMessageCount, x.MinMessageLength
                })
                .FirstOrDefaultAsync();

            if (guildConfig == null)
            {
                logger.LogWarning("Attempted to toggle message count for non-existent guild {GuildId}", guildId);
                return false;
            }

            wasAdded = !guildConfig.UseMessageCount;

            if (wasAdded)
            {
                // Adding the guild to the system
                countGuilds.Add(guildId);
                minCounts[guildId] = guildConfig.MinMessageLength;

                // Load existing counts into cache
                var existingCounts = await db.MessageCounts
                    .LoadWithAsTable(x => x.MessageTimestamps)
                    .Where(x => x.GuildId == guildId)
                    .ToListAsync();

                foreach (var count in existingCounts)
                {
                    var key = $"msgcount:{count.GuildId}:{count.ChannelId}:{count.UserId}";
                    cache.Set(key, count, TimeSpan.FromMinutes(CacheMinutes));
                }
            }
            else
            {
                // Removing the guild from the system
                countGuilds.Remove(guildId);
                minCounts.TryRemove(guildId, out _);

                _ = $"msgcount:{guildId}:*";
            }

            await db.GuildConfigs
                .Where(x => x.GuildId == guildId)
                .Set(x => x.UseMessageCount, wasAdded)
                .UpdateAsync();

            await transaction.CommitAsync();
            return wasAdded;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to toggle message count for guild {GuildId}", guildId);

            // Revert memory state if transaction failed
            if (wasAdded)
            {
                countGuilds.Remove(guildId);
                minCounts.TryRemove(guildId, out _);
            }
            else
            {
                countGuilds.Add(guildId);
            }

            return !wasAdded;
        }
    }

    /// <summary>
    ///     Gets an array of message counts for the selected entity type along with a boolean indicating if counting is enabled
    /// </summary>
    /// <param name="queryType">The type of query - can be Guild, Channel, or User level</param>
    /// <param name="snowflakeId">The ID of the entity to query</param>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>A tuple containing the array of message counts and whether counting is enabled</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an invalid query type is provided</exception>
    public async Task<(MessageCount[] Counts, bool Enabled)> GetAllCountsForEntity(
        CountQueryType queryType,
        ulong snowflakeId,
        ulong guildId)
    {
        if (!countGuilds.Contains(guildId))
            return ([], false);

        await using var db = await dbFactory.CreateConnectionAsync();

        // Define the base query
        IQueryable<MessageCount> query;

        // Build query based on query type
        switch (queryType)
        {
            case CountQueryType.Guild:
                query = db.MessageCounts.LoadWithAsTable(x => x.MessageTimestamps).Where(x => x.GuildId == snowflakeId);
                break;
            case CountQueryType.Channel:
                query = db.MessageCounts.LoadWithAsTable(x => x.MessageTimestamps)
                    .Where(x => x.ChannelId == snowflakeId && x.GuildId == guildId);
                break;
            case CountQueryType.User:
                query = db.MessageCounts.LoadWithAsTable(x => x.MessageTimestamps)
                    .Where(x => x.UserId == snowflakeId && x.GuildId == guildId);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(queryType), queryType, null);
        }

        // Execute query to get message counts
        var counts = await query.ToArrayAsync();

        if (counts.Length > 0)
        {
            var timestampQuery = queryType switch
            {
                CountQueryType.Guild => db.MessageTimestamps.Where(x => x.GuildId == snowflakeId),
                CountQueryType.Channel => db.MessageTimestamps.Where(x =>
                    x.ChannelId == snowflakeId && x.GuildId == guildId),
                CountQueryType.User => db.MessageTimestamps.Where(x => x.UserId == snowflakeId && x.GuildId == guildId),
                _ => throw new ArgumentOutOfRangeException(nameof(queryType), queryType, null)
            };

            var timestamps = await timestampQuery.ToListAsync();

            // Group timestamps by their identifiers for efficient lookup
            var timestampGroups = timestamps.GroupBy(
                t => (t.GuildId, t.ChannelId, t.UserId),
                t => t,
                (key, group) => new
                {
                    Key = key, Timestamps = group.ToList()
                }
            ).ToDictionary(g => g.Key, g => g.Timestamps);

            // Assign timestamps to their corresponding message counts
            foreach (var count in counts)
            {
                var key = (count.GuildId, count.ChannelId, count.UserId);
                if (timestampGroups.TryGetValue(key, out var matchingTimestamps))
                {
                    count.MessageTimestamps = matchingTimestamps;
                }
            }
        }

        // Cache the results
        foreach (var count in counts)
        {
            var key = $"msgcount:{count.GuildId}:{count.ChannelId}:{count.UserId}";
            cache.Set(key, count, TimeSpan.FromMinutes(CacheMinutes));
        }

        return (counts, true);
    }

    /// <summary>
    ///     Gets a count for the specified type
    /// </summary>
    /// <param name="queryType">The type of query - can be Guild, Channel, or User level</param>
    /// <param name="guildId">The ID of the guild to query</param>
    /// <param name="snowflakeId">The ID of the entity to query (user/channel ID)</param>
    /// <returns>The total message count for the specified entity</returns>
    /// <exception cref="ArgumentException">Thrown when an invalid query type is provided</exception>
    public async Task<ulong> GetMessageCount(CountQueryType queryType, ulong guildId, ulong snowflakeId)
    {
        if (!countGuilds.Contains(guildId))
            return 0;

        await using var db = await dbFactory.CreateConnectionAsync();

        var query = queryType switch
        {
            CountQueryType.Guild => db.MessageCounts
                .Where(x => x.GuildId == guildId),

            CountQueryType.Channel => db.MessageCounts
                .Where(x => x.ChannelId == snowflakeId && x.GuildId == guildId),

            CountQueryType.User => db.MessageCounts
                .Where(x => x.UserId == snowflakeId && x.GuildId == guildId),

            _ => throw new ArgumentException("Invalid query type", nameof(queryType))
        };

        return (ulong)await query.SumAsync(x => (decimal)x.Count);
    }

    /// <summary>
    ///     Gets the busiest hours for a guild
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="days">Number of days to analyze</param>
    /// <returns>Collection of hours and their message counts</returns>
    public async Task<IEnumerable<(int Hour, int Count)>> GetBusiestHours(ulong guildId, int days = 7)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var startDate = DateTime.UtcNow.AddDays(-Math.Min(days, 30));

        var results = await db.MessageTimestamps
            .Where(t => t.GuildId == guildId && t.Timestamp >= startDate)
            .GroupBy(t => t.Timestamp.Hour)
            .Select(g => new
            {
                Hour = g.Key, Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(24)
            .ToListAsync();

        return results.Select(x => (x.Hour, x.Count));
    }

    /// <summary>
    ///     Gets the busiest days in the guild
    /// </summary>
    /// <param name="guildId">The Discord guild ID</param>
    /// <param name="weeks">Number of weeks to analyze</param>
    /// <returns>Collection of days and their message counts</returns>
    public async Task<IEnumerable<(DayOfWeek Day, int Count)>> GetBusiestDays(ulong guildId, int weeks = 4)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var startDate = DateTime.UtcNow.AddDays(-Math.Min(7 * weeks, 30));

        var results = await db.MessageTimestamps
            .Where(t => t.GuildId == guildId && t.Timestamp >= startDate)
            .GroupBy(t => t.Timestamp.DayOfWeek)
            .Select(g => new
            {
                Day = g.Key, Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        return results.Select(x => (x.Day, x.Count));
    }

    /// <summary>
    ///     Resets message counts for a specific guild, optionally filtered by user and/or channel.
    ///     Removes both the count records and associated timestamps.
    /// </summary>
    /// <param name="guildId">The ID of the guild to reset counts for</param>
    /// <param name="userId">Optional user ID to reset counts for</param>
    /// <param name="channelId">Optional channel ID to reset counts for</param>
    /// <returns>True if any records were found and removed, false otherwise</returns>
    public async Task<bool> ResetCount(ulong guildId, ulong? userId = 0, ulong? channelId = 0)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await using var transaction = await db.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            CancellationToken.None);

        try
        {
            // Build the base queries
            var countsQuery = db.MessageCounts.Where(x => x.GuildId == guildId);
            var timestampsQuery = db.MessageTimestamps.Where(x => x.GuildId == guildId);

            // Add filters based on parameters
            if (userId != 0)
            {
                countsQuery = countsQuery.Where(x => x.UserId == userId);
                timestampsQuery = timestampsQuery.Where(x => x.UserId == userId);
            }

            if (channelId != 0)
            {
                countsQuery = countsQuery.Where(x => x.ChannelId == channelId);
                timestampsQuery = timestampsQuery.Where(x => x.ChannelId == channelId);
            }

            // Get the counts before deletion for cache cleanup
            var countsToRemove = await countsQuery.ToListAsync();
            if (!countsToRemove.Any())
                return false;

            await countsQuery.DeleteAsync();

            await timestampsQuery.DeleteAsync();

            // Clear cache entries
            foreach (var key in countsToRemove.Select(count =>
                         $"msgcount:{count.GuildId}:{count.ChannelId}:{count.UserId}"))
            {
                cache.Remove(key);
            }

            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Failed to reset message counts for guild {GuildId}", guildId);
            return false;
        }
    }

    /// <summary>
    ///     Cancels background processing and releases the cancellation token source and update lock.
    /// </summary>
    public void Dispose()
    {
        cts.Cancel();
        cts.Dispose();
        updateLock.Dispose();
        GC.SuppressFinalize(this);
    }
}