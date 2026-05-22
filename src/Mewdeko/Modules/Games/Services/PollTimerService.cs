using System.Threading;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Strings;
using Poll = DataModel.Poll;

namespace Mewdeko.Modules.Games.Services;

/// <summary>
/// Background service that manages automatic poll closure based on expiration times.
/// </summary>
public class PollTimerService : INService, IReadyExecutor, IDisposable
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<PollTimerService> logger;
    private readonly PollService pollService;
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly GeneratedBotStrings strings;
    private readonly Timer timer;

    /// <summary>
    /// Initializes a new instance of the PollTimerService class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="pollService">The poll service.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="strings">The strings service.</param>
    public PollTimerService(IDataConnectionFactory dbFactory, PollService pollService,
        DiscordShardedClient client, ILogger<PollTimerService> logger, GeneratedBotStrings strings)
    {
        this.dbFactory = dbFactory;
        this.pollService = pollService;
        this.client = client;
        this.logger = logger;
        this.strings = strings;

        // Check for expired polls every 5 minutes
        timer = new Timer(CheckExpiredPolls, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Starts the timer service when the bot is ready.
    /// </summary>
    public async Task OnReadyAsync()
    {
        logger.LogInformation("Poll timer service is ready");
        await Task.CompletedTask;
        // Perform an initial check for expired polls
        CheckExpiredPolls(null);
    }

    /// <summary>
    /// Sets the expiration time for a poll.
    /// </summary>
    /// <param name="pollId">The ID of the poll.</param>
    /// <param name="duration">The duration until the poll expires.</param>
    /// <returns>True if the expiration time was set successfully, otherwise false.</returns>
    public async Task<bool> SetPollExpirationAsync(int pollId, TimeSpan duration)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var poll = await db.GetTable<Poll>()
                .FirstOrDefaultAsync(p => p.Id == pollId);

            if (poll is not { IsActive: true })
                return false;

            poll.ExpiresAt = DateTime.UtcNow.Add(duration);
            await db.UpdateAsync(poll);

            logger.LogInformation("Set expiration for poll {PollId} to {ExpiresAt}", pollId, poll.ExpiresAt);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set expiration for poll {PollId}", pollId);
            return false;
        }
    }

    /// <summary>
    /// Extends the expiration time of a poll.
    /// </summary>
    /// <param name="pollId">The ID of the poll.</param>
    /// <param name="additionalTime">The additional time to add to the current expiration.</param>
    /// <returns>True if the expiration time was extended successfully, otherwise false.</returns>
    public async Task<bool> ExtendPollAsync(int pollId, TimeSpan additionalTime)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var poll = await db.GetTable<Poll>()
                .FirstOrDefaultAsync(p => p.Id == pollId);

            if (poll is not { IsActive: true })
                return false;

            var newExpiration = poll.ExpiresAt?.Add(additionalTime) ?? DateTime.UtcNow.Add(additionalTime);
            poll.ExpiresAt = newExpiration;

            await db.UpdateAsync(poll);

            logger.LogInformation("Extended poll {PollId} expiration to {ExpiresAt}", pollId, newExpiration);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extend poll {PollId}", pollId);
            return false;
        }
    }

    /// <summary>
    /// Removes the expiration time from a poll, making it run indefinitely.
    /// </summary>
    /// <param name="pollId">The ID of the poll.</param>
    /// <returns>True if the expiration was removed successfully, otherwise false.</returns>
    public async Task<bool> RemovePollExpirationAsync(int pollId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var poll = await db.GetTable<Poll>()
                .FirstOrDefaultAsync(p => p.Id == pollId);

            if (poll == null)
                return false;

            poll.ExpiresAt = null;
            await db.UpdateAsync(poll);

            logger.LogInformation("Removed expiration from poll {PollId}", pollId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove expiration from poll {PollId}", pollId);
            return false;
        }
    }

    /// <summary>
    /// Gets the list of polls that will expire within the specified timeframe.
    /// </summary>
    /// <param name="timeframe">The timeframe to check for expiring polls.</param>
    /// <returns>A list of polls that will expire within the timeframe.</returns>
    public async Task<List<Poll>> GetExpiringPollsAsync(TimeSpan timeframe)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var cutoffTime = DateTime.UtcNow.Add(timeframe);

            return await db.GetTable<Poll>()
                .LoadWithAsTable(p => p.PollOptions)
                .Where(p => p.IsActive && p.ExpiresAt.HasValue && p.ExpiresAt.Value <= cutoffTime)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get expiring polls");
            return new List<Poll>();
        }
    }

    /// <summary>
    /// Gets statistics about poll expiration times.
    /// </summary>
    /// <param name="guildId">The guild ID to get statistics for (optional).</param>
    /// <returns>Statistics about poll expirations.</returns>
    public async Task<PollExpirationStats> GetExpirationStatsAsync(ulong? guildId = null)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var query = db.GetTable<Poll>().Where(p => p.IsActive);

            if (guildId.HasValue)
                query = query.Where(p => p.GuildId == guildId.Value);

            var polls = await query.ToListAsync();

            var withExpiration = polls.Where(p => p.ExpiresAt.HasValue).ToList();
            var withoutExpiration = polls.Where(p => !p.ExpiresAt.HasValue).ToList();

            var now = DateTime.UtcNow;
            var expiringIn1Hour = withExpiration.Count(p => p.ExpiresAt <= now.AddHours(1));
            var expiringIn24Hours = withExpiration.Count(p => p.ExpiresAt <= now.AddHours(24));
            var expiringIn7Days = withExpiration.Count(p => p.ExpiresAt <= now.AddDays(7));

            return new PollExpirationStats
            {
                TotalActivePolls = polls.Count,
                PollsWithExpiration = withExpiration.Count,
                PollsWithoutExpiration = withoutExpiration.Count,
                ExpiringInNextHour = expiringIn1Hour,
                ExpiringInNext24Hours = expiringIn24Hours,
                ExpiringInNext7Days = expiringIn7Days,
                AverageTimeToExpiration = withExpiration.Count > 0
                    ? TimeSpan.FromMilliseconds(
                        withExpiration.Average(p => (p.ExpiresAt!.Value - now).TotalMilliseconds))
                    : TimeSpan.Zero
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get expiration stats for guild {GuildId}", guildId);
            return new PollExpirationStats();
        }
    }

    /// <summary>
    /// Timer callback that checks for and closes expired polls.
    /// </summary>
    /// <param name="state">Timer state (unused).</param>
    private async void CheckExpiredPolls(object? state)
    {
        if (!await semaphore.WaitAsync(100)) // Don't block if already checking
            return;

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var expiredPolls = await db.GetTable<Poll>()
                .LoadWithAsTable(p => p.PollOptions)
                .Where(p => p.IsActive && p.ExpiresAt.HasValue && p.ExpiresAt.Value <= DateTime.UtcNow)
                .ToListAsync();

            if (expiredPolls.Count == 0)
                return;

            logger.LogInformation("Found {Count} expired polls to close", expiredPolls.Count);

            foreach (var poll in expiredPolls)
            {
                try
                {
                    // Close the poll in the database
                    var success = await pollService.ClosePollAsync(poll.Id, 0); // System closure

                    if (success)
                    {
                        // Update the poll message to show it's expired
                        await UpdateExpiredPollMessage(poll);

                        logger.LogInformation("Automatically closed expired poll {PollId} in guild {GuildId}",
                            poll.Id, poll.GuildId);
                    }
                    else
                    {
                        logger.LogWarning("Failed to close expired poll {PollId}", poll.Id);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error closing expired poll {PollId}", poll.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during expired poll check");
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Updates the poll message to indicate it has expired.
    /// </summary>
    /// <param name="poll">The expired poll.</param>
    private async Task UpdateExpiredPollMessage(Poll poll)
    {
        try
        {
            var guild = client.GetGuild(poll.GuildId);
            if (guild == null) return;

            var channel = guild.GetTextChannel(poll.ChannelId);
            if (channel == null) return;

            var message = await channel.GetMessageAsync(poll.MessageId) as IUserMessage;
            if (message == null) return;

            // Get current vote counts
            var stats = await pollService.GetPollStatsAsync(poll.Id);

            // Build the final results embed
            var embed = new EmbedBuilder()
                .WithTitle($"⏰ {poll.Question} (EXPIRED)")
                .WithColor(Color.DarkGrey)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithFooter(strings.PollExpiredFooter(poll.GuildId, poll.Id));

            // Add final results
            foreach (var option in poll.PollOptions.OrderBy(o => o.Index))
            {
                var voteCount = stats?.OptionVotes.GetValueOrDefault(option.Index, 0) ?? 0;
                var percentage = stats?.TotalVotes > 0 ? (double)voteCount / stats.TotalVotes * 100 : 0;

                var optionText = $"{option.Index + 1}. {option.Text}";
                if (!string.IsNullOrEmpty(option.Emote))
                    optionText = $"{option.Emote} {optionText}";

                embed.AddField(optionText, $"{voteCount} votes ({percentage:F1}%)", true);
            }

            if (stats?.TotalVotes > 0)
            {
                embed.AddField("Final Results",
                    $"Total Votes: {stats.TotalVotes}\nUnique Voters: {stats.UniqueVoters}");
            }
            else
            {
                embed.AddField("Final Results", "No votes were cast");
            }

            await message.ModifyAsync(msg =>
            {
                msg.Embed = embed.Build();
                msg.Components = new ComponentBuilder().Build(); // Remove all components
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update expired poll message for poll {PollId}", poll.Id);
        }
    }

    /// <summary>
    /// Disposes the timer when the service is disposed.
    /// </summary>
    public void Dispose()
    {
        timer?.Dispose();
        semaphore?.Dispose();
    }
}

/// <summary>
/// Statistics about poll expiration times.
/// </summary>
public class PollExpirationStats
{
    /// <summary>
    /// Gets or sets the total number of active polls.
    /// </summary>
    public int TotalActivePolls { get; set; }

    /// <summary>
    /// Gets or sets the number of polls with expiration times set.
    /// </summary>
    public int PollsWithExpiration { get; set; }

    /// <summary>
    /// Gets or sets the number of polls without expiration times.
    /// </summary>
    public int PollsWithoutExpiration { get; set; }

    /// <summary>
    /// Gets or sets the number of polls expiring in the next hour.
    /// </summary>
    public int ExpiringInNextHour { get; set; }

    /// <summary>
    /// Gets or sets the number of polls expiring in the next 24 hours.
    /// </summary>
    public int ExpiringInNext24Hours { get; set; }

    /// <summary>
    /// Gets or sets the number of polls expiring in the next 7 days.
    /// </summary>
    public int ExpiringInNext7Days { get; set; }

    /// <summary>
    /// Gets or sets the average time until expiration for polls with expiration times.
    /// </summary>
    public TimeSpan AverageTimeToExpiration { get; set; }
}