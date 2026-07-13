using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Reputation.Common;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Services.Strings;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Reputation.Services;

/// <summary>
///     Service responsible for managing the reputation system including giving, tracking, and configuring reputation.
/// </summary>
public class RepService : INService, IReadyExecutor, IUnloadableService
{
    private readonly ConcurrentDictionary<(ulong, ulong), RepChannelConfig> channelConfigCache = new();
    private readonly DiscordShardedClient client;

    // Cache for performance
    private readonly ConcurrentDictionary<ulong, RepConfig> configCache = new();
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly ILogger<RepService> logger;
    private readonly MessageCountService messageCountService;
    private readonly RepNotificationService notificationService;
    private readonly ConcurrentDictionary<ulong, List<RepReactionConfig>> reactionConfigCache = new();
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RepService" /> class.
    /// </summary>
    /// <param name="client">The Discord sharded client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="strings">The localized bot strings.</param>
    /// <param name="notificationService">The reputation notification service.</param>
    /// <param name="messageCountService">The message count service.</param>
    /// <param name="logger">The logger instance.</param>
    public RepService(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        EventHandler eventHandler,
        GeneratedBotStrings strings,
        RepNotificationService notificationService,
        MessageCountService messageCountService,
        ILogger<RepService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.eventHandler = eventHandler;
        this.strings = strings;
        this.notificationService = notificationService;
        this.messageCountService = messageCountService;
        this.logger = logger;
    }

    /// <summary>
    ///     Initializes the service and loads configuration cache when the bot is ready.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task OnReadyAsync()
    {
        logger.LogInformation("Starting {ServiceName} Cache", GetType().Name);
        await using var db = await dbFactory.CreateConnectionAsync();

        // Load configurations into cache
        var configs = await db.RepConfigs.ToListAsync();
        foreach (var config in configs)
        {
            configCache.TryAdd(config.GuildId, config);
        }

        var channelConfigs = await db.RepChannelConfigs.ToListAsync();
        foreach (var channelConfig in channelConfigs)
        {
            channelConfigCache.TryAdd((channelConfig.GuildId, channelConfig.ChannelId), channelConfig);
        }

        // Load reaction configurations
        var reactionConfigs = await db.RepReactionConfigs.ToListAsync();
        foreach (var reactionConfig in reactionConfigs)
        {
            if (!reactionConfigCache.ContainsKey(reactionConfig.GuildId))
                reactionConfigCache.TryAdd(reactionConfig.GuildId, new List<RepReactionConfig>());
            reactionConfigCache[reactionConfig.GuildId].Add(reactionConfig);
        }

        // Subscribe to reaction events
        eventHandler.Subscribe("ReactionAdded", "RepService", HandleReactionAdded);
        eventHandler.Subscribe("ReactionRemoved", "RepService", HandleReactionRemoved);

        logger.LogInformation(
            "Reputation Cache Ready - {ConfigCount} guild configs, {ChannelCount} channel configs, {ReactionCount} reaction configs",
            configs.Count, channelConfigs.Count, reactionConfigs.Count);
    }

    /// <summary>
    ///     Unloads the service and clears caches.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task Unload()
    {
        configCache.Clear();
        channelConfigCache.Clear();
        reactionConfigCache.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Gives reputation from one user to another.
    /// </summary>
    public async Task<GiveRepResult> GiveReputationAsync(ulong guildId, ulong giverId, ulong receiverId,
        ulong channelId, string repType = "standard", string? reason = null, int? customAmount = null,
        bool anonymous = false, int? customCooldownMinutes = null, IUserMessage? processAllReactionsOnMessage = null,
        IEmote? reactionEmote = null)
    {
        // Get guild configuration
        var config = await GetOrCreateConfigAsync(guildId);
        if (!config.Enabled)
            return new GiveRepResult
            {
                Result = GiveRepResultType.Disabled
            };

        // Check channel configuration
        var channelConfig = await GetChannelConfigAsync(guildId, channelId);
        if (channelConfig?.State == "disabled")
            return new GiveRepResult
            {
                Result = GiveRepResultType.ChannelDisabled
            };

        // Anti-abuse checks (skip for negative amounts like reaction removal)
        if (customAmount is null or > 0)
        {
            var abuseResult = await CheckAntiAbuseAsync(guildId, giverId, receiverId, config);
            if (abuseResult.Result != GiveRepResultType.Success)
                return abuseResult;
        }

        await using var db = await dbFactory.CreateConnectionAsync();
        await using var transaction = await db.BeginTransactionAsync();

        try
        {
            // Calculate reputation amount with multipliers
            var baseAmount = customAmount ?? 1;
            var channelMultiplier = channelConfig?.Multiplier ?? 1.0m;
            var finalAmount = customAmount.HasValue ? baseAmount : (int)(baseAmount * channelMultiplier);

            // Update or create user reputation
            var userRep = await db.UserReputations
                .Where(x => x.UserId == receiverId && x.GuildId == guildId)
                .FirstOrDefaultAsync();

            if (userRep == null)
            {
                userRep = new UserReputation
                {
                    UserId = receiverId,
                    GuildId = guildId,
                    TotalRep = finalAmount,
                    LastReceivedAt = DateTime.UtcNow,
                    DateAdded = DateTime.UtcNow
                };

                // Handle custom reputation types
                await UpdateCustomReputationAsync(db, receiverId, guildId, repType, finalAmount);
                await db.InsertAsync(userRep);
            }
            else
            {
                userRep.TotalRep += finalAmount;
                userRep.LastReceivedAt = DateTime.UtcNow;

                // Handle custom reputation types
                await UpdateCustomReputationAsync(db, receiverId, guildId, repType, finalAmount);
                await db.UpdateAsync(userRep);
            }

            // Update giver's streak and stats
            var giverRep = await db.UserReputations
                .Where(x => x.UserId == giverId && x.GuildId == guildId)
                .FirstOrDefaultAsync();

            if (giverRep == null)
            {
                giverRep = new UserReputation
                {
                    UserId = giverId,
                    GuildId = guildId,
                    LastGivenAt = DateTime.UtcNow,
                    CurrentStreak = 1,
                    LongestStreak = 1,
                    DateAdded = DateTime.UtcNow
                };
                await db.InsertAsync(giverRep);
            }
            else
            {
                var daysSinceLastGiven = giverRep.LastGivenAt.HasValue
                    ? (DateTime.UtcNow - giverRep.LastGivenAt.Value).TotalDays
                    : double.MaxValue;

                if (daysSinceLastGiven <= 1.5) // Allow some grace for timezone differences
                {
                    giverRep.CurrentStreak++;
                    if (giverRep.CurrentStreak > giverRep.LongestStreak)
                        giverRep.LongestStreak = giverRep.CurrentStreak;
                }
                else
                {
                    giverRep.CurrentStreak = 1;
                }

                giverRep.LastGivenAt = DateTime.UtcNow;
                await db.UpdateAsync(giverRep);
            }

            // Add to history
            var historyEntry = new RepHistory
            {
                GiverId = giverId,
                ReceiverId = receiverId,
                GuildId = guildId,
                ChannelId = channelId,
                Amount = finalAmount,
                RepType = repType,
                Reason = reason,
                IsAnonymous = config.EnableAnonymous && anonymous,
                Timestamp = DateTime.UtcNow
            };
            await db.InsertAsync(historyEntry);

            // Add or update cooldown (only for positive reputation)
            if (finalAmount > 0)
            {
                var cooldownMinutes = customCooldownMinutes ?? config.DefaultCooldownMinutes;
                var newExpiresAt = DateTime.UtcNow.AddMinutes(cooldownMinutes);

                // Use UPSERT to avoid duplicate key violations
                var existingCooldown = await db.RepCooldowns
                    .FirstOrDefaultAsync(x =>
                        x.GiverId == giverId && x.ReceiverId == receiverId && x.GuildId == guildId);

                if (existingCooldown != null)
                {
                    // Update existing cooldown with new expiry time
                    existingCooldown.ExpiresAt = newExpiresAt;
                    await db.UpdateAsync(existingCooldown);
                }
                else
                {
                    // Insert new cooldown
                    var cooldown = new RepCooldowns
                    {
                        GiverId = giverId, ReceiverId = receiverId, GuildId = guildId, ExpiresAt = newExpiresAt
                    };
                    await db.InsertAsync(cooldown);
                }
            }

            await transaction.CommitAsync();

            // Check for role rewards
            await CheckRoleRewardsAsync(guildId, receiverId, userRep.TotalRep);

            // Send notifications
            await notificationService.SendReputationNotificationAsync(guildId, receiverId, giverId,
                finalAmount, userRep.TotalRep, repType, reason, config.EnableAnonymous && anonymous);

            // If requested, process all other reactions of the same type on the message
            if (processAllReactionsOnMessage != null && reactionEmote != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessAllOtherReactionsAsync(guildId, channelId, processAllReactionsOnMessage,
                            reactionEmote, repType, reason, customAmount, customCooldownMinutes, giverId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Error processing other reactions on message {MessageId}",
                            processAllReactionsOnMessage.Id);
                    }
                });
            }

