using System.Threading;
using EFCore.BulkExtensions;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
/// Service for counting messages
/// </summary>
public class MessageCountService : INService, IReadyExecutor
{
    private HashSet<ulong> countGuilds = [];
    private readonly DbContextProvider dbContext;
    private readonly ConcurrentDictionary<ulong, int> minCounts = [];
    private ConcurrentDictionary<(ulong GuildId, ulong UserId, ulong ChannelId), MessageCount> messageCounts = new();
    private Timer updateTimer;

    /// <summary>
    ///
    /// </summary>
    public MessageCountService(DbContextProvider dbContext, EventHandler handler)
    {
        this.dbContext = dbContext;
        handler.MessageReceived += HandleCount;
    }

    private async Task HandleCount(SocketMessage args)
    {
        await Task.CompletedTask;

        if (countGuilds.Count == 0 || args.Channel is IDMChannel || args.Channel is not IGuildChannel channel || !countGuilds.Contains(channel.GuildId) || args.Author.IsBot)
            return;

        if (!minCounts.TryGetValue(channel.GuildId, out var minValue) || args.Content.Length < minValue)
            return;

        var key = (channel.GuildId, args.Author.Id, channel.Id);
        var timestamp = args.Timestamp.UtcDateTime;

        messageCounts.AddOrUpdate(
            key,
            _ => new MessageCount
            {
                GuildId = channel.GuildId,
                UserId = args.Author.Id,
                ChannelId = channel.Id,
                Count = 1,
                RecentTimestamps = timestamp.ToString("O")
            },
            (_, existingCount) =>
            {
                existingCount.Count++;
                existingCount.AddTimestamp(timestamp);
                return existingCount;
            }
        );
    }

    /// <summary>
    /// Toggles the message count system for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to toggle in the message count system.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a boolean value:
    /// - true if the guild was added to the message count system.
    /// - false if the guild was removed from the message count system.
    /// If an error occurs during the operation, the method returns the original state.
    /// </returns>
    /// <remarks>
    /// This method toggles the guild's presence in the in-memory collections and updates the database
    /// to enable or disable message counting for the specified guild.
    /// </remarks>
    public async Task<bool> ToggleGuildMessageCount(ulong guildId)
    {
        var wasAdded = false;

        await using var db = await dbContext.GetContextAsync();
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
                Log.Warning("Attempted to toggle message count for non-existent guild {GuildId}", guildId);
                return false;
            }

            wasAdded = !guildConfig.UseMessageCount;

            if (wasAdded)
            {
                // Adding the guild to the system
                countGuilds.Add(guildId);
                minCounts[guildId] = guildConfig.MinMessageLength;

                // Load existing message counts for this guild
                var existingCounts = await db.MessageCounts
                    .Where(x => x.GuildId == guildId)
                    .ToListAsync();

                foreach (var count in existingCounts)
                {
                    messageCounts[(count.GuildId, count.UserId, count.ChannelId)] = count;
                }
            }
            else
            {
                // Removing the guild from the system
                countGuilds.Remove(guildId);
                minCounts.TryRemove(guildId, out _);

                // Remove all message counts for this guild from in-memory collection
                var keysToRemove = messageCounts.Keys
                    .Where(k => k.GuildId == guildId)
                    .ToList();
                foreach (var key in keysToRemove)
                {
                    messageCounts.TryRemove(key, out _);
                }
            }

