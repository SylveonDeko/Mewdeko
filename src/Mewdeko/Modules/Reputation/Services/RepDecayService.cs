using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Reputation.Services;

/// <summary>
///     Background service responsible for handling reputation decay based on user inactivity.
/// </summary>
public class RepDecayService : INService, IReadyExecutor, IUnloadableService
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<RepDecayService> logger;
    private readonly GuildOwnershipService ownership;
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly GeneratedBotStrings strings;

    private Timer? decayTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RepDecayService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="strings">The localized bot strings.</param>
    /// <param name="client">The Discord sharded client.</param>
    /// <param name="ownership">The service deciding which guilds this instance handles.</param>
    public RepDecayService(
        IDataConnectionFactory dbFactory,
        ILogger<RepDecayService> logger,
        GeneratedBotStrings strings,
        DiscordShardedClient client,
        GuildOwnershipService ownership)
    {
        this.ownership = ownership;
        this.dbFactory = dbFactory;
        this.logger = logger;
        this.strings = strings;
        this.client = client;
    }

    /// <summary>
    ///     Initializes the decay service when the bot is ready.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task OnReadyAsync()
    {
        logger.LogInformation("Starting Reputation Decay Service");

        // Start the decay timer to run every hour
        decayTimer = new Timer(ProcessDecay, null, TimeSpan.Zero, TimeSpan.FromHours(1));

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Unloads the service and stops the decay timer.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task Unload()
    {
        decayTimer?.Dispose();
        semaphore.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Processes reputation decay for all eligible guilds.
    /// </summary>
    /// <param name="state">Timer state (unused).</param>
    private void ProcessDecay(object? state)
    {
        Task.Run(async () =>
        {
            if (!await semaphore.WaitAsync(TimeSpan.FromMinutes(5)))
            {
                logger.LogWarning("Decay process is already running, skipping this iteration");
                return;
            }

            try
            {
                await ProcessDecayInternal();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during reputation decay processing");
            }
            finally
            {
                semaphore.Release();
            }
        });
    }

    /// <summary>
    ///     Internal method to process reputation decay.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ProcessDecayInternal()
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get all guilds with decay enabled
        var guildsWithDecay = await db.RepConfigs
            .Where(x => x.EnableDecay)
            .ToListAsync();
        guildsWithDecay = guildsWithDecay.Where(x => ownership.IsMine(x.GuildId)).ToList();

        if (!guildsWithDecay.Any())
        {
            logger.LogDebug("No guilds have reputation decay enabled");
            return;
        }

        logger.LogInformation("Processing reputation decay for {GuildCount} guilds", guildsWithDecay.Count);

        var totalProcessed = 0;
        var totalDecayed = 0;

        foreach (var config in guildsWithDecay)
        {
            try
            {
                var (processed, decayed) = await ProcessGuildDecayAsync(db, config);
                totalProcessed += processed;
                totalDecayed += decayed;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing decay for guild {GuildId}", config.GuildId);
            }
        }

        logger.LogInformation(
            "Reputation decay processing complete: {TotalProcessed} users processed, {TotalDecayed} users decayed",
            totalProcessed, totalDecayed);
    }

    /// <summary>
    ///     Processes reputation decay for a specific guild.
    /// </summary>
    /// <param name="db">Database connection.</param>
    /// <param name="config">Guild reputation configuration.</param>
    /// <returns>A tuple containing (processed users, decayed users).</returns>
    private async Task<(int processed, int decayed)> ProcessGuildDecayAsync(MewdekoDb db, RepConfig config)
    {
        // Check if it's time to run decay for this guild based on DecayType
        if (!ShouldRunDecayForGuild(config))
            return (0, 0);

        var cutoffDate = DateTime.UtcNow.AddDays(-config.DecayInactiveDays);

        // Find users eligible for decay (haven't given or received rep in the inactive period)
        var eligibleUsers = await db.UserReputations
            .Where(x => x.GuildId == config.GuildId &&
                        x.TotalRep > 0 &&
                        !x.IsFrozen &&
                        (x.LastGivenAt == null || x.LastGivenAt < cutoffDate) &&
                        (x.LastReceivedAt == null || x.LastReceivedAt < cutoffDate))
            .ToListAsync();

        if (!eligibleUsers.Any())
        {
            logger.LogDebug("No users eligible for decay in guild {GuildId}", config.GuildId);
            return (0, 0);
        }

        logger.LogDebug("Processing decay for {UserCount} users in guild {GuildId}",
            eligibleUsers.Count, config.GuildId);

        var decayedUsers = 0;
        var processedUsers = 0;

        await using var transaction = await db.BeginTransactionAsync();

        try
        {
            foreach (var user in eligibleUsers)
            {
                processedUsers++;

                // Calculate decay amount based on type
                var decayAmount = CalculateDecayAmount(config, user);

                if (decayAmount <= 0)
                    continue;

                var newTotal = Math.Max(0, user.TotalRep - decayAmount);
                var actualDecay = user.TotalRep - newTotal;

                if (actualDecay > 0)
                {
                    user.TotalRep = newTotal;
                    await db.UpdateAsync(user);

                    // Also decay custom reputation types proportionally
                    await DecayCustomReputationsAsync(db, user.UserId, config.GuildId, actualDecay,
                        user.TotalRep + actualDecay);

                    // Add decay history entry
                    var historyEntry = new RepHistory
                    {
                        GiverId = 0, // System decay
                        ReceiverId = user.UserId,
                        GuildId = config.GuildId,
                        ChannelId = 0,
                        Amount = -actualDecay,
                        RepType = "decay",
                        Reason = $"Inactive for {config.DecayInactiveDays} days - {config.DecayType} decay",
                        IsAnonymous = false,
                        Timestamp = DateTime.UtcNow
                    };
                    await db.InsertAsync(historyEntry);

                    decayedUsers++;

                    // Send notification if configured
                    if (config.NotificationChannel.HasValue)
                    {
                        await SendDecayNotificationAsync(config.GuildId, config.NotificationChannel.Value,
                            user.UserId, actualDecay, newTotal);
                    }
                }
            }

            await transaction.CommitAsync();

            logger.LogInformation(
                "Processed decay for guild {GuildId}: {ProcessedUsers} processed, {DecayedUsers} decayed",
                config.GuildId, processedUsers, decayedUsers);

            return (processedUsers, decayedUsers);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Error during decay transaction for guild {GuildId}", config.GuildId);
            throw;
        }
    }

    /// <summary>
    ///     Determines if decay should run for a guild based on its configuration and last run time.
    /// </summary>
    /// <param name="config">Guild reputation configuration.</param>
    /// <returns>True if decay should run, false otherwise.</returns>
    private bool ShouldRunDecayForGuild(RepConfig config)
    {
        // Simple time-based check - in a production environment, you might want to store last decay time
        var now = DateTime.UtcNow;

        return config.DecayType.ToLower() switch
        {
            "daily" => now.Hour == 2, // Run at 2 AM UTC daily
            "weekly" => now is { DayOfWeek: DayOfWeek.Sunday, Hour: 2 }, // Run Sunday 2 AM UTC
            "monthly" => now is { Day: 1, Hour: 2 }, // Run first of month 2 AM UTC
            _ => false
        };
    }

    /// <summary>
    ///     Calculates the decay amount for a user based on configuration.
    /// </summary>
    /// <param name="config">Guild reputation configuration.</param>
    /// <param name="user">User reputation data.</param>
    /// <returns>The amount of reputation to decay.</returns>
    private int CalculateDecayAmount(RepConfig config, UserReputation user)
    {
        return config.DecayType.ToLower() switch
        {
            "percentage" => (int)(user.TotalRep * (config.DecayAmount / 100.0)),
            "fixed" => config.DecayAmount,
            _ => config.DecayAmount // Default to fixed amount
        };
    }

    /// <summary>
    ///     Decays custom reputation types proportionally to total reputation decay.
    /// </summary>
    /// <param name="db">Database connection.</param>
    /// <param name="userId">User ID.</param>
    /// <param name="guildId">Guild ID.</param>
    /// <param name="totalDecay">Total reputation decayed.</param>
    /// <param name="originalTotal">Original total reputation before decay.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task DecayCustomReputationsAsync(MewdekoDb db, ulong userId, ulong guildId, int totalDecay,
        int originalTotal)
    {
        if (originalTotal <= 0) return;

        var userCustomReps = await db.UserCustomReputations
            .Where(x => x.UserId == userId && x.GuildId == guildId && x.Amount > 0)
            .ToListAsync();

        foreach (var customRep in userCustomReps)
        {
            // Calculate proportional decay
            var proportionalDecay = (int)((double)customRep.Amount / originalTotal * totalDecay);

            if (proportionalDecay > 0)
            {
                customRep.Amount = Math.Max(0, customRep.Amount - proportionalDecay);
                customRep.LastUpdated = DateTime.UtcNow;
                await db.UpdateAsync(customRep);
            }
        }
    }

    /// <summary>
    ///     Sends a decay notification to the configured channel.
    /// </summary>
    /// <param name="guildId">Guild ID.</param>
    /// <param name="channelId">Notification channel ID.</param>
    /// <param name="userId">User who had reputation decayed.</param>
    /// <param name="decayAmount">Amount of reputation decayed.</param>
    /// <param name="newTotal">New total reputation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SendDecayNotificationAsync(ulong guildId, ulong channelId, ulong userId, int decayAmount,
        int newTotal)
    {
        try
        {
            if (client.GetGuild(guildId) is not IGuild guild) return;

            var channel = await guild.GetTextChannelAsync(channelId);
            if (channel == null) return;

            var user = guild.GetUserAsync(userId);
            var username = user?.ToString() ?? $"Unknown User ({userId})";

            var message = strings.RepDecayNotification(guildId, username, decayAmount, newTotal);
            await channel.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send decay notification for user {UserId} in guild {GuildId}",
                userId, guildId);
        }
    }
}