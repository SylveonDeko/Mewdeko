using System.Text.Json;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Xp.Events;
using Mewdeko.Modules.Xp.Extensions;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Services.Strings;
using StackExchange.Redis;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     Manages XP rewards and notifications.
/// </summary>
public class XpRewardManager : INService
{
    private readonly XpCacheManager cacheManager;
    private readonly DiscordShardedClient client;
    private readonly ICurrencyService currencyService;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<XpRewardManager> logger;
    private readonly GeneratedBotStrings Strings;
    private readonly XpService xpService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpRewardManager" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database factory.</param>
    /// <param name="currencyService">The currency service.</param>
    /// <param name="cacheManager">The cache manager.</param>
    /// <param name="strings">The localized strings service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="eventHandler">The event handler service.</param>
    /// <param name="xpService">The xp service.</param>
    public XpRewardManager(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        ICurrencyService currencyService,
        XpCacheManager cacheManager, GeneratedBotStrings strings, ILogger<XpRewardManager> logger,
        EventHandler eventHandler, XpService xpService)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.currencyService = currencyService;
        this.cacheManager = cacheManager;
        Strings = strings;
        this.logger = logger;
        this.xpService = xpService;

        // Subscribe individual methods to XP level change events for better separation of concerns
        eventHandler.Subscribe("XpLevelChanged", "XpRewardManager-Notifications", HandleLevelUpNotificationAsync);
        eventHandler.Subscribe("XpLevelChanged", "XpRewardManager-RoleRewards", HandleRoleRewardsAsync);
        eventHandler.Subscribe("XpLevelChanged", "XpRewardManager-CurrencyRewards", HandleCurrencyRewardsAsync);
    }

    /// <summary>
    ///     Sets a role reward for a specific level.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="level">The level to set the reward for.</param>
    /// <param name="roleId">The role ID to award, or null to remove the reward.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetRoleRewardAsync(ulong guildId, int level, ulong? roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get existing reward using LinqToDB
        var existingReward = await db.XpRoleRewards
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Level == level);

        if (roleId.HasValue)
        {
            XpRoleReward rewardToCache;

            if (existingReward != null)
            {
                existingReward.RoleId = roleId.Value;
                await db.UpdateAsync(existingReward);
                rewardToCache = existingReward;
            }
            else
            {
                var newReward = new XpRoleReward
                {
                    GuildId = guildId, Level = level, RoleId = roleId.Value
                };
                await db.InsertAsync(newReward);
                rewardToCache = newReward;
            }

            // Immediately cache the new/updated reward
            var cacheKey = $"xp:rewards:{guildId}:role:{level}";
            var serializedReward = JsonSerializer.Serialize(rewardToCache);
            await cacheManager.GetRedisDatabase().StringSetAsync(cacheKey, serializedReward);

            logger.LogInformation("Set and cached role reward for guild {GuildId} level {Level}: Role {RoleId}",
                guildId, level, roleId.Value);
        }
        else if (existingReward != null)
        {
            // Delete from database
            await db.XpRoleRewards
                .Where(x => x.Id == existingReward.Id)
                .DeleteAsync();

            // Clear from cache
            await cacheManager.GetRedisDatabase().KeyDeleteAsync($"xp:rewards:{guildId}:role:{level}");

            logger.LogInformation("Removed role reward for guild {GuildId} level {Level}", guildId, level);
        }
    }

    /// <summary>
    ///     Sets a currency reward for a specific level.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="level">The level to set the reward for.</param>
    /// <param name="amount">The amount of currency to award, or 0 to remove the reward.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetCurrencyRewardAsync(ulong guildId, int level, long amount)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get existing reward using LinqToDB
        var existingReward = await db.XpCurrencyRewards
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Level == level);

        if (amount > 0)
        {
            if (existingReward != null)
            {
                existingReward.Amount = amount;
                // Update using LinqToDB
                await db.UpdateAsync(existingReward);
            }
            else
            {
                var newReward = new XpCurrencyReward
                {
                    GuildId = guildId, Level = level, Amount = amount
                };
                // Insert using LinqToDB
                await db.InsertAsync(newReward);
            }
        }
        else if (existingReward != null)
        {
            // Delete using LinqToDB
            await db.XpCurrencyRewards
                .Where(x => x.Id == existingReward.Id)
                .DeleteAsync();
        }

        // Clear cache
        await cacheManager.GetRedisDatabase().KeyDeleteAsync($"xp:rewards:{guildId}:currency:{level}");
    }

    /// <summary>
    ///     Sends XP level-up notifications using customizable message templates.
    /// </summary>
    /// <param name="notifications">The list of notifications to send.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendNotificationsAsync(List<XpNotification> notifications)
    {
        if (notifications.Count == 0)
            return;

        foreach (var notification in notifications)
        {
            try
            {
                var guild = client.GetGuild(notification.GuildId);
                var user = guild?.GetUser(notification.UserId);

                if (guild == null || user == null)
                    continue;

                // Get XP settings and user preferences
                var settings = await cacheManager.GetGuildXpSettingsAsync(notification.GuildId);
                var userPrefs = await GetUserPreferencesAsync(notification.UserId);
                var pingsDisabled = userPrefs?.LevelUpPingsDisabled ?? false;
                var isFirstLevelUp = !userPrefs.LevelUpInfoShown;

                // Get custom level-up messages for this guild
                var customMessages = await GetCustomLevelUpMessagesAsync(notification.GuildId);
                string messageTemplate;

                if (customMessages.Count > 0)
                {
                    // Pick a random message from the enabled ones
                    var random = new Random();
                    messageTemplate = customMessages[random.Next(customMessages.Count)];
                }
                else
                {
                    // Use default embed - created directly in notification methods
                    messageTemplate = null;
                }

                // Get user stats for placeholders
                var userStats = await xpService.GetUserXpStatsAsync(notification.GuildId, notification.UserId);
                var currentLevelXp = XpCalculator.CalculateLevelXp(userStats.TotalXp, notification.Level,
                    (XpCurveType)settings.XpCurveType);
                var nextLevelXp =
                    XpCalculator.CalculateXpForLevel(notification.Level + 1, (XpCurveType)settings.XpCurveType) -
                    XpCalculator.CalculateXpForLevel(notification.Level, (XpCurveType)settings.XpCurveType);
                var rank = await GetUserRankAsync(notification.GuildId, notification.UserId);

                // Build the replacer with all XP placeholders
                var replacer = new ReplacementBuilder()
                    .WithDefault(user, null, guild, client)
                    .WithXpPlaceholders(
                        user,
                        guild,
                        null, // We don't track the specific channel for notifications
                        notification.Level - 1,
                        notification.Level,
                        (int)userStats!.TotalXp,
                        (int)currentLevelXp,
                        (int)nextLevelXp,
                        0, // We don't track this in notifications currently
                        rank,
                        null,
                        pingsDisabled)
                    .Build();

                // Determine the target channel
                ITextChannel? targetChannel = null;
                if (notification.NotificationType == XpNotificationType.Channel)
                {
                    // Use custom level-up channel if set, otherwise use the notification channel
                    var channelId = settings.LevelUpChannel != 0 ? settings.LevelUpChannel : notification.ChannelId;
                    targetChannel = guild.GetTextChannel(channelId);
                }

                // Send the notification
                if (notification.NotificationType == XpNotificationType.Dm)
                {
                    // Skip DM notifications if user has level-up pings disabled
                    if (!pingsDisabled)
                    {
                        if (messageTemplate != null)
                        {
                            var processedMessage = ReplacementBuilderExtensions.ProcessLevelUpMessage(
                                messageTemplate, replacer, user, pingsDisabled);
                            await SendDmNotificationAsync(user, processedMessage, guild.Id);
                        }
                        else
                        {
                            await SendDefaultDmNotificationAsync(user, replacer, guild.Id, notification.Level,
                                userStats!.TotalXp, rank, pingsDisabled);
                        }
                    }
                }
                else if (targetChannel != null)
                {
                    if (messageTemplate != null)
                    {
                        var processedMessage = ReplacementBuilderExtensions.ProcessLevelUpMessage(
                            messageTemplate, replacer, user, pingsDisabled);
                        await SendChannelNotificationAsync(targetChannel, processedMessage, guild);
                    }
                    else
                    {
                        await SendDefaultChannelNotificationAsync(targetChannel, replacer, guild, user,
                            notification.Level, userStats!.TotalXp, rank, pingsDisabled);
                    }
                }

                // Send first-time info message if this is the user's first level-up notification
                if (isFirstLevelUp)
                {
                    await SendFirstTimeInfoMessageAsync(user, guild, notification.NotificationType, targetChannel);
                    await MarkUserAsSeenLevelUpInfoAsync(notification.UserId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending XP notification for {UserId} in {GuildId}",
                    notification.UserId, notification.GuildId);
            }
        }
    }

    /// <summary>
    ///     Sends a DM notification to a user.
    /// </summary>
    /// <param name="user">The user to send the notification to.</param>
    /// <param name="message">The processed message content.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    private async Task SendDmNotificationAsync(IGuildUser user, string message, ulong guildId)
    {
        try
        {
            var dmChannel = await user.CreateDMChannelAsync();
            if (dmChannel != null)
            {
                // Try to parse as embed first, fall back to plain text
                if (SmartEmbed.TryParse(message, guildId, out var embed, out var plainText, out var components))
                {
                    await dmChannel.SendMessageAsync(plainText, embeds: embed, components: components?.Build());
                }
                else
                {
                    // Create a default embed with the message
                    await dmChannel.SendMessageAsync(
                        embed: new EmbedBuilder()
                            .WithColor(Color.Green)
                            .WithDescription(message)
                            .WithTitle(Strings.LevelUpTitle(guildId))
                            .Build()
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send DM notification to user {UserId}", user.Id);
        }
    }

    /// <summary>
    ///     Sends a channel notification.
    /// </summary>
    /// <param name="channel">The channel to send the notification to.</param>
    /// <param name="message">The processed message content.</param>
    /// <param name="guild">The guild for permission checking.</param>
    private async Task SendChannelNotificationAsync(ITextChannel channel, string message, IGuild guild)
    {
        try
        {
            var botUser = await guild.GetUserAsync(client.CurrentUser.Id);
            var perms = botUser.GetPermissions(channel);

            if (!perms.Has(ChannelPermission.SendMessages))
                return;

            // Try to parse as embed first, fall back to plain text
            if (SmartEmbed.TryParse(message, guild.Id, out var embed, out var plainText, out var components))
            {
                await channel.SendMessageAsync(plainText, embeds: embed, components: components?.Build());
            }
            else
            {
                // Create a default embed with the message
                await channel.SendMessageAsync(
                    embed: new EmbedBuilder()
                        .WithColor(Color.Green)
                        .WithDescription(message)
                        .Build()
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send channel notification to {ChannelId}", channel.Id);
        }
    }

    /// <summary>
    ///     Sends a default DM notification with a beautiful embed.
    /// </summary>
    /// <param name="user">The user to send the notification to.</param>
    /// <param name="replacer">The replacer with placeholders.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <param name="level">The new level.</param>
    /// <param name="totalXp">The user's total XP.</param>
    /// <param name="rank">The user's rank.</param>
    /// <param name="pingsDisabled">Whether pings are disabled.</param>
    private async Task SendDefaultDmNotificationAsync(IGuildUser user, Replacer replacer, ulong guildId, int level,
        long totalXp, int rank, bool pingsDisabled)
    {
        try
        {
            var dmChannel = await user.CreateDMChannelAsync();
            if (dmChannel != null)
            {
                var userMention = pingsDisabled ? user.Username.EscapeWeirdStuff() : user.Mention;

                var embed = new EmbedBuilder()
                    .WithTitle(Strings.XpLevelUpTitle(user.Guild.Id))
                    .WithDescription(Strings.XpLevelUpDesc(user.Guild.Id, userMention, level))
                    .WithColor(new Color(0x5865F2))
                    .WithThumbnailUrl(user.GetAvatarUrl(size: 128) ?? user.GetDefaultAvatarUrl())
                    .AddField(Strings.XpLevelUpTotalXp(user.Guild.Id), totalXp.ToString("N0"), true)
                    .AddField(Strings.XpLevelUpServerRank(user.Guild.Id), $"#{rank}", true)
                    .AddField(Strings.XpLevelUpNewLevel(user.Guild.Id), level.ToString(), true)
                    .WithFooter($"{user.Guild.Name}", user.Guild.IconUrl)
                    .WithCurrentTimestamp()
                    .Build();

                await dmChannel.SendMessageAsync(embed: embed);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send default DM notification to user {UserId}", user.Id);
        }
    }

    /// <summary>
    ///     Sends a default channel notification with a beautiful embed.
    /// </summary>
    /// <param name="channel">The channel to send the notification to.</param>
    /// <param name="replacer">The replacer with placeholders.</param>
    /// <param name="guild">The guild.</param>
    /// <param name="user">The user who leveled up.</param>
    /// <param name="level">The new level.</param>
    /// <param name="totalXp">The user's total XP.</param>
    /// <param name="rank">The user's rank.</param>
    /// <param name="pingsDisabled">Whether pings are disabled.</param>
    private async Task SendDefaultChannelNotificationAsync(ITextChannel channel, Replacer replacer, IGuild guild,
        IGuildUser user, int level, long totalXp, int rank, bool pingsDisabled)
    {
        try
        {
            var botUser = await guild.GetUserAsync(client.CurrentUser.Id);
            var perms = botUser.GetPermissions(channel);

            if (!perms.Has(ChannelPermission.SendMessages))
                return;

            var userMention = pingsDisabled ? user.Username.EscapeWeirdStuff() : user.Mention;

            var embed = new EmbedBuilder()
                .WithTitle(Strings.XpLevelUpTitle(guild.Id))
                .WithDescription(Strings.XpLevelUpDesc(guild.Id, userMention, level))
                .WithColor(new Color(0x5865F2))
                .WithThumbnailUrl(user.GetAvatarUrl(size: 128) ?? user.GetDefaultAvatarUrl())
                .AddField(Strings.XpLevelUpTotalXp(guild.Id), totalXp.ToString("N0"), true)
                .AddField(Strings.XpLevelUpServerRank(guild.Id), $"#{rank}", true)
                .AddField(Strings.XpLevelUpNewLevel(guild.Id), level.ToString(), true)
                .WithFooter($"{guild.Name}", guild.IconUrl)
                .WithCurrentTimestamp()
                .Build();

            await channel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send default channel notification to {ChannelId}", channel.Id);
        }
    }

    /// <summary>
    ///     Sends an informational message to first-time level-up users explaining how to disable notifications.
    /// </summary>
    /// <param name="user">The user who leveled up.</param>
    /// <param name="guild">The guild.</param>
    /// <param name="notificationType">The notification type preference.</param>
    /// <param name="targetChannel">The target channel (if channel notification).</param>
    private async Task SendFirstTimeInfoMessageAsync(IGuildUser user, IGuild guild, XpNotificationType notificationType,
        ITextChannel? targetChannel)
    {
        try
        {
            var embed = new EmbedBuilder()
                .WithTitle(Strings.XpFirstTimeInfoTitle(guild.Id))
                .WithDescription(Strings.XpFirstTimeInfoDesc(guild.Id))
                .WithColor(new Color(0x3498DB))
                .WithFooter(Strings.XpFirstTimeInfoFooter(guild.Id))
                .Build();

            if (notificationType == XpNotificationType.Dm)
            {
                var dmChannel = await user.CreateDMChannelAsync();
                if (dmChannel != null)
                {
                    await dmChannel.SendMessageAsync(embed: embed);
                }
            }
            else if (targetChannel != null)
            {
                var botUser = await guild.GetUserAsync(client.CurrentUser.Id);
                var perms = botUser.GetPermissions(targetChannel);

                if (perms.Has(ChannelPermission.SendMessages))
                {
                    await targetChannel.SendMessageAsync(embed: embed);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send first-time info message to user {UserId}", user.Id);
        }
    }

    /// <summary>
    ///     Marks a user as having seen the level-up info message.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    private async Task MarkUserAsSeenLevelUpInfoAsync(ulong userId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var user = await db.DiscordUsers.FirstOrDefaultAsync(x => x.UserId == userId);
            if (user != null)
            {
                user.LevelUpInfoShown = true;
                await db.UpdateAsync(user);
            }
            else
            {
                // Create user record if it doesn't exist
                var newUser = new DiscordUser
                {
                    UserId = userId, LevelUpInfoShown = true, DateAdded = DateTime.UtcNow
                };
                await db.InsertAsync(newUser);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mark user {UserId} as having seen level-up info", userId);
        }
    }

    /// <summary>
    ///     Gets user preferences from the database.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The user's preferences or null if not found.</returns>
    private async Task<DiscordUser?> GetUserPreferencesAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.DiscordUsers.FirstOrDefaultAsync(x => x.UserId == userId);
    }

    /// <summary>
    ///     Gets custom level-up messages for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>List of enabled custom messages.</returns>
    private async Task<List<string>> GetCustomLevelUpMessagesAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var messages = await db.XpLevelUpMessages
            .Where(x => x.GuildId == guildId && x.IsEnabled)
            .Select(x => x.MessageContent)
            .ToListAsync();

        return messages;
    }

    /// <summary>
    ///     Gets a user's rank in the guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The user's rank (1-based).</returns>
    private async Task<int> GetUserRankAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var rank = await db.GuildUserXps
            .Where(x => x.GuildId == guildId)
            .CountAsync(x => x.TotalXp >
                             db.GuildUserXps
                                 .Where(y => y.GuildId == guildId && y.UserId == userId)
                                 .Select(y => y.TotalXp)
                                 .FirstOrDefault());

        return rank + 1; // Convert to 1-based ranking
    }

    /// <summary>
    ///     Gets the highest level reward for a user from a group of rewards.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userRewards">The user's rewards.</param>
    /// <returns>The highest level reward.</returns>
    private static async Task<RoleRewardItem?> GetHighestLevelRewardAsync(
        MewdekoDb db,
        ulong guildId,
        IGrouping<ulong, RoleRewardItem> userRewards)
    {
        var rewardRoleLevels = new Dictionary<ulong, int>();

        foreach (var reward in userRewards)
        {
            // Get role reward using LinqToDB
            var roleReward = await db.XpRoleRewards
                .FirstOrDefaultAsync(x => x.GuildId == guildId && x.RoleId == reward.RoleId);

            if (roleReward != null)
            {
                rewardRoleLevels[reward.RoleId] = roleReward.Level;
            }
        }

        if (rewardRoleLevels.Count == 0)
            return null;

        var highestRoleId = rewardRoleLevels.OrderByDescending(x => x.Value).First().Key;

        return userRewards.FirstOrDefault(r => r.RoleId == highestRoleId);
    }

    /// <summary>
    ///     Grants currency rewards to users.
    /// </summary>
    /// <param name="rewards">The list of currency rewards to grant.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task GrantCurrencyRewardsAsync(List<CurrencyRewardItem> rewards)
    {
        if (rewards.Count == 0)
            return;

        foreach (var reward in rewards)
        {
            try
            {
                // Use the currency service to award currency
                await currencyService.AddUserBalanceAsync(reward.UserId, reward.Amount, reward.GuildId);

                // Add transaction record for the reward
                await currencyService.AddTransactionAsync(
                    reward.UserId,
                    reward.Amount,
                    "XP Level-up reward",
                    reward.GuildId);

                logger.LogInformation("Awarded {Amount} currency to {UserId} in {GuildId}",
                    reward.Amount, reward.UserId, reward.GuildId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error granting currency reward to {UserId} in {GuildId}",
                    reward.UserId, reward.GuildId);
            }
        }
    }

    /// <summary>
    ///     Handles XP level change events for notifications only.
    /// </summary>
    /// <param name="eventArgs">The level change event arguments.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleLevelUpNotificationAsync(XpLevelChangedEventArgs eventArgs)
    {
        try
        {
            // Only process notifications for actual level UPS (not downs)
            if (eventArgs.NotificationType != XpNotificationType.None && eventArgs.NewLevel > eventArgs.OldLevel)
            {
                var notification = new XpNotification
                {
                    GuildId = eventArgs.GuildId,
                    UserId = eventArgs.UserId,
                    Level = eventArgs.NewLevel,
                    ChannelId = eventArgs.ChannelId,
                    NotificationType = eventArgs.NotificationType,
                    Sources = eventArgs.Source.ToString()
                };

                await SendNotificationsAsync([notification]).ConfigureAwait(false);
                logger.LogInformation("Sent level up notification for user {UserId} in guild {GuildId}: Level {Level}",
                    eventArgs.UserId, eventArgs.GuildId, eventArgs.NewLevel);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling level up notification for user {UserId} in guild {GuildId}",
                eventArgs.UserId, eventArgs.GuildId);
        }
    }

    /// <summary>
    ///     Handles XP level change events for role rewards only.
    /// </summary>
    /// <param name="eventArgs">The level change event arguments.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleRoleRewardsAsync(XpLevelChangedEventArgs eventArgs)
    {
        try
        {
            var settings = await cacheManager.GetGuildXpSettingsAsync(eventArgs.GuildId);
            var redis = cacheManager.GetRedisDatabase();
            var server = redis.Multiplexer.GetServer(redis.Multiplexer.GetEndPoints().First());
            logger.LogInformation($"Processing rewards for {eventArgs.GuildId}");

            var pattern = $"xp:rewards:{eventArgs.GuildId}:role:*";
            var keys = new List<RedisKey>();

            await foreach (var key in server.KeysAsync(pattern: pattern))
            {
                keys.Add(key);
            }

            if (keys.Count == 0)
                return;

            var values = await redis.StringGetAsync(keys.ToArray());
            var allRoleRewards = new List<XpRoleReward>();

            for (var i = 0; i < values.Length; i++)
            {
                if (values[i].HasValue)
                {
                    var reward = JsonSerializer.Deserialize<XpRoleReward>((string)values[i]);
                    if (reward != null)
                        allRoleRewards.Add(reward);
                }
            }

            var guild = client.GetGuild(eventArgs.GuildId);
            var user = guild?.GetUser(eventArgs.UserId);

            if (guild == null || user == null)
                return;

            if (settings.ExclusiveRoleRewards)
            {
                var allRewardRoleIds = allRoleRewards.Select(r => r.RoleId).ToHashSet();
                var userRewardRoles = user.Roles.Where(r => allRewardRoleIds.Contains(r.Id)).ToList();

                if (userRewardRoles.Count > 0)
                {
                    await user.RemoveRolesAsync(userRewardRoles);
                }

                var qualifyingReward = allRoleRewards
                    .Where(r => r.Level <= eventArgs.NewLevel)
                    .OrderByDescending(r => r.Level)
                    .FirstOrDefault();

                if (qualifyingReward != null)
                {
                    var role = guild.GetRole(qualifyingReward.RoleId);
                    if (role != null && user.Roles.All(r => r.Id != role.Id))
                    {
                        await user.AddRoleAsync(role);
                    }
                }
            }
            else
            {
                var qualifyingRoleIds = allRoleRewards
                    .Where(r => r.Level <= eventArgs.NewLevel)
                    .Select(r => r.RoleId)
                    .ToHashSet();

                var nonQualifyingRoleIds = allRoleRewards
                    .Where(r => r.Level > eventArgs.NewLevel)
                    .Select(r => r.RoleId)
                    .ToHashSet();

                var rolesToRemove = user.Roles.Where(r => nonQualifyingRoleIds.Contains(r.Id)).ToList();
                var rolesToAdd = qualifyingRoleIds
                    .Where(roleId => user.Roles.All(r => r.Id != roleId))
                    .Select(roleId => guild.GetRole(roleId))
                    .Where(role => role != null)
                    .ToList();

                if (rolesToRemove.Count > 0)
                {
                    await user.RemoveRolesAsync(rolesToRemove);
                }

                if (rolesToAdd.Count > 0)
                {
                    await user.AddRolesAsync(rolesToAdd);
                }
            }

            logger.LogInformation("Synchronized role rewards for user {UserId} in guild {GuildId} at level {Level}",
                eventArgs.UserId, eventArgs.GuildId, eventArgs.NewLevel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling role rewards for user {UserId} in guild {GuildId}",
                eventArgs.UserId, eventArgs.GuildId);
        }
    }

    /// <summary>
    ///     Handles XP level change events for currency rewards only.
    /// </summary>
    /// <param name="eventArgs">The level change event arguments.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleCurrencyRewardsAsync(XpLevelChangedEventArgs eventArgs)
    {
        try
        {
            var redis = cacheManager.GetRedisDatabase();
            var server = redis.Multiplexer.GetServer(redis.Multiplexer.GetEndPoints().First());

            var pattern = $"xp:rewards:{eventArgs.GuildId}:currency:*";
            var keys = new List<RedisKey>();

            await foreach (var key in server.KeysAsync(pattern: pattern))
            {
                keys.Add(key);
            }

            if (keys.Count == 0)
                return;

            var values = await redis.StringGetAsync(keys.ToArray());
            var allCurrencyRewards = new List<XpCurrencyReward>();

            for (var i = 0; i < values.Length; i++)
            {
                if (values[i].HasValue)
                {
                    var reward = JsonSerializer.Deserialize<XpCurrencyReward>((string)values[i]);
                    if (reward != null)
                        allCurrencyRewards.Add(reward);
                }
            }

            if (allCurrencyRewards.Count == 0)
                return;

            var currencyRewards = new List<CurrencyRewardItem>();

            if (eventArgs.NewLevel > eventArgs.OldLevel)
            {
                var gainedRewards = allCurrencyRewards
                    .Where(r => r.Level > eventArgs.OldLevel && r.Level <= eventArgs.NewLevel);

                foreach (var reward in gainedRewards)
                {
                    currencyRewards.Add(new CurrencyRewardItem
                    {
                        GuildId = eventArgs.GuildId, UserId = eventArgs.UserId, Amount = reward.Amount
                    });
                }

                if (currencyRewards.Count > 0)
                {
                    await GrantCurrencyRewardsAsync(currencyRewards).ConfigureAwait(false);
                    logger.LogInformation(
                        "Granted {Count} currency rewards for user {UserId} in guild {GuildId}: {OldLevel} -> {NewLevel}",
                        currencyRewards.Count, eventArgs.UserId, eventArgs.GuildId, eventArgs.OldLevel,
                        eventArgs.NewLevel);
                }
            }
            else if (eventArgs.NewLevel < eventArgs.OldLevel)
            {
                var lostRewards = allCurrencyRewards
                    .Where(r => r.Level > eventArgs.NewLevel && r.Level <= eventArgs.OldLevel);

                foreach (var reward in lostRewards)
                {
                    currencyRewards.Add(new CurrencyRewardItem
                    {
                        GuildId = eventArgs.GuildId, UserId = eventArgs.UserId, Amount = -reward.Amount
                    });
                }

                if (currencyRewards.Count > 0)
                {
                    await GrantCurrencyRewardsAsync(currencyRewards).ConfigureAwait(false);
                    logger.LogInformation(
                        "Removed {Count} currency rewards from user {UserId} in guild {GuildId}: {OldLevel} -> {NewLevel}",
                        currencyRewards.Count, eventArgs.UserId, eventArgs.GuildId, eventArgs.OldLevel,
                        eventArgs.NewLevel);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling currency rewards for user {UserId} in guild {GuildId}",
                eventArgs.UserId, eventArgs.GuildId);
        }
    }
}