            // Update the database
            await db.GuildConfigs
                .Where(x => x.GuildId == guildId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.UseMessageCount, wasAdded));


            return wasAdded;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to toggle message count for guild {GuildId}", guildId);

            // Revert in-memory changes if database operation failed
            if (wasAdded)
            {
                countGuilds.Remove(guildId);
                minCounts.TryRemove(guildId, out _);
                var keysToRemove = messageCounts.Keys
                    .Where(k => k.GuildId == guildId)
                    .ToList();
                foreach (var key in keysToRemove)
                {
                    messageCounts.TryRemove(key, out _);
                }
            }
            else
            {
                countGuilds.Add(guildId);
            }

            return !wasAdded; // Return the original state
        }
    }

    /// <summary>
    /// Gets an array of messagecounts for the selected type
    /// </summary>
    /// <param name="queryType"></param>
    /// <param name="snowflakeId"></param>
    /// <param name="guildId"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public async Task<(MessageCount[], bool)> GetAllCountsForEntity(CountQueryType queryType, ulong snowflakeId,
        ulong guildId)
    {
        await Task.CompletedTask;

        if (!countGuilds.Contains(guildId))
            return (null, false);

        return queryType switch
        {
            CountQueryType.Guild => (messageCounts.Where(x => x.Value.GuildId == snowflakeId)
                .Select(x => x.Value)
                .ToArray(), true),
            CountQueryType.Channel => (messageCounts.Where(x => x.Value.ChannelId == snowflakeId)
                .Select(x => x.Value)
                .ToArray(), true),
            CountQueryType.User => (messageCounts.Where(x => x.Value.GuildId == guildId && x.Value.UserId == snowflakeId)
                .Select(x => x.Value)
                .ToArray(), true),
            _ => throw new ArgumentOutOfRangeException(nameof(queryType), queryType, null)
        };
    }

    /// <summary>
    /// Gets a count for the specified type
    /// </summary>
    /// <param name="queryType">The type of query, <see cref="CountQueryType"/></param>
    /// <param name="snowflakeId">The id related to the query type</param>
    /// <returns>AN ulong count</returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<ulong> GetMessageCount(CountQueryType queryType, ulong guildId, ulong snowflakeId)
    {
        await Task.CompletedTask;
        var count = queryType switch
        {
            CountQueryType.Guild => messageCounts.Values
                .Where(x => x.GuildId == guildId)
                .Aggregate(0UL, (total, next) => total + next.Count),

            CountQueryType.Channel => messageCounts.Values
                .Where(x => x.ChannelId == snowflakeId && x.GuildId == guildId)
                .Aggregate(0UL, (total, next) => total + next.Count),

            CountQueryType.User => messageCounts.Values
                .Where(x => x.UserId == snowflakeId && x.GuildId == guildId)
                .Aggregate(0UL, (total, next) => total + next.Count),

            _ => throw new ArgumentException("Invalid query type", nameof(queryType))
        };

        return count;
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        Log.Information("Loading Message Count Cache");
        await using var db = await dbContext.GetContextAsync();
        countGuilds = (await db.GuildConfigs
                .Where(x => x.UseMessageCount)
                .Select(x => x.GuildId).ToListAsyncEF())
            .ToHashSet();

        var lengthConfigs = await db.GuildConfigs
            .Where(x => x.UseMessageCount)
            .Select(x => new
            {
                x.GuildId, x.MinMessageLength
            })
            .ToListAsyncEF();

        foreach (var config in lengthConfigs)
        {
            minCounts[config.GuildId] = config.MinMessageLength;
        }

        var dbMessageCounts = await db.MessageCounts.ToListAsync();
        messageCounts = new ConcurrentDictionary<(ulong GuildId, ulong UserId, ulong ChannelId), MessageCount>(
            dbMessageCounts.ToDictionary(
                mc => (mc.GuildId, mc.UserId, mc.ChannelId),
                mc => mc
            )
        );

        updateTimer = new Timer(async _ => await UpdateDatabase(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    private async Task UpdateDatabase()
    {
        try
        {
            Log.Information("Starting batch update of message counts and timestamps");
            await using var db = await dbContext.GetContextAsync();

            var countsToUpdate = messageCounts.Values.ToList();
            var bulkConfig = new BulkConfig
            {
                UpdateByProperties = ["GuildId", "UserId", "ChannelId"],

                PropertiesToExcludeOnUpdate = ["Id"],

                SqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity,

                PropertiesToInclude = ["GuildId", "UserId", "ChannelId", "Count", "RecentTimestamps"],

                SetOutputIdentity = true,
            };

            await db.BulkInsertOrUpdateOrDeleteAsync(countsToUpdate, bulkConfig);

            Log.Information("Batch update completed. Updated/Added {Count} entries", countsToUpdate.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred during batch update of message counts and timestamps");
        }
    }


    /// <summary>
    /// Gets the busiest hours for a guild
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="days"></param>
    /// <returns></returns>
    public async Task<IEnumerable<(int Hour, int Count)>> GetBusiestHours(ulong guildId, int days = 7)
    {
        await using var db = await dbContext.GetContextAsync();
        var startDate = DateTime.UtcNow.AddDays(-Math.Min(days, 30));

        var messageCounts = await db.MessageCounts
            .Where(m => m.GuildId == guildId)
            .Select(m => new { m.RecentTimestamps })
            .ToListAsync();

        return messageCounts
            .SelectMany(m => m.RecentTimestamps.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(DateTime.Parse)
                .Where(t => t >= startDate))
            .GroupBy(t => t.Hour)
            .Select(g => (g.Key, g.Count()))
            .OrderByDescending(x => x.Item2)
            .Take(24)
            .ToList();
    }

    /// <summary>
    /// Gets the busiest days in the guild
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="weeks"></param>
    /// <returns></returns>
    public async Task<IEnumerable<(DayOfWeek Day, int Count)>> GetBusiestDays(ulong guildId, int weeks = 4)
    {
        await using var db = await dbContext.GetContextAsync();
        var startDate = DateTime.UtcNow.AddDays(-Math.Min(7 * weeks, 30));

        var messageCounts = await db.MessageCounts
            .Where(m => m.GuildId == guildId)
            .Select(m => new { m.RecentTimestamps })
            .ToListAsync();

        return messageCounts
            .SelectMany(m => m.RecentTimestamps.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(DateTime.Parse)
                .Where(t => t >= startDate))
            .GroupBy(t => t.DayOfWeek)
            .Select(g => (g.Key, g.Count()))
            .OrderByDescending(x => x.Item2)
            .ToList();
    }

    /// <summary>
    /// Whether the query is for a channel, user, or guild
    /// </summary>
    public enum CountQueryType
    {
        /// <summary>
        /// Guild
        /// </summary>
        Guild,

        /// <summary>
        /// Channel
        /// </summary>
        Channel,

        /// <summary>
        /// User
        /// </summary>
        User
    }
}