using System.Threading;
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

        if (countGuilds.Count == 0 || args.Channel is IDMChannel || args.Channel is not IGuildChannel channel)
            return;

        var key = (channel.GuildId, args.Author.Id, channel.Id);
        messageCounts.AddOrUpdate(
            key,
            _ => new MessageCount { GuildId = channel.GuildId, UserId = args.Author.Id, ChannelId = channel.Id, Count = 1 },
            (_, existingCount) =>
            {
                existingCount.Count++;
                return existingCount;
            }
        );
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
            Log.Information("Starting batch update of message counts");
            await using var db = await dbContext.GetContextAsync();

            var countsToUpdate = messageCounts.Values.ToList();

            await db.BulkMergeAsync(countsToUpdate, options =>
            {
                options.ColumnPrimaryKeyExpression = c => new { c.GuildId, c.UserId, c.ChannelId };
                options.IgnoreOnMergeUpdateExpression = c => c.Id;
                options.MergeKeepIdentity = true;
                options.InsertIfNotExists = true;
                options.ColumnInputExpression = c => new
                {
                    c.GuildId,
                    c.UserId,
                    c.ChannelId,
                    c.Count
                };
                options.ColumnOutputExpression = c => c.Id;
                options.ColumnSynchronizeDeleteKeySubsetExpression = c => new { c.GuildId, c.UserId, c.ChannelId };
            });

            Log.Information("Batch update completed. Updated/Added {Count} entries", countsToUpdate.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred during batch update of message counts");
        }
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