            return new GiveRepResult
            {
                Result = GiveRepResultType.Success, NewTotal = userRep.TotalRep, Amount = finalAmount
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Error giving reputation from {GiverId} to {ReceiverId} in guild {GuildId}",
                giverId, receiverId, guildId);
            throw;
        }
    }

    /// <summary>
    ///     Gets a user's reputation and rank in the guild.
    /// </summary>
    public async Task<(int reputation, int rank)> GetUserReputationAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var userRep = await db.UserReputations
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .FirstOrDefaultAsync();

        if (userRep == null)
            return (0, 0);

        var rank = await db.UserReputations
            .Where(x => x.GuildId == guildId && x.TotalRep > userRep.TotalRep)
            .CountAsync() + 1;

        return (userRep.TotalRep, rank);
    }

    /// <summary>
    ///     Gets the reputation leaderboard for a guild.
    /// </summary>
    public async Task<List<(ulong userId, int reputation)>> GetLeaderboardAsync(ulong guildId, int page = 1,
        int pageSize = 25)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var leaderboard = await db.UserReputations
            .Where(x => x.GuildId == guildId && x.TotalRep > 0)
            .OrderByDescending(x => x.TotalRep)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.UserId, x.TotalRep
            })
            .ToListAsync()
            .ConfigureAwait(false);

        return leaderboard.Select(x => (x.UserId, x.TotalRep)).ToList();
    }

    /// <summary>
    ///     Gets reputation history for a user.
    /// </summary>
    public async Task<List<RepHistory>> GetReputationHistoryAsync(ulong guildId, ulong userId, int page = 1,
        int pageSize = 25)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.RepHistories
            .Where(x => x.GuildId == guildId && x.ReceiverId == userId)
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets detailed statistics for a user.
    /// </summary>
    public async Task<RepUserStats> GetUserStatsAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var userRep = await db.UserReputations
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .FirstOrDefaultAsync();

        var totalGiven = await db.RepHistories
            .Where(x => x.GuildId == guildId && x.GiverId == userId)
            .SumAsync(x => x.Amount);

        var totalReceived = await db.RepHistories
            .Where(x => x.GuildId == guildId && x.ReceiverId == userId)
            .SumAsync(x => x.Amount);

        var rank = userRep != null
            ? await db.UserReputations
                .Where(x => x.GuildId == guildId && x.TotalRep > userRep.TotalRep)
                .CountAsync() + 1
            : 0;

        // Get custom reputation types for this user
        var customReps = await GetUserCustomReputationsAsync(db, guildId, userId);

        return new RepUserStats
        {
            TotalRep = userRep?.TotalRep ?? 0,
            Rank = rank,
            TotalGiven = totalGiven,
            TotalReceived = totalReceived,
            CurrentStreak = userRep?.CurrentStreak ?? 0,
            LongestStreak = userRep?.LongestStreak ?? 0,
            LastGivenAt = userRep?.LastGivenAt,
            LastReceivedAt = userRep?.LastReceivedAt,
            CustomReputations = customReps
        };
    }

    private async Task<GiveRepResult> CheckAntiAbuseAsync(ulong guildId, ulong giverId, ulong receiverId,
        RepConfig config)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Check if receiver is frozen
        var receiverRep = await db.UserReputations
            .Where(x => x.UserId == receiverId && x.GuildId == guildId)
            .FirstOrDefaultAsync();

        if (receiverRep?.IsFrozen == true)
            return new GiveRepResult
            {
                Result = GiveRepResultType.UserFrozen
            };

        // Check cooldown
        var cooldown = await db.RepCooldowns
            .Where(x => x.GiverId == giverId && x.ReceiverId == receiverId && x.GuildId == guildId)
            .FirstOrDefaultAsync();

        if (cooldown != null && cooldown.ExpiresAt > DateTime.UtcNow)
        {
            var remaining = cooldown.ExpiresAt - DateTime.UtcNow;
            return new GiveRepResult
            {
                Result = GiveRepResultType.Cooldown, CooldownRemaining = remaining
            };
        }

        // Check daily limit
        var today = DateTime.UtcNow.Date;
        var dailyCount = await db.RepHistories
            .Where(x => x.GiverId == giverId && x.GuildId == guildId && x.Timestamp >= today)
            .CountAsync();

        if (dailyCount >= config.DailyLimit)
            return new GiveRepResult
            {
                Result = GiveRepResultType.DailyLimit, DailyLimit = config.DailyLimit
            };

        // Check weekly limit (if configured)
        if (config.WeeklyLimit.HasValue)
        {
            var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
            var weeklyCount = await db.RepHistories
                .Where(x => x.GiverId == giverId && x.GuildId == guildId && x.Timestamp >= weekStart)
                .CountAsync();

            if (weeklyCount >= config.WeeklyLimit.Value)
                return new GiveRepResult
                {
                    Result = GiveRepResultType.WeeklyLimit, WeeklyLimit = config.WeeklyLimit.Value
                };
        }

        // Check account age requirements
        if (config.MinAccountAgeDays > 0)
        {
            var socketGuild = client.GetGuild(guildId);
            if (socketGuild != null)
            {
                var guild = (IGuild)socketGuild;
                var giver = await guild.GetUserAsync(giverId);
                if (giver != null)
                {
                    var accountAge = DateTime.UtcNow - giver.CreatedAt.UtcDateTime;
                    if (accountAge.TotalDays < config.MinAccountAgeDays)
                        return new GiveRepResult
                        {
                            Result = GiveRepResultType.MinimumAccountAge, RequiredDays = config.MinAccountAgeDays
                        };
                }
            }
        }

        // Check server membership duration
        if (config.MinServerMembershipHours > 0)
        {
            var socketGuild = client.GetGuild(guildId);
            if (socketGuild != null)
            {
                IGuild guild = socketGuild;
                var giver = await guild.GetUserAsync(giverId);
                if (giver is { JoinedAt: not null })
                {
                    var membershipDuration = DateTime.UtcNow - giver.JoinedAt.Value.UtcDateTime;
                    if (membershipDuration.TotalHours < config.MinServerMembershipHours)
                        return new GiveRepResult
                        {
                            Result = GiveRepResultType.MinimumServerMembership,
                            RequiredHours = config.MinServerMembershipHours
                        };
                }
            }
        }

        // Check minimum message count
        if (config.MinMessageCount > 0)
        {
            var userMessageCount = await messageCountService.GetMessageCount(
                MessageCountService.CountQueryType.User, guildId, giverId);

            if (userMessageCount < (ulong)config.MinMessageCount)
                return new GiveRepResult
                {
                    Result = GiveRepResultType.MinimumMessages, RequiredMessages = config.MinMessageCount
                };
        }

        return new GiveRepResult
        {
            Result = GiveRepResultType.Success
        };
    }

    private async Task<RepConfig> GetOrCreateConfigAsync(ulong guildId)
    {
        if (configCache.TryGetValue(guildId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var config = await db.RepConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (config == null)
        {
            config = new RepConfig
            {
                GuildId = guildId, DateAdded = DateTime.UtcNow
            };
            await db.InsertAsync(config);
        }

        configCache.TryAdd(guildId, config);
        return config;
    }

    private async Task<RepChannelConfig?> GetChannelConfigAsync(ulong guildId, ulong channelId)
    {
        if (channelConfigCache.TryGetValue((guildId, channelId), out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var config = await db.RepChannelConfigs
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.ChannelId == channelId);

        if (config != null)
            channelConfigCache.TryAdd((guildId, channelId), config);

        return config;
    }

    private async Task CheckRoleRewardsAsync(ulong guildId, ulong userId, int newReputation)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var allRoleRewards = await db.RepRoleRewards
                .Where(x => x.GuildId == guildId)
                .OrderBy(x => x.RepRequired)
                .ToListAsync();

            var socketGuild = client.GetGuild(guildId);
            if (socketGuild == null) return;

            var guild = (IGuild)socketGuild;
            var user = await guild.GetUserAsync(userId);
            if (user == null) return;

            // Check which roles user should have based on reputation
            var eligibleRoleRewards = allRoleRewards.Where(x => newReputation >= x.RepRequired).ToList();
            var ineligibleRoleRewards =
                allRoleRewards.Where(x => newReputation < x.RepRequired && x.RemoveOnDrop).ToList();

            // Add roles user is eligible for
            foreach (var reward in eligibleRoleRewards)
            {
                var role = guild.GetRole(reward.RoleId);
                if (role == null) continue;

                if (!user.RoleIds.Contains(reward.RoleId))
                {
                    try
                    {
                        await user.AddRoleAsync(role);
                        logger.LogInformation(
                            "Added role {RoleName} to user {UserId} in guild {GuildId} for reaching {Rep} reputation",
                            role.Name, userId, guildId, reward.RepRequired);

                        // Send milestone notification if configured
                        if (reward.AnnounceChannel.HasValue)
                        {
                            var channel = await guild.GetTextChannelAsync(reward.AnnounceChannel.Value);
                            if (channel != null)
                            {
                                var message = strings.RepRoleMilestone(guildId, user.Mention, newReputation,
                                    role.Mention);
                                await channel.SendMessageAsync(message);
                            }
                        }

                        // Send DM notification if configured
                        if (reward.AnnounceDM)
                        {
                            try
                            {
                                // TODO: Add RepRoleAwarded to locale strings
                                // var dmMessage = strings.RepRoleAwarded(guildId, role.Name, reward.RepRequired, guild.Name);
                                var dmMessage =
                                    $"Congratulations! You've been awarded the **{role.Name}** role for reaching {reward.RepRequired} reputation in **{guild.Name}**!";
                                await user.SendMessageAsync(dmMessage);
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Failed to send DM notification to user {UserId}", userId);
                            }
                        }

                        // TODO: Add XP reward if configured and XP system is available
                        if (reward.XPReward is > 0)
                        {
                            logger.LogDebug(
                                "XP reward of {XP} configured for role {RoleId} but XP system integration not yet implemented",
                                reward.XPReward.Value, reward.RoleId);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to add role {RoleId} to user {UserId} in guild {GuildId}",
                            reward.RoleId, userId, guildId);
                    }
                }
            }

            // Remove roles user is no longer eligible for
            foreach (var reward in ineligibleRoleRewards)
            {
                var role = guild.GetRole(reward.RoleId);
                if (role == null) continue;

                if (user.RoleIds.Contains(reward.RoleId))
                {
                    try
                    {
                        await user.RemoveRoleAsync(role);
                        logger.LogInformation(
                            "Removed role {RoleName} from user {UserId} in guild {GuildId} as reputation dropped below {Rep}",
                            role.Name, userId, guildId, reward.RepRequired);

                        // Send notification if configured
                        if (reward.AnnounceDM)
                        {
                            try
                            {
                                // TODO: Add RepRoleRemoved to locale strings
                                // var dmMessage = strings.RepRoleRemoved(guildId, role.Name, reward.RepRequired, guild.Name);
                                var dmMessage =
                                    $"Your **{role.Name}** role has been removed as your reputation dropped below {reward.RepRequired} in **{guild.Name}**.";
                                await user.SendMessageAsync(dmMessage);
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Failed to send DM notification to user {UserId}", userId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to remove role {RoleId} from user {UserId} in guild {GuildId}",
                            reward.RoleId, userId, guildId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking role rewards for user {UserId} in guild {GuildId}",
                userId, guildId);
        }
    }

    /// <summary>
    ///     Handles when a reaction is added to a message, potentially giving reputation.
    /// </summary>
    /// <param name="channel">The channel this reaction was added in.</param>
    /// <param name="reaction">The reaction event arguments.</param>
    /// <param name="message">The message this reaction was added to</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        try
        {
            // Only process reactions in guilds
            if (channel.Value is not SocketTextChannel text ||
                await message.GetOrDownloadAsync() is not { } reactionMessage) return;

            ITextChannel textChannel = text;
            var guildId = textChannel.Guild.Id;
            var reactorId = reaction.UserId;

            // Don't give rep for own reactions or bot reactions
            if (reaction.User.Value?.IsBot != false) return;

            // Check if we have reaction configs for this guild
            if (!reactionConfigCache.ContainsKey(guildId)) return;

            // Get the message to find the author

            // Find matching reaction config
            var matchingConfig = FindMatchingReactionConfig(guildId, reaction.Emote);
            if (matchingConfig is not { IsEnabled: true }) return;

            // Check message age requirements
            if (matchingConfig.MinMessageAgeMinutes > 0)
            {
                var messageAge = DateTime.UtcNow - reactionMessage.CreatedAt.UtcDateTime;
                if (messageAge.TotalMinutes < matchingConfig.MinMessageAgeMinutes) return;
            }

            // Check message length requirements
            if (matchingConfig.MinMessageLength > 0 &&
                reactionMessage.Content.Length < matchingConfig.MinMessageLength) return;

            // Check channel restrictions
            if (!string.IsNullOrEmpty(matchingConfig.AllowedChannels))
            {
                var allowedChannels = JsonConvert.DeserializeObject<List<ulong>>(matchingConfig.AllowedChannels);
                if (allowedChannels != null && !allowedChannels.Contains(channel.Id)) return;
            }

            // Check role requirements for reactor
            if (matchingConfig.RequiredRoleId.HasValue)
            {
                var reactor = await textChannel.Guild.GetUserAsync(reactorId);
                if (reactor == null || reactor.RoleIds.All(r => r != matchingConfig.RequiredRoleId.Value)) return;
            }

            // Check role restrictions for receiver
            if (!string.IsNullOrEmpty(matchingConfig.AllowedReceiverRoles))
            {
                var allowedRoles = JsonConvert.DeserializeObject<List<ulong>>(matchingConfig.AllowedReceiverRoles);
                if (allowedRoles != null)
                {
                    var receiver = await textChannel.Guild.GetUserAsync(reactionMessage.Author.Id);
                    if (receiver == null || !receiver.RoleIds.Any(r => allowedRoles.Contains(r))) return;
                }
            }

            // Pass the message and emote to process all existing reactions of this type
            await GiveReputationAsync(guildId, reactorId, reactionMessage.Author.Id, channel.Id,
                matchingConfig.RepType, $"Reaction: {matchingConfig.EmojiName}", matchingConfig.RepAmount,
                customCooldownMinutes: matchingConfig.CooldownMinutes,
                processAllReactionsOnMessage: reactionMessage, reactionEmote: reaction.Emote);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling reaction added");
        }
    }

    /// <summary>
    ///     Handles when a reaction is removed from a message, potentially removing reputation.
    /// </summary>
    /// <param name="channel">The channel this reaction happened in. </param>
    /// <param name="reaction">The reaction event arguments.</param>
    /// <param name="message">The message this reaction was attached to</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task HandleReactionRemoved(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        try
        {
            // Only process reactions in guilds
            if (channel.Value is not SocketTextChannel text ||
                await message.GetOrDownloadAsync() is not { } reactionMessage) return;

            ITextChannel textChannel = text;

            var guildId = textChannel.Guild.Id;

            // Check if we have reaction configs for this guild
            if (!reactionConfigCache.ContainsKey(guildId)) return;

            // Find matching reaction config
            var matchingConfig = FindMatchingReactionConfig(guildId, reaction.Emote);
            if (matchingConfig is not { IsEnabled: true }) return;

            // Get the message to find the author

            // Remove reputation by giving negative amount
            await GiveReputationAsync(guildId, reaction.UserId, reactionMessage.Author.Id, channel.Id,
                matchingConfig.RepType, $"Reaction removed: {matchingConfig.EmojiName}", -matchingConfig.RepAmount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling reaction removed");
        }
    }

    /// <summary>
    ///     Finds a matching reaction configuration for the given emoji.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="emote">The emote to match.</param>
    /// <returns>The matching configuration or null if none found.</returns>
    private RepReactionConfig? FindMatchingReactionConfig(ulong guildId, IEmote emote)
    {
        if (!reactionConfigCache.TryGetValue(guildId, out var configs)) return null;

        return emote switch
        {
            Emote customEmote => configs.FirstOrDefault(c => c.EmojiId == customEmote.Id),
            Emoji unicodeEmoji => configs.FirstOrDefault(c => c.EmojiName == unicodeEmoji.Name && c.EmojiId == null),
            _ => null
        };
    }

    /// <summary>
    ///     Updates custom reputation for a user based on the reputation type.
    /// </summary>
    /// <param name="db">Database connection.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="repType">The type of reputation being given.</param>
    /// <param name="amount">The amount of reputation to add.</param>
    private async Task UpdateCustomReputationAsync(MewdekoDb db, ulong userId, ulong guildId, string repType,
        int amount)
    {
        // Skip if it's standard reputation (only goes to TotalRep)
        if (repType.Equals("standard", StringComparison.OrdinalIgnoreCase))
            return;

        // Find the custom reputation type
        var customType = await db.RepCustomTypes
            .Where(x => x.GuildId == guildId && x.TypeName.ToLower() == repType.ToLower() && x.IsActive)
            .FirstOrDefaultAsync();

        if (customType == null) return;

        // Apply multiplier to amount
        var finalAmount = (int)(amount * customType.Multiplier);

        // Update or create user custom reputation
        var userCustomRep = await db.UserCustomReputations
            .FirstOrDefaultAsync(x => x.UserId == userId && x.GuildId == guildId && x.CustomTypeId == customType.Id);

        if (userCustomRep == null)
        {
            userCustomRep = new UserCustomReputation
            {
                UserId = userId,
                GuildId = guildId,
                CustomTypeId = customType.Id,
                Amount = finalAmount,
                LastUpdated = DateTime.UtcNow
            };
            await db.InsertAsync(userCustomRep);
        }
        else
        {
            userCustomRep.Amount += finalAmount;
            userCustomRep.LastUpdated = DateTime.UtcNow;
            await db.UpdateAsync(userCustomRep);
        }
    }

    /// <summary>
    ///     Gets all custom reputation types and amounts for a user.
    /// </summary>
    /// <param name="db">Database connection.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>Dictionary of custom reputation types and amounts.</returns>
    private async Task<Dictionary<string, (int amount, string displayName, string? emoji)>>
        GetUserCustomReputationsAsync(MewdekoDb db, ulong guildId, ulong userId)
    {
        var result = new Dictionary<string, (int amount, string displayName, string? emoji)>();

        // Get user's custom reputation entries
        var userCustomReps = await db.UserCustomReputations
            .Where(ucr => ucr.UserId == userId && ucr.GuildId == guildId)
            .ToListAsync();

        // Get active custom types for this guild
        var activeTypes = await db.RepCustomTypes
            .Where(ct => ct.GuildId == guildId && ct.IsActive)
            .ToListAsync();

        // Join them in memory
        var customReps = (from ucr in userCustomReps
                join ct in activeTypes on ucr.CustomTypeId equals ct.Id
                select new
                {
                    ucr.Amount, ct.TypeName, ct.DisplayName, ct.EmojiIcon
                })
            .ToList();

        foreach (var rep in customReps)
        {
            result[rep.TypeName] = (rep.Amount, rep.DisplayName, rep.EmojiIcon);
        }

        return result;
    }

    /// <summary>
    ///     Gets all valid reputation types for a guild (standard + custom active types).
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>List of valid reputation type names.</returns>
    public async Task<List<string>> GetValidReputationTypesAsync(ulong guildId)
    {
        var result = new List<string>
        {
            "standard"
        };

        await using var db = await dbFactory.CreateConnectionAsync();
        var customTypes = await db.RepCustomTypes
            .Where(ct => ct.GuildId == guildId && ct.IsActive)
            .Select(ct => ct.TypeName)
            .ToListAsync();

        result.AddRange(customTypes);
        return result;
    }

    /// <summary>
    ///     Checks if a reputation type is valid for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="repType">The reputation type to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public async Task<bool> IsValidReputationTypeAsync(ulong guildId, string repType)
    {
        if (repType.Equals("standard", StringComparison.OrdinalIgnoreCase))
            return true;

        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.RepCustomTypes
            .AnyAsync(x => x.GuildId == guildId &&
                           x.TypeName.ToLower() == repType.ToLower() &&
                           x.IsActive);
    }

    /// <summary>
    ///     Gets custom reputation for a user of a specific type.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="repType">The reputation type.</param>
    /// <returns>The user's custom reputation amount.</returns>
    public async Task<int> GetUserCustomReputationAsync(ulong guildId, ulong userId, string repType)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // First get the custom type ID
        var customType = await db.RepCustomTypes
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.TypeName.ToLower() == repType.ToLower() && x.IsActive);

        if (customType == null)
            return 0;

        var customRep = await db.UserCustomReputations
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId && x.CustomTypeId == customType.Id);

        return customRep?.Amount ?? 0;
    }

    /// <summary>
    ///     Adds or updates a role reward for reputation milestones.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="roleId">The role ID.</param>
    /// <param name="repRequired">The reputation required to earn the role.</param>
    /// <param name="removeOnDrop">Whether to remove the role if reputation drops below threshold.</param>
    /// <param name="announceChannel">Optional channel to announce role awards.</param>
    /// <param name="announceDm">Whether to send DM notifications.</param>
    /// <param name="xpReward">Optional XP reward for reaching milestone.</param>
    /// <returns>True if a new role reward was created, false if an existing one was updated.</returns>
    public async Task<bool> AddOrUpdateRoleRewardAsync(ulong guildId, ulong roleId, int repRequired,
        bool removeOnDrop = true, ulong? announceChannel = null, bool announceDm = false, int? xpReward = null)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = await db.RepRoleRewards
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.RoleId == roleId);

        if (existing != null)
        {
            existing.RepRequired = repRequired;
            existing.RemoveOnDrop = removeOnDrop;
            existing.AnnounceChannel = announceChannel;
            existing.AnnounceDM = announceDm;
            existing.XPReward = xpReward;
            await db.UpdateAsync(existing);
            return false; // Updated existing
        }

        var roleReward = new RepRoleRewards
        {
            GuildId = guildId,
            RoleId = roleId,
            RepRequired = repRequired,
            RemoveOnDrop = removeOnDrop,
            AnnounceChannel = announceChannel,
            AnnounceDM = announceDm,
            XPReward = xpReward,
            DateAdded = DateTime.UtcNow
        };
        await db.InsertAsync(roleReward);
        return true; // Created new
    }

    /// <summary>
    ///     Removes a role reward configuration.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="roleId">The role ID.</param>
    /// <returns>True if the role reward was removed, false if it didn't exist.</returns>
    public async Task<bool> RemoveRoleRewardAsync(ulong guildId, ulong roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var deleted = await db.RepRoleRewards
            .Where(x => x.GuildId == guildId && x.RoleId == roleId)
            .DeleteAsync();

        return deleted > 0;
    }

    /// <summary>
    ///     Gets all role rewards for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>List of role rewards ordered by reputation required.</returns>
    public async Task<List<RepRoleRewards>> GetRoleRewardsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.RepRoleRewards
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.RepRequired)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets a specific role reward configuration.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="roleId">The role ID.</param>
    /// <returns>The role reward configuration or null if not found.</returns>
    public async Task<RepRoleRewards?> GetRoleRewardAsync(ulong guildId, ulong roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.RepRoleRewards
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.RoleId == roleId);
    }

    /// <summary>
    ///     Processes all other reactions of the same type on a message to give reputation for any missed reactions.
    /// </summary>
    private async Task ProcessAllOtherReactionsAsync(ulong guildId, ulong channelId, IUserMessage message,
        IEmote targetEmote, string repType, string? baseReason, int? customAmount, int? customCooldownMinutes,
        ulong skipUserId)
    {
        try
        {
            // Find the matching reaction on the message
            var matchingReaction = message.Reactions.FirstOrDefault(kvp =>
                DoesEmoteMatch(kvp.Key, targetEmote));

            if (matchingReaction.Key == null || matchingReaction.Value.ReactionCount <= 1)
                return; // No reactions or only the one we just processed

            // Get all users who reacted with this emote
            var users = await message.GetReactionUsersAsync(matchingReaction.Key, matchingReaction.Value.ReactionCount)
                .FlattenAsync();

            // Process each user's reaction (except the one we just processed and bots)
            foreach (var user in users)
            {
                // Skip the user we just processed, bots, and the message author
                if (user.Id == skipUserId || user.IsBot || user.Id == message.Author.Id)
                    continue;

                try
                {
                    // Give reputation for this user's reaction (without processing all reactions again)
                    await GiveReputationAsync(guildId, user.Id, message.Author.Id, channelId,
                        repType, baseReason, customAmount, customCooldownMinutes: customCooldownMinutes);
                }
                catch (Exception ex)
                {
                    // Log but continue processing other reactions - likely cooldown or other anti-abuse
                    logger.LogDebug(ex,
                        "Could not give reputation from user {UserId} reaction on message {MessageId}: {Message}",
                        user.Id, message.Id, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing all reactions on message {MessageId}", message.Id);
        }
    }

    /// <summary>
    ///     Checks if an emote matches another emote.
    /// </summary>
    private static bool DoesEmoteMatch(IEmote emote1, IEmote emote2)
    {
        return (emote1, emote2) switch
        {
            (Emote custom1, Emote custom2) => custom1.Id == custom2.Id,
            (Emoji unicode1, Emoji unicode2) => unicode1.Name == unicode2.Name,
            _ => false
        };
    }

    #region Role Reward Extensions

    /// <summary>
    ///     Updates just the announcement channel for a role reward.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="roleId">The role ID.</param>
    /// <param name="channelId">The channel ID (null to disable).</param>
    /// <returns>True if updated, false if role reward not found.</returns>
    public async Task<bool> UpdateRoleRewardChannelAsync(ulong guildId, ulong roleId, ulong? channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var roleReward = await db.RepRoleRewards
            .Where(x => x.GuildId == guildId && x.RoleId == roleId)
            .FirstOrDefaultAsync();

        if (roleReward == null)
            return false;

        roleReward.AnnounceChannel = channelId;
        await db.UpdateAsync(roleReward);
        return true;
    }

    #endregion

    #region Configuration Methods

    /// <summary>
    ///     Updates the reputation system enabled status for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="enabled">Whether to enable the system.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetEnabledAsync(ulong guildId, bool enabled)
    {
        var config = await GetOrCreateConfigAsync(guildId);
        config.Enabled = enabled;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);

        // Update cache
        configCache.TryRemove(guildId, out _);
        configCache.TryAdd(guildId, config);
    }

    /// <summary>
    ///     Updates the default cooldown for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="minutes">Cooldown in minutes.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetDefaultCooldownAsync(ulong guildId, int minutes)
    {
        var config = await GetOrCreateConfigAsync(guildId);
        config.DefaultCooldownMinutes = minutes;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);

        // Update cache
        configCache.TryRemove(guildId, out _);
        configCache.TryAdd(guildId, config);
    }

    /// <summary>
    ///     Updates the daily limit for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="limit">Daily limit.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetDailyLimitAsync(ulong guildId, int limit)
    {
        var config = await GetOrCreateConfigAsync(guildId);
        config.DailyLimit = limit;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);

        // Update cache
        configCache.TryRemove(guildId, out _);
        configCache.TryAdd(guildId, config);
    }

    /// <summary>
    ///     Updates the weekly limit for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="limit">Weekly limit (null to disable).</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetWeeklyLimitAsync(ulong guildId, int? limit)
    {
        var config = await GetOrCreateConfigAsync(guildId);
        config.WeeklyLimit = limit;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);

        // Update cache
        configCache.TryRemove(guildId, out _);
        configCache.TryAdd(guildId, config);
    }

    /// <summary>
    ///     Updates the minimum account age requirement for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="days">Minimum account age in days.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetMinAccountAgeAsync(ulong guildId, int days)
    {
        var config = await GetOrCreateConfigAsync(guildId);
        config.MinAccountAgeDays = days;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);

        // Update cache
        configCache.TryRemove(guildId, out _);
        configCache.TryAdd(guildId, config);
    }

    /// <summary>
    ///     Updates the minimum server membership requirement for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="hours">Minimum membership hours.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetMinServerMembershipAsync(ulong guildId, int hours)
    {
        var config = await GetOrCreateConfigAsync(guildId);
        config.MinServerMembershipHours = hours;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);

        // Update cache
        configCache.TryRemove(guildId, out _);
        configCache.TryAdd(guildId, config);
    }

    /// <summary>
    ///     Updates the minimum message count requirement for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="count">Minimum message count.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetMinMessageCountAsync(ulong guildId, int count)
    {
        var config = await GetOrCreateConfigAsync(guildId);
        config.MinMessageCount = count;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);

        // Update cache
        configCache.TryRemove(guildId, out _);
        configCache.TryAdd(guildId, config);
    }

    /// <summary>
    ///     Updates negative reputation setting for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="enabled">Whether to enable negative reputation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetNegativeReputationAsync(ulong guildId, bool enabled)
    {
        var config = await GetOrCreateConfigAsync(guildId);
        config.EnableNegativeRep = enabled;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);

        // Update cache
        configCache.TryRemove(guildId, out _);
        configCache.TryAdd(guildId, config);
    }

    /// <summary>
    ///     Updates anonymous reputation setting for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="enabled">Whether to enable anonymous reputation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetAnonymousReputationAsync(ulong guildId, bool enabled)
    {
        var config = await GetOrCreateConfigAsync(guildId);
        config.EnableAnonymous = enabled;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);

        // Update cache
        configCache.TryRemove(guildId, out _);
        configCache.TryAdd(guildId, config);
    }

    /// <summary>
    ///     Updates the notification channel for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The notification channel ID (null to disable).</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetNotificationChannelAsync(ulong guildId, ulong? channelId)
    {
        var config = await GetOrCreateConfigAsync(guildId);
        config.NotificationChannel = channelId;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);

        // Update cache
        configCache.TryRemove(guildId, out _);
        configCache.TryAdd(guildId, config);
    }

    /// <summary>
    ///     Updates decay settings for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="enabled">Whether to enable decay.</param>
    /// <param name="type">The decay type.</param>
    /// <param name="amount">The decay amount.</param>
    /// <param name="inactiveDays">Days of inactivity before decay starts.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetDecaySettingsAsync(ulong guildId, bool enabled, string? type = null, int? amount = null,
        int? inactiveDays = null)
    {
        var config = await GetOrCreateConfigAsync(guildId);
        config.EnableDecay = enabled;

        if (type != null) config.DecayType = type;
        if (amount.HasValue) config.DecayAmount = amount.Value;
        if (inactiveDays.HasValue) config.DecayInactiveDays = inactiveDays.Value;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);

        // Update cache
        configCache.TryRemove(guildId, out _);
        configCache.TryAdd(guildId, config);
    }

    /// <summary>
    ///     Gets the configuration for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The guild's reputation configuration.</returns>
    public async Task<RepConfig> GetConfigAsync(ulong guildId)
    {
        return await GetOrCreateConfigAsync(guildId);
    }

    #endregion

    #region User Reputation Management

    /// <summary>
    ///     Takes reputation from a user (admin command).
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="adminId">The admin performing the action.</param>
    /// <returns>The user's new total reputation.</returns>
    public async Task<int> TakeReputationAsync(ulong guildId, ulong userId, int amount, ulong adminId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var userRep = await db.UserReputations
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .FirstOrDefaultAsync();

        if (userRep == null)
            return 0;

        userRep.TotalRep = Math.Max(0, userRep.TotalRep - amount);
        await db.UpdateAsync(userRep);

        // Add to history
        var historyEntry = new RepHistory
        {
            GiverId = adminId,
            ReceiverId = userId,
            GuildId = guildId,
            ChannelId = 0, // Admin action, no specific channel
            Amount = -amount,
            RepType = "admin_take",
            Reason = $"Admin removal by {adminId}",
            IsAnonymous = false,
            Timestamp = DateTime.UtcNow
        };
        await db.InsertAsync(historyEntry);

        // Check for role changes
        await CheckRoleRewardsAsync(guildId, userId, userRep.TotalRep);

        return userRep.TotalRep;
    }

    /// <summary>
    ///     Sets a user's reputation to a specific value (admin command).
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="amount">The amount to set reputation to.</param>
    /// <param name="adminId">The admin performing the action.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetReputationAsync(ulong guildId, ulong userId, int amount, ulong adminId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var userRep = await db.UserReputations
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .FirstOrDefaultAsync();

        if (userRep == null)
        {
            userRep = new UserReputation
            {
                UserId = userId, GuildId = guildId, TotalRep = amount, DateAdded = DateTime.UtcNow
            };
            await db.InsertAsync(userRep);
        }
        else
        {
            userRep.TotalRep = amount;
            await db.UpdateAsync(userRep);
        }

        // Add to history
        var historyEntry = new RepHistory
        {
            GiverId = adminId,
            ReceiverId = userId,
            GuildId = guildId,
            ChannelId = 0, // Admin action, no specific channel
            Amount = amount,
            RepType = "admin_set",
            Reason = $"Admin set by {adminId}",
            IsAnonymous = false,
            Timestamp = DateTime.UtcNow
        };
        await db.InsertAsync(historyEntry);

        // Check for role changes
        await CheckRoleRewardsAsync(guildId, userId, amount);
    }

    /// <summary>
    ///     Freezes a user's reputation, preventing them from gaining or losing reputation.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task FreezeUserAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var userRep = await db.UserReputations
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .FirstOrDefaultAsync();

        if (userRep == null)
        {
            userRep = new UserReputation
            {
                UserId = userId, GuildId = guildId, IsFrozen = true, DateAdded = DateTime.UtcNow
            };
            await db.InsertAsync(userRep);
        }
        else
        {
            userRep.IsFrozen = true;
            await db.UpdateAsync(userRep);
        }
    }

    /// <summary>
    ///     Unfreezes a user's reputation, allowing them to gain and lose reputation again.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>True if the user was unfrozen, false if they weren't frozen.</returns>
    public async Task<bool> UnfreezeUserAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var userRep = await db.UserReputations
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .FirstOrDefaultAsync();

        if (userRep == null || !userRep.IsFrozen)
            return false;

        userRep.IsFrozen = false;
        await db.UpdateAsync(userRep);
        return true;
    }

    /// <summary>
    ///     Resets reputation for a specific user in the guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>True if data was reset, false if no data existed.</returns>
    public async Task<bool> ResetUserReputationAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var deletedRep = await db.UserReputations
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .DeleteAsync();

        var deletedHistory = await db.RepHistories
            .Where(x => x.ReceiverId == userId && x.GuildId == guildId)
            .DeleteAsync();

        var deletedCooldowns = await db.RepCooldowns
            .Where(x => x.ReceiverId == userId && x.GuildId == guildId)
            .DeleteAsync();

        var deletedBadges = await db.RepBadges
            .Where(x => x.UserId == userId && x.GuildId == guildId)
            .DeleteAsync();

        return deletedRep > 0 || deletedHistory > 0 || deletedCooldowns > 0 || deletedBadges > 0;
    }

    /// <summary>
    ///     Resets ALL reputation data for the entire guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ResetAllReputationAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        await db.UserReputations.Where(x => x.GuildId == guildId).DeleteAsync();
        await db.RepHistories.Where(x => x.GuildId == guildId).DeleteAsync();
        await db.RepCooldowns.Where(x => x.GuildId == guildId).DeleteAsync();
        await db.RepBadges.Where(x => x.GuildId == guildId).DeleteAsync();
        await db.UserCustomReputations.Where(x => x.GuildId == guildId).DeleteAsync();
    }

    #endregion

    #region Channel Configuration

    /// <summary>
    ///     Sets or updates channel-specific reputation configuration.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="state">The channel state (enabled/disabled/readonly).</param>
    /// <param name="multiplier">The reputation multiplier for this channel.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetChannelConfigAsync(ulong guildId, ulong channelId, string state, decimal multiplier)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var channelConfig = await db.RepChannelConfigs
            .Where(x => x.GuildId == guildId && x.ChannelId == channelId)
            .FirstOrDefaultAsync();

        if (channelConfig == null)
        {
            channelConfig = new RepChannelConfig
            {
                GuildId = guildId,
                ChannelId = channelId,
                State = state.ToLower(),
                Multiplier = multiplier,
                DateAdded = DateTime.UtcNow
            };
            await db.InsertAsync(channelConfig);
        }
        else
        {
            channelConfig.State = state.ToLower();
            channelConfig.Multiplier = multiplier;
            await db.UpdateAsync(channelConfig);
        }

        // Update cache
        channelConfigCache.TryRemove((guildId, channelId), out _);
        channelConfigCache.TryAdd((guildId, channelId), channelConfig);
    }

    /// <summary>
    ///     Gets all channel configurations for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>List of channel configurations.</returns>
    public async Task<List<RepChannelConfig>> GetChannelConfigsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.RepChannelConfigs
            .Where(x => x.GuildId == guildId)
            .ToListAsync();
    }

    /// <summary>
    ///     Removes channel configuration.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>True if removed, false if didn't exist.</returns>
    public async Task<bool> RemoveChannelConfigAsync(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var deleted = await db.RepChannelConfigs
            .Where(x => x.GuildId == guildId && x.ChannelId == channelId)
            .DeleteAsync();

        if (deleted > 0)
        {
            channelConfigCache.TryRemove((guildId, channelId), out _);
            return true;
        }

        return false;
    }

    #endregion

    #region Reaction Configuration

    /// <summary>
    ///     Adds or updates a reaction-based reputation configuration.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="emoji">The emoji string.</param>
    /// <param name="amount">The reputation amount.</param>
    /// <param name="repType">The reputation type.</param>
    /// <returns>True if added, false if updated.</returns>
    public async Task<bool> AddOrUpdateReactionConfigAsync(ulong guildId, string emoji, int amount, string repType)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Parse emoji
        ulong? emojiId = null;
        var emojiName = emoji;

        if (Emote.TryParse(emoji, out var customEmote))
        {
            emojiId = customEmote.Id;
            emojiName = customEmote.Name;
        }

        var existingConfig = await db.RepReactionConfigs
            .Where(x => x.GuildId == guildId && x.EmojiName == emojiName && x.EmojiId == emojiId)
            .FirstOrDefaultAsync();

        var isNew = existingConfig == null;

        if (existingConfig != null)
        {
            existingConfig.RepAmount = amount;
            existingConfig.RepType = repType.ToLower();
            existingConfig.IsEnabled = true;
            await db.UpdateAsync(existingConfig);
        }
        else
        {
            var reactionConfig = new RepReactionConfig
            {
                GuildId = guildId,
                EmojiName = emojiName,
                EmojiId = emojiId,
                RepAmount = amount,
                RepType = repType.ToLower(),
                IsEnabled = true
            };
            await db.InsertAsync(reactionConfig);
            existingConfig = reactionConfig;
        }

        // Update cache
        if (!reactionConfigCache.ContainsKey(guildId))
            reactionConfigCache.TryAdd(guildId, []);

        if (!isNew)
        {
            reactionConfigCache[guildId].RemoveAll(x => x.EmojiName == emojiName && x.EmojiId == emojiId);
        }

        reactionConfigCache[guildId].Add(existingConfig);

        return isNew;
    }

    /// <summary>
    ///     Removes a reaction configuration.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="emoji">The emoji string.</param>
    /// <returns>True if removed, false if didn't exist.</returns>
    public async Task<bool> RemoveReactionConfigAsync(ulong guildId, string emoji)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Parse emoji
        ulong? emojiId = null;
        var emojiName = emoji;

        if (Emote.TryParse(emoji, out var customEmote))
        {
            emojiId = customEmote.Id;
            emojiName = customEmote.Name;
        }

        var deleted = await db.RepReactionConfigs
            .Where(x => x.GuildId == guildId && x.EmojiName == emojiName && x.EmojiId == emojiId)
            .DeleteAsync();

        if (deleted > 0 && reactionConfigCache.TryGetValue(guildId, out var configs))
        {
            configs.RemoveAll(x => x.EmojiName == emojiName && x.EmojiId == emojiId);
        }

        return deleted > 0;
    }

    /// <summary>
    ///     Gets all reaction configurations for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>List of reaction configurations.</returns>
    public async Task<List<RepReactionConfig>> GetReactionConfigsAsync(ulong guildId)
    {
        if (reactionConfigCache.TryGetValue(guildId, out var cached))
            return cached.ToList(); // Return a copy

        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.RepReactionConfigs
            .Where(x => x.GuildId == guildId)
            .ToListAsync();
    }

    #endregion

    #region Custom Type Management

    /// <summary>
    ///     Adds a custom reputation type.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="typeName">The internal name of the type.</param>
    /// <param name="displayName">The display name of the type.</param>
    /// <returns>True if added, false if already exists.</returns>
    public async Task<bool> AddCustomTypeAsync(ulong guildId, string typeName, string displayName)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = await db.RepCustomTypes
            .Where(x => x.GuildId == guildId && x.TypeName == typeName.ToLower())
            .FirstOrDefaultAsync();

        if (existing != null)
            return false;

        var customType = new RepCustomType
        {
            GuildId = guildId,
            TypeName = typeName.ToLower(),
            DisplayName = displayName,
            DateAdded = DateTime.UtcNow,
            IsActive = true
        };

        await db.InsertAsync(customType);
        return true;
    }

    /// <summary>
    ///     Removes a custom reputation type.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="typeName">The type name to remove.</param>
    /// <returns>True if removed, false if didn't exist.</returns>
    public async Task<bool> RemoveCustomTypeAsync(ulong guildId, string typeName)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var deleted = await db.RepCustomTypes
            .Where(x => x.GuildId == guildId && x.TypeName == typeName.ToLower())
            .DeleteAsync();

        return deleted > 0;
    }

    /// <summary>
    ///     Gets all custom reputation types for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>List of custom reputation types.</returns>
    public async Task<List<RepCustomType>> GetCustomTypesAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.RepCustomTypes
            .Where(x => x.GuildId == guildId)
            .ToListAsync();
    }

    #endregion

    #region Event Management

    /// <summary>
    ///     Creates a reputation multiplier event.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The event name.</param>
    /// <param name="multiplier">The reputation multiplier.</param>
    /// <param name="durationHours">Duration in hours.</param>
    /// <returns>The created event.</returns>
    public async Task<RepEvent> CreateEventAsync(ulong guildId, string name, decimal multiplier, int durationHours)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var endTime = DateTime.UtcNow.AddHours(durationHours);
        var repEvent = new RepEvent
        {
            GuildId = guildId,
            Name = name,
            Multiplier = multiplier,
            StartTime = DateTime.UtcNow,
            EndTime = endTime,
            IsEnabled = true,
            DateAdded = DateTime.UtcNow
        };

        await db.InsertAsync(repEvent);
        return repEvent;
    }

    /// <summary>
    ///     Ends a reputation event early.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The event name.</param>
    /// <returns>True if ended, false if not found or already ended.</returns>
    public async Task<bool> EndEventAsync(ulong guildId, string name)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var repEvent = await db.RepEvents
            .Where(x => x.GuildId == guildId && x.Name == name && x.IsEnabled)
            .FirstOrDefaultAsync();

        if (repEvent == null)
            return false;

        repEvent.IsEnabled = false;
        repEvent.EndTime = DateTime.UtcNow;
        await db.UpdateAsync(repEvent);

        return true;
    }

    /// <summary>
    ///     Gets all active events for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>List of active events.</returns>
    public async Task<List<RepEvent>> GetActiveEventsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.RepEvents
            .Where(x => x.GuildId == guildId && x.IsEnabled && x.EndTime > DateTime.UtcNow)
            .ToListAsync();
    }

    #endregion
}