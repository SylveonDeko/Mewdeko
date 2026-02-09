using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Configs;
using Mewdeko.Modules.Birthday.Common;
using Mewdeko.Modules.UserProfile.Common;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Birthday.Services;

/// <summary>
///     Service for managing birthday announcements and configurations across Discord guilds.
/// </summary>
public class BirthdayService : INService, IDisposable
{
    private readonly SemaphoreSlim cacheLock = new(1, 1);
    private readonly DiscordShardedClient client;
    private readonly BotConfig config;

    // Memory management and caching
    private readonly ConcurrentDictionary<ulong, (BirthdayConfig Config, DateTime Expiry)> configCache = new();
    private readonly Timer dailyCheckTimer;
    private readonly IDataConnectionFactory dbFactory;
    private readonly GuildSettingsService guildSettings;
    private readonly ILogger<BirthdayService> logger;
    private readonly GeneratedBotStrings strings;
    private bool isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BirthdayService" /> class.
    /// </summary>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="dbFactory">Provider for database contexts.</param>
    /// <param name="guildSettings">Service for accessing guild settings.</param>
    /// <param name="config">Bot configuration settings.</param>
    /// <param name="strings">Service for localized strings.</param>
    /// <param name="logger">Logger instance.</param>
    public BirthdayService(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        GuildSettingsService guildSettings,
        BotConfig config,
        GeneratedBotStrings strings,
        ILogger<BirthdayService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.guildSettings = guildSettings;
        this.config = config;
        this.strings = strings;
        this.logger = logger;

        // Set up daily birthday check timer (runs every hour, but checks if it's a new day)
        dailyCheckTimer = new Timer(_ => _ = ProcessDailyBirthdaysAsync(), null,
            TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));
    }

    #region IDisposable

    /// <summary>
    ///     Disposes of the service resources.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed) return;

        dailyCheckTimer?.Dispose();
        cacheLock?.Dispose();

        isDisposed = true;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Invalidates the cached configuration for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID to invalidate cache for.</param>
    private void InvalidateConfigCache(ulong guildId)
    {
        configCache.TryRemove(guildId, out _);
    }

    #endregion

    #region Public Configuration Methods

    /// <summary>
    ///     Gets the birthday configuration for a guild, creating default if none exists.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The birthday configuration.</returns>
    public async Task<BirthdayConfig> GetBirthdayConfigAsync(ulong guildId)
    {
        // Check cache first
        if (configCache.TryGetValue(guildId, out var cached) && cached.Expiry > DateTime.UtcNow)
            return cached.Config;

        await using var db = await dbFactory.CreateConnectionAsync();
        var config = await db.BirthdayConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (config == null)
        {
            // Create default configuration
            config = new BirthdayConfig
            {
                GuildId = guildId,
                BirthdayReminderDays = 0,
                DefaultTimezone = "UTC",
                EnabledFeatures = 0,
                DateAdded = DateTime.UtcNow,
                DateModified = DateTime.UtcNow
            };

            config.Id = await db.InsertWithInt32IdentityAsync(config);
        }

        // Cache for 30 minutes
        await cacheLock.WaitAsync();
        try
        {
            configCache[guildId] = (config, DateTime.UtcNow.AddMinutes(30));
        }
        finally
        {
            cacheLock.Release();
        }

        return config;
    }

    /// <summary>
    ///     Updates the birthday configuration for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="updateAction">Action to update the configuration.</param>
    /// <returns>The updated configuration.</returns>
    public async Task<BirthdayConfig> UpdateBirthdayConfigAsync(ulong guildId, Action<BirthdayConfig> updateAction)
    {
        var config = await GetBirthdayConfigAsync(guildId);
        updateAction(config);
        config.DateModified = DateTime.UtcNow;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);

        // Invalidate cache
        InvalidateConfigCache(guildId);

        return config;
    }

    /// <summary>
    ///     Enables a birthday feature for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="feature">The feature to enable.</param>
    /// <returns>True if the feature was enabled.</returns>
    public async Task<bool> EnableBirthdayFeatureAsync(ulong guildId, BirthdayFeature feature)
    {
        try
        {
            await UpdateBirthdayConfigAsync(guildId, config =>
            {
                config.EnabledFeatures |= (int)feature;
            });
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enable birthday feature {Feature} for guild {GuildId}", feature, guildId);
            return false;
        }
    }

    /// <summary>
    ///     Disables a birthday feature for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="feature">The feature to disable.</param>
    /// <returns>True if the feature was disabled.</returns>
    public async Task<bool> DisableBirthdayFeatureAsync(ulong guildId, BirthdayFeature feature)
    {
        try
        {
            await UpdateBirthdayConfigAsync(guildId, config =>
            {
                config.EnabledFeatures &= ~(int)feature;
            });
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to disable birthday feature {Feature} for guild {GuildId}", feature, guildId);
            return false;
        }
    }

    /// <summary>
    ///     Checks if a birthday feature is enabled for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="feature">The feature to check.</param>
    /// <returns>True if the feature is enabled.</returns>
    public async Task<bool> IsFeatureEnabledAsync(ulong guildId, BirthdayFeature feature)
    {
        var config = await GetBirthdayConfigAsync(guildId);
        return (config.EnabledFeatures & (int)feature) != 0;
    }

    #endregion

    #region User Birthday Management

    /// <summary>
    ///     Toggles birthday announcements for a user.
    /// </summary>
    /// <param name="user">The user to toggle announcements for.</param>
    /// <returns>The new announcement status.</returns>
    public async Task<bool> ToggleBirthdayAnnouncementsAsync(IUser user)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var discordUser = await db.GetOrCreateUser(user);

        discordUser.BirthdayAnnouncementsEnabled = !discordUser.BirthdayAnnouncementsEnabled;
        await db.UpdateAsync(discordUser);

        return discordUser.BirthdayAnnouncementsEnabled;
    }

    /// <summary>
    ///     Sets the timezone for a user's birthday calculations.
    /// </summary>
    /// <param name="user">The user to set the timezone for.</param>
    /// <param name="timezone">The timezone string (e.g., "America/New_York").</param>
    /// <returns>True if the timezone was set successfully.</returns>
    public async Task<bool> SetUserTimezoneAsync(IUser user, string timezone)
    {
        // Basic timezone validation - accept common timezone formats
        var validTimezones = new[]
        {
            "UTC", "GMT", "America/New_York", "America/Chicago", "America/Denver", "America/Los_Angeles",
            "Europe/London", "Europe/Paris", "Europe/Berlin", "Europe/Rome", "Europe/Moscow", "Asia/Tokyo",
            "Asia/Shanghai", "Asia/Kolkata", "Australia/Sydney", "Pacific/Auckland"
        };

        if (!validTimezones.Contains(timezone, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        await using var db = await dbFactory.CreateConnectionAsync();
        var discordUser = await db.GetOrCreateUser(user);

        discordUser.BirthdayTimezone = timezone;
        await db.UpdateAsync(discordUser);

        return true;
    }

    /// <summary>
    ///     Checks if a user can announce their birthday based on privacy settings.
    /// </summary>
    /// <param name="user">The Discord user to check.</param>
    /// <returns>True if the birthday can be announced.</returns>
    public bool CanAnnounceBirthday(DiscordUser user)
    {
        // Must have birthday set
        if (!user.Birthday.HasValue) return false;

        // Must not have private profile
        if (user.ProfilePrivacy == (int)ProfilePrivacyEnum.Private) return false;

        // Must not have birthday display disabled
        if (user.BirthdayDisplayMode == (int)BirthdayDisplayModeEnum.Disabled) return false;

        // Must have opted into announcements
        return user.BirthdayAnnouncementsEnabled;
    }

    /// <summary>
    ///     Gets users with birthdays for a specific date and guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="date">The date to check for birthdays.</param>
    /// <returns>List of users with birthdays on the specified date.</returns>
    public async Task<List<DiscordUser>> GetBirthdayUsersForDateAsync(ulong guildId, DateTime date)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var guild = client.GetGuild(guildId);
        if (guild == null) return [];

        var guildUserIds = guild.Users.Select(u => u.Id).ToHashSet();

        return await db.DiscordUsers
            .Where(u => guildUserIds.Contains(u.UserId) &&
                        u.Birthday.HasValue &&
                        u.Birthday.Value.Month == date.Month &&
                        u.Birthday.Value.Day == date.Day &&
                        u.BirthdayAnnouncementsEnabled &&
                        u.ProfilePrivacy != (int)ProfilePrivacyEnum.Private &&
                        u.BirthdayDisplayMode != (int)BirthdayDisplayModeEnum.Disabled)
            .ToListAsync();
    }


    /// <summary>
    ///     Gets users with birthdays within a date range for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="startDate">The start date for the range.</param>
    /// <param name="days">The number of days to include in the range.</param>
    /// <returns>Dictionary mapping dates to lists of users with birthdays on those dates.</returns>
    public async Task<Dictionary<DateTime, List<DiscordUser>>> GetBirthdayUsersInRangeAsync(ulong guildId,
        DateTime startDate, int days)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var guild = client.GetGuild(guildId);
        if (guild == null) return [];

        var guildUserIds = guild.Users.Select(u => u.Id).ToHashSet();

        // Get all users in the range
        var users = await db.DiscordUsers
            .Where(u => guildUserIds.Contains(u.UserId) &&
                        u.Birthday.HasValue &&
                        u.BirthdayAnnouncementsEnabled &&
                        u.ProfilePrivacy != (int)ProfilePrivacyEnum.Private &&
                        u.BirthdayDisplayMode != (int)BirthdayDisplayModeEnum.Disabled)
            .ToListAsync();

        // Group by date within the range
        var result = new Dictionary<DateTime, List<DiscordUser>>();
        for (var i = 0; i < days; i++)
        {
            var checkDate = startDate.AddDays(i);
            var usersForDate = users
                .Where(u => u.Birthday.HasValue && u.Birthday.Value.Month == checkDate.Month &&
                            u.Birthday.Value.Day == checkDate.Day)
                .ToList();

            if (usersForDate.Any())
            {
                result[checkDate] = usersForDate;
            }
        }

        return result;
    }

    #endregion

    #region Background Processing

    /// <summary>
    ///     Processes daily birthday checks for all guilds.
    /// </summary>
    private async Task ProcessDailyBirthdaysAsync()
    {
        try
        {
            var today = DateTime.UtcNow.Date;

            foreach (var guild in client.Guilds)
            {
                try
                {
                    await ProcessGuildBirthdaysAsync(guild, today);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process birthdays for guild {GuildId}", guild.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process daily birthdays");
        }
    }

    /// <summary>
    ///     Processes birthdays for a specific guild.
    /// </summary>
    /// <param name="guild">The guild to process.</param>
    /// <param name="date">The date to check for birthdays.</param>
    private async Task ProcessGuildBirthdaysAsync(IGuild guild, DateTime date)
    {
        var birthdayConfig = await GetBirthdayConfigAsync(guild.Id);

        // Check if announcements are enabled
        if (!await IsFeatureEnabledAsync(guild.Id, BirthdayFeature.Announcements))
            return;

        // Check if birthday channel is configured
        if (!birthdayConfig.BirthdayChannelId.HasValue)
            return;

        // Check if we've already announced birthdays for today
        if (birthdayConfig.LastAnnouncementDate.HasValue &&
            birthdayConfig.LastAnnouncementDate.Value.Date == date.Date)
        {
            return; // Already announced today
        }

        var channel = await guild.GetTextChannelAsync(birthdayConfig.BirthdayChannelId.Value);
        if (channel == null)
            return;

        var birthdayUsers = await GetBirthdayUsersForDateAsync(guild.Id, date);

        if (!birthdayUsers.Any())
        {
            // No birthdays today, but still update the last announcement date to prevent unnecessary checks
            await UpdateLastAnnouncementDateAsync(guild.Id, date);
            return;
        }

        var announcementsMade = false;

        foreach (var user in birthdayUsers)
        {
            try
            {
                await AnnounceBirthdayAsync(guild, channel, user, birthdayConfig);
                announcementsMade = true;

                // Assign birthday role if configured
                if (birthdayConfig.BirthdayRoleId.HasValue &&
                    await IsFeatureEnabledAsync(guild.Id, BirthdayFeature.BirthdayRole))
                {
                    await AssignBirthdayRoleAsync(guild, user.UserId, birthdayConfig.BirthdayRoleId.Value);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to announce birthday for user {UserId} in guild {GuildId}",
                    user.UserId, guild.Id);
            }
        }

        // Update the last announcement date only if we successfully made announcements
        if (announcementsMade)
        {
            await UpdateLastAnnouncementDateAsync(guild.Id, date);
        }
    }

    /// <summary>
    ///     Updates the last announcement date for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="date">The date of the last announcement.</param>
    private async Task UpdateLastAnnouncementDateAsync(ulong guildId, DateTime date)
    {
        try
        {
            await UpdateBirthdayConfigAsync(guildId, config =>
            {
                config.LastAnnouncementDate = date.Date;
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update last announcement date for guild {GuildId}", guildId);
        }
    }

    /// <summary>
    ///     Announces a user's birthday in the configured channel.
    /// </summary>
    /// <param name="guild">The guild where the announcement is made.</param>
    /// <param name="channel">The channel to announce in.</param>
    /// <param name="user">The user whose birthday is being announced.</param>
    /// <param name="config">The birthday configuration.</param>
    private async Task AnnounceBirthdayAsync(IGuild guild, ITextChannel channel,
        DiscordUser user, BirthdayConfig config)
    {
        var guildUser = await guild.GetUserAsync(user.UserId);
        if (guildUser == null) return;

        // Get the custom message or use default
        var customBirthdayMessage = config.BirthdayMessage ?? "🎉 Happy Birthday %user.mention%! 🎂";

        var replacer = new ReplacementBuilder()
            .WithOverride("%birthday.age%", () =>
            {
                if (!user.Birthday.HasValue) return "Unknown";
                var age = DateTime.UtcNow.Year - user.Birthday.Value.Year;
                if (DateTime.UtcNow < user.Birthday.Value.AddYears(age)) age--;
                return age.ToString();
            })
            .WithDefault(guildUser, channel, guild as SocketGuild, client)
            .Build();

        var parsedMessage = replacer.Replace(customBirthdayMessage);

        // Add ping role if configured
        var finalMessage = parsedMessage;
        if (config.BirthdayPingRoleId.HasValue &&
            await IsFeatureEnabledAsync(guild.Id, BirthdayFeature.PingRole))
        {
            var pingRole = guild.GetRole(config.BirthdayPingRoleId.Value);
            if (pingRole != null)
            {
                finalMessage = $"{pingRole.Mention} {parsedMessage}";
            }
        }

        if (SmartEmbed.TryParse(finalMessage, guild.Id, out var embed, out var plainText, out var components))
        {
            await channel.SendMessageAsync(plainText, embeds: embed, components: components?.Build());
        }
        else
        {
            await channel.SendMessageAsync(finalMessage.SanitizeMentions(true));
        }
    }

    /// <summary>
    ///     Assigns the birthday role to a user temporarily.
    /// </summary>
    /// <param name="guild">The guild where the role is assigned.</param>
    /// <param name="userId">The user to assign the role to.</param>
    /// <param name="roleId">The role ID to assign.</param>
    private async Task AssignBirthdayRoleAsync(IGuild guild, ulong userId, ulong roleId)
    {
        try
        {
            var user = await guild.GetUserAsync(userId);
            var role = guild.GetRole(roleId);

            if (user != null && role != null && !user.RoleIds.Contains(roleId))
            {
                await user.AddRoleAsync(role);

                // Schedule role removal after 24 hours
                _ = Task.Delay(TimeSpan.FromHours(24)).ContinueWith(async _ =>
                {
                    try
                    {
                        var updatedUser = await guild.GetUserAsync(userId);
                        if (updatedUser != null && updatedUser.RoleIds.Contains(roleId))
                        {
                            await updatedUser.RemoveRoleAsync(role);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to remove birthday role from user {UserId}", userId);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to assign birthday role to user {UserId}", userId);
        }
    }

    #endregion
}