using System.Text.Json;
using DataModel;
using LinqToDB;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Xp.Events;
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
    private readonly EventHandler eventHandler;
    private readonly ILogger<XpRewardManager> logger;
    private readonly GeneratedBotStrings Strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="XpRewardManager" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database factory.</param>
    /// <param name="currencyService">The currency service.</param>
    /// <param name="cacheManager">The cache manager.</param>
    public XpRewardManager(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        ICurrencyService currencyService,
        XpCacheManager cacheManager, GeneratedBotStrings strings, ILogger<XpRewardManager> logger,
        EventHandler eventHandler)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.currencyService = currencyService;
        this.cacheManager = cacheManager;
        Strings = strings;
        this.logger = logger;
        this.eventHandler = eventHandler;

        // Subscribe individual methods to XP level change events for better separation of concerns
        eventHandler.Subscribe("XpLevelChanged", "XpRewardManager-Notifications", HandleLevelUpNotificationAsync);
        eventHandler.Subscribe("XpLevelChanged", "XpRewardManager-RoleRewards", HandleRoleRewardsAsync);
        eventHandler.Subscribe("XpLevelChanged", "XpRewardManager-CurrencyRewards", HandleCurrencyRewardsAsync);
    }

    /// <summary>
    ///     Gets the role reward for a specific level.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="level">The level.</param>
    /// <returns>The role reward for the specified level, or null if none exists.</returns>
    public async Task<XpRoleReward?> GetRoleRewardForLevelAsync(MewdekoDb db, ulong guildId, int level)
    {
        // Create a cache key for Redis
        var cacheKey = $"xp:rewards:{guildId}:role:{level}";

        // Get Redis database from cache manager
        var redis = cacheManager.GetRedisDatabase();

        // Try to get from Redis
        var cachedValue = await redis.StringGetAsync(cacheKey);

        if (cachedValue.HasValue)
        {
            // Deserialize the JSON string back to XpRoleReward object
            return JsonSerializer.Deserialize<XpRoleReward>((string)cachedValue);
        }

        // Get from database if not in cache using LinqToDB
        var reward = await db.XpRoleRewards
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Level == level);

        if (reward == null) return null;

        // Serialize the object to JSON and store in Redis
        var serializedReward = JsonSerializer.Serialize(reward);
        await redis.StringSetAsync(cacheKey, serializedReward, TimeSpan.FromMinutes(30));

        return reward;
    }

    /// <summary>
    ///     Gets the currency reward for a specific level.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="level">The level.</param>
    /// <returns>The currency reward for the specified level, or null if none exists.</returns>
    public async Task<XpCurrencyReward?> GetCurrencyRewardForLevelAsync(MewdekoDb db, ulong guildId, int level)
    {
        // Check cache first
        var cacheKey = $"xp:rewards:{guildId}:currency:{level}";
        var redis = cacheManager.GetRedisDatabase();

        // Try to get from Redis
        var cachedValue = await redis.StringGetAsync(cacheKey);

        if (cachedValue.HasValue)
        {
            // Deserialize the JSON string back to XpCurrencyReward object
            return JsonSerializer.Deserialize<XpCurrencyReward>((string)cachedValue);
        }

        // Get from database if not in cache using LinqToDB
        var reward = await db.XpCurrencyRewards
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Level == level);

        if (reward == null) return null;

        // Serialize the object to JSON and store in Redis
        var serializedReward = JsonSerializer.Serialize(reward);
        await redis.StringSetAsync(cacheKey, serializedReward, TimeSpan.FromMinutes(30));

        return reward;
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
            await cacheManager.GetRedisDatabase().StringSetAsync(cacheKey, serializedReward, TimeSpan.FromMinutes(30));

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
    ///     Sends XP level-up notifications.
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

                // Get notification message template
                var settings = await cacheManager.GetGuildXpSettingsAsync(notification.GuildId);
                var messageTemplate = settings.LevelUpMessage;

                // Format the message
                var formattedMessage = messageTemplate
                    .Replace("{UserMention}", user.Mention)
                    .Replace("{UserName}", user.Username)
                    .Replace("{Level}", notification.Level.ToString())
                    .Replace("{Server}", guild.Name)
                    .Replace("{Guild}", guild.Name);

                if (!string.IsNullOrEmpty(notification.Sources))
                {
                    formattedMessage += $" (Source: {notification.Sources})";
                }

                // Send the notification
                if (notification.NotificationType == XpNotificationType.Dm)
                {
                    var dmChannel = await user.CreateDMChannelAsync();
                    if (dmChannel != null)
                    {
                        await dmChannel.SendMessageAsync(
                            embed: new EmbedBuilder()
                                .WithColor(Color.Green)
                                .WithDescription(formattedMessage)
                                .WithTitle(Strings.LevelUpTitle(guild.Id))
                                .Build()
                        );
                    }
                }
                else // Channel
                {
                    var channel = guild.GetTextChannel(notification.ChannelId);
                    var curUser = guild.GetUser(client.CurrentUser.Id);
                    var perms = curUser.GetPermissions(channel);
                    if (channel != null && perms.Has(ChannelPermission.SendMessages))
                    {
                        await channel.SendMessageAsync(
                            embed: new EmbedBuilder()
                                .WithColor(Color.Green)
                                .WithDescription(formattedMessage)
                                .Build()
                        );
                    }
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
    ///     Grants role rewards to users.
    /// </summary>
    /// <param name="rewards">The list of role rewards to grant.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task GrantRoleRewardsAsync(List<RoleRewardItem> rewards)
    {
        if (rewards.Count == 0)
            return;

        // Get unique guilds
        var guildIds = rewards.Select(r => r.GuildId).Distinct().ToList();

        foreach (var guildId in guildIds)
        {
            var guild = client.GetGuild(guildId);
            if (guild == null)
                continue;

            // Get guild settings to check exclusivity
            var settings = await cacheManager.GetGuildXpSettingsAsync(guildId);

            // Process rewards by user
            var userRewards = rewards.Where(r => r.GuildId == guildId).GroupBy(r => r.UserId);

            foreach (var userGroup in userRewards)
            {
                var userId = userGroup.Key;
                var user = guild.GetUser(userId);

                if (user == null)
                    continue;

                try
                {
                    // Apply exclusive role rewards if configured
                    if (settings.ExclusiveRoleRewards)
                    {
                        await ProcessExclusiveRoleRewardsAsync(guild, user, userGroup);
                    }
                    else
                    {
                        // Just add all role rewards
                        foreach (var reward in userGroup)
                        {
                            var role = guild.GetRole(reward.RoleId);
                            if (role != null && !user.Roles.Any(r => r.Id == role.Id))
                            {
                                await user.AddRoleAsync(role);
                                await Task.Delay(100); // Avoid rate limiting
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error granting role reward to {UserId} in {GuildId}", userId, guildId);
                }
            }
        }
    }

    /// <summary>
    ///     Processes exclusive role rewards, removing old rewards and adding new ones.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="user">The user.</param>
    /// <param name="userRewards">The user's rewards.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ProcessExclusiveRoleRewardsAsync(
        SocketGuild guild,
        SocketGuildUser user,
        IGrouping<ulong, RoleRewardItem> userRewards)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get all role rewards for the guild using LinqToDB
        var allRewardRoles = await db.XpRoleRewards
            .Where(r => r.GuildId == guild.Id)
            .Select(r => r.RoleId)
            .ToListAsync();

        // Remove old reward roles first
        foreach (var roleId in user.Roles.Where(r => allRewardRoles.Contains(r.Id)).Select(r => r.Id))
        {
            var role = guild.GetRole(roleId);
            if (role != null)
            {
                await user.RemoveRoleAsync(role);
                await Task.Delay(100); // Avoid rate limiting
            }
        }

        // Get highest level reward in this batch
        var highestReward = await GetHighestLevelRewardAsync(db, guild.Id, userRewards);

        if (highestReward != null)
        {
            var role = guild.GetRole(highestReward.RoleId);
            if (role != null)
            {
                await user.AddRoleAsync(role);
            }
        }
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
            // Only process notifications
            if (eventArgs.NotificationType != XpNotificationType.None)
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
                logger.LogDebug("Sent level up notification for user {UserId} in guild {GuildId}: Level {Level}",
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

            logger.LogDebug("Synchronized role rewards for user {UserId} in guild {GuildId} at level {Level}",
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
                    logger.LogDebug(
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
                    logger.LogDebug(
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