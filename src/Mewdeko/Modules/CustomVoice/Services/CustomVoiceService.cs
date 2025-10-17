using System.Text.Json;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.CustomVoice.Services;

/// <summary>
///     Service for managing custom voice channels.
/// </summary>
public class CustomVoiceService : INService, IUnloadableService
{
    private readonly ConcurrentDictionary<ulong, HashSet<ulong>> activeChannels = new();
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ConcurrentDictionary<ulong, DateTime> emptyChannels = new();
    private readonly ConcurrentDictionary<ulong, Timer> emptyChannelTimers = new();
    private readonly EventHandler eventHandler;
    private readonly SemaphoreSlim @lock = new(1, 1);
    private readonly ILogger<CustomVoiceService> logger;
    private readonly GeneratedBotStrings strings;


    /// <summary>
    ///     Initializes a new instance of the <see cref="CustomVoiceService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="eventHandler">The event handler service.</param>
    /// <param name="strings">The localized strings service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public CustomVoiceService(
        IDataConnectionFactory dbFactory,
        DiscordShardedClient client,
        EventHandler eventHandler,
        GeneratedBotStrings strings, ILogger<CustomVoiceService> logger)
    {
        this.dbFactory = dbFactory;
        this.client = client;
        this.eventHandler = eventHandler;
        this.strings = strings;
        this.logger = logger;

        this.eventHandler.Subscribe("UserVoiceStateUpdated", "CustomVoiceService", OnUserVoiceStateUpdated);
        this.eventHandler.Subscribe("ChannelDestroyed", "CustomVoiceService", OnChannelDestroyed);
        this.eventHandler.Subscribe("JoinedGuild", "CustomVoiceService", OnJoinedGuild);
        this.eventHandler.Subscribe("LeftGuild", "CustomVoiceService", OnLeftGuild);

        // Start cleaning task for empty channels
        _ = StartEmptyChannelCleanupTask();

        // Load all active custom voice channels on startup
        _ = LoadActiveChannelsOnStartup();
    }

    /// <summary>
    ///     Unloads the service and unsubscribes from events.
    /// </summary>
    public Task Unload()
    {
        eventHandler.Unsubscribe("UserVoiceStateUpdated", "CustomVoiceService", OnUserVoiceStateUpdated);
        eventHandler.Unsubscribe("ChannelDestroyed", "CustomVoiceService", OnChannelDestroyed);
        eventHandler.Unsubscribe("JoinedGuild", "CustomVoiceService", OnJoinedGuild);
        eventHandler.Unsubscribe("LeftGuild", "CustomVoiceService", OnLeftGuild);

        // Dispose all empty channel timers
        foreach (var timer in emptyChannelTimers.Values)
        {
            timer.Dispose();
        }

        emptyChannelTimers.Clear();

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Loads all active custom voice channels on startup.
    /// </summary>
    private async Task LoadActiveChannelsOnStartup()
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var allChannels = await dbContext.CustomVoiceChannels.ToListAsync();

            foreach (var channel in allChannels)
            {
                var guild = client.GetGuild(channel.GuildId);
                if (guild == null)
                    continue;

                var voiceChannel = guild.GetVoiceChannel(channel.ChannelId);
                if (voiceChannel == null)
                {
                    // Channel no longer exists, remove from database
                    await dbContext.DeleteAsync(channel);
                    continue;
                }

                // Add to active channels
                activeChannels.AddOrUpdate(channel.GuildId,
                    [channel.ChannelId],
                    (_, set) =>
                    {
                        set.Add(channel.ChannelId);
                        return set;
                    });

                // Check if it's empty and should be tracked for deletion
                if (voiceChannel.Users.Count == 0 && !channel.KeepAlive)
                {
                    var config = await GetOrCreateConfigAsync(channel.GuildId);
                    if (config.DeleteWhenEmpty)
                    {
                        emptyChannels.TryAdd(channel.ChannelId, DateTime.UtcNow);

                        // Schedule deletion after timeout
                        if (config.EmptyChannelTimeout > 0)
                        {
                            var timer = new Timer(async _ =>
                            {
                                await DeleteEmptyChannelAsync(channel.GuildId, channel.ChannelId);
                            }, null, TimeSpan.FromMinutes(config.EmptyChannelTimeout), Timeout.InfiniteTimeSpan);

                            emptyChannelTimers.TryAdd(channel.ChannelId, timer);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading active voice channels on startup");
        }
    }

    /// <summary>
    ///     Gets or creates the custom voice configuration for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The custom voice configuration.</returns>
    public async Task<CustomVoiceConfig> GetOrCreateConfigAsync(ulong guildId)
    {
        if (guildId == 0)
            throw new ArgumentException("Guild ID cannot be 0", nameof(guildId));

        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var config = await dbContext.CustomVoiceConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);

        if (config == null)
        {
            config = new CustomVoiceConfig
            {
                GuildId = guildId,
                DefaultNameFormat = "{0}'s Channel",
                DefaultUserLimit = 0,
                DefaultBitrate = 64,
                DeleteWhenEmpty = true,
                EmptyChannelTimeout = 1,
                AllowMultipleChannels = false,
                AllowNameCustomization = true,
                AllowUserLimitCustomization = true,
                AllowBitrateCustomization = true,
                AllowLocking = true,
                AllowUserManagement = true,
                MaxUserLimit = 99,
                MaxBitrate = 96,
                PersistUserPreferences = true,
                AutoPermission = true
            };

            await dbContext.InsertAsync(config);
        }
        else
        {
            // Auto-migrate bad bitrate values (if stored in bps instead of kbps)
            var needsUpdate = false;

            if (config.DefaultBitrate > 1000)
            {
                logger.LogWarning("Guild {GuildId} has bad DefaultBitrate {OldValue}, converting from bps to kbps",
                    guildId, config.DefaultBitrate);
                config.DefaultBitrate = config.DefaultBitrate / 1000;
                needsUpdate = true;
            }

            if (config.MaxBitrate > 1000)
            {
                logger.LogWarning("Guild {GuildId} has bad MaxBitrate {OldValue}, converting from bps to kbps",
                    guildId, config.MaxBitrate);
                config.MaxBitrate = config.MaxBitrate / 1000;
                needsUpdate = true;
            }

            // Also validate against Discord limits
            var guild = client.GetGuild(guildId);
            if (guild != null)
            {
                var maxAllowed = guild.PremiumTier switch
                {
                    PremiumTier.Tier3 => 384,
                    PremiumTier.Tier2 => 256,
                    PremiumTier.Tier1 => 128,
                    _ => 96
                };

                if (config.MaxBitrate > maxAllowed)
                {
                    logger.LogWarning(
                        "Guild {GuildId} MaxBitrate {OldValue} exceeds Discord limit {MaxAllowed} for Tier {PremiumTier}, capping to {MaxAllowed}",
                        guildId, config.MaxBitrate, maxAllowed, guild.PremiumTier, maxAllowed);
                    config.MaxBitrate = maxAllowed;
                    needsUpdate = true;
                }

                if (config.DefaultBitrate > maxAllowed)
                {
                    logger.LogWarning(
                        "Guild {GuildId} DefaultBitrate {OldValue} exceeds Discord limit {MaxAllowed} for Tier {PremiumTier}, capping to {MaxAllowed}",
                        guildId, config.DefaultBitrate, maxAllowed, guild.PremiumTier, maxAllowed);
                    config.DefaultBitrate = maxAllowed;
                    needsUpdate = true;
                }
            }

            if (needsUpdate)
            {
                await dbContext.UpdateAsync(config);
            }
        }

        return config;
    }

    /// <summary>
    ///     Updates the custom voice configuration for a guild.
    /// </summary>
    /// <param name="config">The updated configuration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UpdateConfigAsync(CustomVoiceConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        if (config.GuildId == 0)
            throw new ArgumentException("Guild ID cannot be 0", nameof(config));

        await using var dbContext = await dbFactory.CreateConnectionAsync();
        await dbContext.UpdateAsync(config);
    }

    /// <summary>
    ///     Sets up a voice channel as a hub for creating custom voice channels.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="hubChannelId">The hub channel ID.</param>
    /// <param name="categoryId">The optional category ID for created channels.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetupHubAsync(ulong guildId, ulong hubChannelId, ulong? categoryId = null)
    {
        if (guildId == 0)
            throw new ArgumentException("Guild ID cannot be 0", nameof(guildId));

        if (hubChannelId == 0)
            throw new ArgumentException("Hub channel ID cannot be 0", nameof(hubChannelId));

        // Validate that the channel exists
        var guild = client.GetGuild(guildId);
        if (guild == null)
            throw new ArgumentException($"Guild with ID {guildId} not found", nameof(guildId));

        var hubChannel = guild.GetVoiceChannel(hubChannelId);
        if (hubChannel == null)
            throw new ArgumentException($"Voice channel with ID {hubChannelId} not found", nameof(hubChannelId));

        // Validate category if provided
        if (categoryId.HasValue)
        {
            var category = guild.GetCategoryChannel(categoryId.Value);
            if (category == null)
                throw new ArgumentException($"Category with ID {categoryId} not found", nameof(categoryId));
        }

        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var config = await dbContext.CustomVoiceConfigs.FirstOrDefaultAsync(c => c.GuildId == guildId);

        if (config == null)
        {
            config = new CustomVoiceConfig
            {
                GuildId = guildId,
                HubVoiceChannelId = hubChannelId,
                ChannelCategoryId = categoryId,
                DefaultNameFormat = "{0}'s Channel",
                DefaultUserLimit = 0,
                DefaultBitrate = 64,
                DeleteWhenEmpty = true,
                EmptyChannelTimeout = 1,
                AllowMultipleChannels = false,
                AllowNameCustomization = true,
                AllowUserLimitCustomization = true,
                AllowBitrateCustomization = true,
                AllowLocking = true,
                AllowUserManagement = true,
                MaxUserLimit = 99,
                MaxBitrate = 96,
                PersistUserPreferences = true,
                AutoPermission = true
            };

            await dbContext.InsertAsync(config);
        }
        else
        {
            config.HubVoiceChannelId = hubChannelId;
            if (categoryId.HasValue)
                config.ChannelCategoryId = categoryId;

            await dbContext.UpdateAsync(config);
        }
    }

    /// <summary>
    ///     Gets the active custom voice channels for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>A list of active custom voice channels.</returns>
    public async Task<List<CustomVoiceChannel>> GetActiveChannelsAsync(ulong guildId)
    {
        if (guildId == 0)
            throw new ArgumentException("Guild ID cannot be 0", nameof(guildId));

        await using var dbContext = await dbFactory.CreateConnectionAsync();
        return await dbContext.CustomVoiceChannels.Where(c => c.GuildId == guildId).ToListAsync();
    }

    /// <summary>
    ///     Gets a custom voice channel by its ID.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>The custom voice channel if found, null otherwise.</returns>
    public async Task<CustomVoiceChannel?> GetChannelAsync(ulong guildId, ulong channelId)
    {
        if (guildId == 0)
            throw new ArgumentException("Guild ID cannot be 0", nameof(guildId));

        if (channelId == 0)
            throw new ArgumentException("Channel ID cannot be 0", nameof(channelId));

        await using var dbContext = await dbFactory.CreateConnectionAsync();
        return await dbContext.CustomVoiceChannels.FirstOrDefaultAsync(c =>
            c.GuildId == guildId && c.ChannelId == channelId);
    }

    /// <summary>
    ///     Gets all custom voice channels owned by a user.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>A list of custom voice channels owned by the user.</returns>
    public async Task<List<CustomVoiceChannel>> GetUserChannelsAsync(ulong guildId, ulong userId)
    {
        if (guildId == 0)
            throw new ArgumentException("Guild ID cannot be 0", nameof(guildId));

        if (userId == 0)
            throw new ArgumentException("User ID cannot be 0", nameof(userId));

        await using var dbContext = await dbFactory.CreateConnectionAsync();
        return await dbContext.CustomVoiceChannels.Where(c => c.GuildId == guildId && c.OwnerId == userId)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets a user's voice preferences.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The user's voice preferences if found, null otherwise.</returns>
    public async Task<UserVoicePreference?> GetUserPreferencesAsync(ulong guildId, ulong userId)
    {
        if (guildId == 0)
            throw new ArgumentException("Guild ID cannot be 0", nameof(guildId));

        if (userId == 0)
            throw new ArgumentException("User ID cannot be 0", nameof(userId));

        await using var dbContext = await dbFactory.CreateConnectionAsync();
        return await dbContext.UserVoicePreferences.FirstOrDefaultAsync(p =>
            p.GuildId == guildId && p.UserId == userId);
    }

    /// <summary>
    ///     Sets a user's voice preferences.
    /// </summary>
    /// <param name="prefs">The user's voice preferences.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetUserPreferencesAsync(UserVoicePreference prefs)
    {
        if (prefs == null)
            throw new ArgumentNullException(nameof(prefs));

        if (prefs.GuildId == 0)
            throw new ArgumentException("Guild ID cannot be 0", nameof(prefs));

        if (prefs.UserId == 0)
            throw new ArgumentException("User ID cannot be 0", nameof(prefs));

        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var existingPrefs = await dbContext.UserVoicePreferences.FirstOrDefaultAsync(p =>
            p.GuildId == prefs.GuildId && p.UserId == prefs.UserId);

        if (existingPrefs == null)
        {
            await dbContext.InsertAsync(prefs);
        }
        else
        {
            existingPrefs.NameFormat = prefs.NameFormat;
            existingPrefs.UserLimit = prefs.UserLimit;
            existingPrefs.Bitrate = prefs.Bitrate;
            existingPrefs.PreferLocked = prefs.PreferLocked;
            existingPrefs.KeepAlive = prefs.KeepAlive;
            existingPrefs.WhitelistJson = prefs.WhitelistJson;
            existingPrefs.BlacklistJson = prefs.BlacklistJson;
            await dbContext.UpdateAsync(existingPrefs);
        }
    }

    /// <summary>
    ///     Creates a custom voice channel for a user.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="user">The user.</param>
    /// <returns>The created voice channel if successful, null otherwise.</returns>
    public async Task<IVoiceChannel> CreateVoiceChannelAsync(IGuild guild, IGuildUser user)
    {
        if (guild == null)
            throw new ArgumentNullException(nameof(guild));

        if (user == null)
            throw new ArgumentNullException(nameof(user));

        var config = await GetOrCreateConfigAsync(guild.Id);

        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Check if the user already has a channel and multiple channels are not allowed
        if (!config.AllowMultipleChannels)
        {
            var existingChannel = await dbContext.CustomVoiceChannels
                .FirstOrDefaultAsync(c => c.GuildId == guild.Id && c.OwnerId == user.Id);

            if (existingChannel != null)
            {
                // Delete the old channel and create a new one
                await DeleteVoiceChannelAsync(guild.Id, existingChannel.ChannelId);
            }
        }

        // Get user preferences if they exist and persistence is enabled
        UserVoicePreference prefs = null;
        if (config.PersistUserPreferences)
        {
            prefs = await GetUserPreferencesAsync(guild.Id, user.Id);
        }

        // Determine channel name
        var channelName = FormatChannelName(config.DefaultNameFormat, user, guild);
        if (prefs?.NameFormat != null && config.AllowNameCustomization)
        {
            channelName = FormatChannelName(prefs.NameFormat, user, guild);
        }

        // Sanitize channel name
        channelName = SanitizeChannelName(channelName);

        // Determine channel category
        ICategoryChannel category = null;
        if (config.ChannelCategoryId.HasValue)
        {
            category = await guild.GetCategoryChannelAsync(config.ChannelCategoryId.Value);
        }

        // Determine user limit
        var userLimit = config.DefaultUserLimit;
        if (prefs?.UserLimit.HasValue == true && config.AllowUserLimitCustomization)
        {
            userLimit = Math.Min(prefs.UserLimit.Value, config.MaxUserLimit);
        }

        // Determine bitrate and validate against Discord limits
        var bitrate = config.DefaultBitrate;
        if (prefs?.Bitrate.HasValue == true && config.AllowBitrateCustomization)
        {
            bitrate = Math.Min(prefs.Bitrate.Value, config.MaxBitrate);
        }

        // Validate bitrate against Discord's actual limits (cap to safe defaults if misconfigured)
        var maxAllowedBitrate = (guild as SocketGuild)?.PremiumTier switch
        {
            PremiumTier.Tier3 => 384,
            PremiumTier.Tier2 => 256,
            PremiumTier.Tier1 => 128,
            _ => 96
        };

        if (bitrate > maxAllowedBitrate)
        {
            logger.LogWarning(
                "Configured bitrate {ConfiguredBitrate} exceeds Discord limit {MaxAllowed} for guild {GuildId} (Tier: {PremiumTier}). Capping to {MaxAllowed}.",
                bitrate, maxAllowedBitrate, guild.Id, (guild as SocketGuild)?.PremiumTier, maxAllowedBitrate);
            bitrate = maxAllowedBitrate;
        }

        // Create the channel
        IVoiceChannel voiceChannel;
        if (category != null)
        {
            voiceChannel = await guild.CreateVoiceChannelAsync(channelName, props =>
            {
                props.CategoryId = category.Id;
                props.UserLimit = userLimit > 0 ? userLimit : null;
                props.Bitrate = bitrate * 1000;
            });
        }
        else
        {
            voiceChannel = await guild.CreateVoiceChannelAsync(channelName, props =>
            {
                props.UserLimit = userLimit > 0 ? userLimit : null;
                props.Bitrate = bitrate * 1000;
            });
        }

        // Create the channel entry in the database
        var customChannel = new CustomVoiceChannel
        {
            GuildId = guild.Id,
            ChannelId = voiceChannel.Id,
            OwnerId = user.Id,
            CreatedAt = DateTime.UtcNow,
            LastActive = DateTime.UtcNow,
            IsLocked = prefs?.PreferLocked == true && config.AllowLocking,
            KeepAlive = prefs?.KeepAlive == true
        };
        var allowedUsers = new List<ulong>();
        var deniedUsers = new List<ulong>();

        // Add allowed/denied users if they exist in preferences
        if (prefs?.WhitelistJson != null)
        {
            try
            {
                allowedUsers = JsonSerializer.Deserialize<List<ulong>>(prefs.WhitelistJson) ?? [];
                customChannel.AllowedUsersJson = prefs.WhitelistJson;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deserializing whitelist JSON for user {UserId}", user.Id);
            }
        }

        if (prefs?.BlacklistJson != null)
        {
            try
            {
                deniedUsers = JsonSerializer.Deserialize<List<ulong>>(prefs.BlacklistJson) ?? [];
                customChannel.DeniedUsersJson = prefs.BlacklistJson;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deserializing blacklist JSON for user {UserId}", user.Id);
            }
        }

        await dbContext.InsertAsync(customChannel);

        // Keep track of the channel immediately after database insert
        activeChannels.AddOrUpdate(guild.Id,
            [voiceChannel.Id],
            (_, set) =>
            {
                set.Add(voiceChannel.Id);
                return set;
            });

        // Set permissions for the channel
        if (config.AutoPermission)
        {
            try
            {
                // Give the creator management permissions
                await voiceChannel.AddPermissionOverwriteAsync(user, new OverwritePermissions(
                    connect: PermValue.Allow,
                    speak: PermValue.Allow,
                    useVoiceActivation: PermValue.Allow,
                    manageChannel: PermValue.Allow,
                    moveMembers: PermValue.Allow,
                    muteMembers: PermValue.Allow,
                    deafenMembers: PermValue.Allow
                ));

                // If the channel should be locked, deny everyone else connect permission
                if (customChannel.IsLocked)
                {
                    var everyoneRole = guild.EveryoneRole;
                    await voiceChannel.AddPermissionOverwriteAsync(everyoneRole, new OverwritePermissions(
                        connect: PermValue.Deny
                    ));

                    // Add allowed users if any
                    foreach (var allowedUserId in allowedUsers)
                    {
                        try
                        {
                            var allowedUser = await guild.GetUserAsync(allowedUserId);
                            if (allowedUser != null)
                            {
                                await voiceChannel.AddPermissionOverwriteAsync(allowedUser, new OverwritePermissions(
                                    connect: PermValue.Allow
                                ));
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex,
                                "Error setting permissions for allowed user {UserId} in custom voice channel {ChannelId}",
                                allowedUserId, voiceChannel.Id);
                        }
                    }

                    // Deny specific users if any
                    foreach (var deniedUserId in deniedUsers)
                    {
                        try
                        {
                            var deniedUser = await guild.GetUserAsync(deniedUserId);
                            if (deniedUser != null)
                            {
                                await voiceChannel.AddPermissionOverwriteAsync(deniedUser, new OverwritePermissions(
                                    connect: PermValue.Deny
                                ));
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex,
                                "Error setting permissions for denied user {UserId} in custom voice channel {ChannelId}",
                                deniedUserId, voiceChannel.Id);
                        }
                    }
                }

                // If a custom voice admin role is set, give it full permissions
                if (config.CustomVoiceAdminRoleId.HasValue)
                {
                    var adminRole = guild.GetRole(config.CustomVoiceAdminRoleId.Value);
                    if (adminRole != null)
                    {
                        await voiceChannel.AddPermissionOverwriteAsync(adminRole, new OverwritePermissions(
                            connect: PermValue.Allow,
                            speak: PermValue.Allow,
                            useVoiceActivation: PermValue.Allow,
                            manageChannel: PermValue.Allow,
                            moveMembers: PermValue.Allow,
                            muteMembers: PermValue.Allow,
                            deafenMembers: PermValue.Allow
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error setting permissions for custom voice channel {ChannelId}", voiceChannel.Id);
            }
        }

        // Also set permissions for the associated text channel
        try
        {
            ITextChannel textChannel = voiceChannel;
            if (textChannel != null && config.AutoPermission)
            {
                // Give the creator management permissions for the text channel
                await textChannel.AddPermissionOverwriteAsync(user, new OverwritePermissions(
                    viewChannel: PermValue.Allow,
                    sendMessages: PermValue.Allow,
                    readMessageHistory: PermValue.Allow,
                    manageChannel: PermValue.Allow,
                    manageMessages: PermValue.Allow
                ));

                // If the channel should be locked, restrict who can see the text channel
                if (customChannel.IsLocked)
                {
                    var everyoneRole = guild.EveryoneRole;
                    await textChannel.AddPermissionOverwriteAsync(everyoneRole, new OverwritePermissions(
                        viewChannel: PermValue.Deny
                    ));

                    // Add allowed users if any
                    foreach (var allowedUserId in allowedUsers)
                    {
                        try
                        {
                            var allowedUser = await guild.GetUserAsync(allowedUserId);
                            if (allowedUser != null)
                            {
                                await textChannel.AddPermissionOverwriteAsync(allowedUser, new OverwritePermissions(
                                    viewChannel: PermValue.Allow,
                                    sendMessages: PermValue.Allow,
                                    readMessageHistory: PermValue.Allow
                                ));
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex,
                                "Error setting text channel permissions for allowed user {UserId} in custom voice channel {ChannelId}",
                                allowedUserId, voiceChannel.Id);
                        }
                    }
                }

                // If a custom voice admin role is set, give it full permissions
                if (config.CustomVoiceAdminRoleId.HasValue)
                {
                    var adminRole = guild.GetRole(config.CustomVoiceAdminRoleId.Value);
                    if (adminRole != null)
                    {
                        await textChannel.AddPermissionOverwriteAsync(adminRole, new OverwritePermissions(
                            viewChannel: PermValue.Allow,
                            sendMessages: PermValue.Allow,
                            readMessageHistory: PermValue.Allow,
                            manageChannel: PermValue.Allow,
                            manageMessages: PermValue.Allow
                        ));
                    }
                }

                // Send a welcome message with control buttons to the text channel
                var embed = new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Voice Channel Controls")
                    .WithDescription(strings.CustomVoiceDescription(voiceChannel.Guild.Id) ??
                                     "This is your custom voice channel. Use the buttons below to manage it.")
                    .Build();

                var components = BuildVoiceControlButtons(voiceChannel.Id, customChannel, config);
                await textChannel.SendMessageAsync(embed: embed, components: components.Build());
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting up text channel for voice channel {ChannelId}", voiceChannel.Id);
        }

        // Move the user to the new channel
        try
        {
            await user.ModifyAsync(props => props.Channel = new Optional<IVoiceChannel>(voiceChannel));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error moving user {UserId} to custom voice channel {ChannelId}", user.Id,
                voiceChannel.Id);
        }

        return voiceChannel;
    }

    /// <summary>
    ///     Deletes a custom voice channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>True if deleted successfully, false otherwise.</returns>
    public async Task<bool> DeleteVoiceChannelAsync(ulong guildId, ulong channelId)
    {
        if (guildId == 0)
            throw new ArgumentException("Guild ID cannot be 0", nameof(guildId));

        if (channelId == 0)
            throw new ArgumentException("Channel ID cannot be 0", nameof(channelId));

        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null)
                return false;

            var channel = guild.GetVoiceChannel(channelId);
            if (channel == null)
                return false;

            await using var dbContext = await dbFactory.CreateConnectionAsync();

            // Delete from database
            var cvc = await dbContext.CustomVoiceChannels
                .FirstOrDefaultAsync(c => c.ChannelId == channelId);

            if (cvc != null)
                await dbContext.DeleteAsync(cvc);

            await channel.DeleteAsync();

            // Remove from tracking collections
            if (activeChannels.TryGetValue(guildId, out var channels))
            {
                channels.Remove(channelId);
            }

            emptyChannels.TryRemove(channelId, out _);
            if (emptyChannelTimers.TryRemove(channelId, out var timer))
            {
                await timer.DisposeAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting custom voice channel {ChannelId}", channelId);
            return false;
        }
    }

    /// <summary>
    ///     Updates a custom voice channel's settings.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="name">The new name (optional).</param>
    /// <param name="userLimit">The new user limit (optional).</param>
    /// <param name="bitrate">The new bitrate (optional).</param>
    /// <param name="isLocked">The new locked status (optional).</param>
    /// <returns>True if updated successfully, false otherwise.</returns>
    public async Task<bool> UpdateVoiceChannelAsync(ulong guildId, ulong channelId, string name = null,
        int? userLimit = null,
        int? bitrate = null, bool? isLocked = null)
    {
        if (guildId == 0)
            throw new ArgumentException("Guild ID cannot be 0", nameof(guildId));

        if (channelId == 0)
            throw new ArgumentException("Channel ID cannot be 0", nameof(channelId));

        try
        {
            if (client.GetGuild(guildId) is not IGuild guild)
                return false;

            var channel = await guild.GetVoiceChannelAsync(channelId);
            if (channel == null)
                return false;

            // Get the config for permission checks
            var config = await GetOrCreateConfigAsync(guildId);

            // Get the channel from database
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var customChannel = await dbContext.CustomVoiceChannels.FirstOrDefaultAsync(c => c.ChannelId == channelId);
            if (customChannel == null)
                return false;

            var allowedUsers = new List<ulong>();
            var deniedUsers = new List<ulong>();

            // Update channel settings
            if (!string.IsNullOrEmpty(name) && config.AllowNameCustomization)
            {
                // Sanitize the name
                name = SanitizeChannelName(name);
                await channel.ModifyAsync(props => props.Name = name);
            }

            if (userLimit.HasValue && config.AllowUserLimitCustomization)
            {
                var limit = Math.Min(userLimit.Value, config.MaxUserLimit);
                await channel.ModifyAsync(props => props.UserLimit = limit > 0 ? limit : null);
            }

            if (bitrate.HasValue && config.AllowBitrateCustomization)
            {
                var rate = Math.Min(bitrate.Value, config.MaxBitrate);

                // Validate against Discord's actual limits
                var maxAllowedBitrate = (guild as SocketGuild)?.PremiumTier switch
                {
                    PremiumTier.Tier3 => 384,
                    PremiumTier.Tier2 => 256,
                    PremiumTier.Tier1 => 128,
                    _ => 96
                };

                if (rate > maxAllowedBitrate)
                {
                    logger.LogWarning(
                        "Requested bitrate {RequestedBitrate} exceeds Discord limit {MaxAllowed} for guild {GuildId} (Tier: {PremiumTier}). Capping to {MaxAllowed}.",
                        rate, maxAllowedBitrate, guildId, (guild as SocketGuild)?.PremiumTier, maxAllowedBitrate);
                    rate = maxAllowedBitrate;
                }

                await channel.ModifyAsync(props => props.Bitrate = rate * 1000);
            }

            if (isLocked.HasValue && config.AllowLocking)
            {
                customChannel.IsLocked = isLocked.Value;

                // Update permissions based on lock status
                if (isLocked.Value)
                {
                    // Lock both voice and text channel

                    // Voice channel
                    await channel.AddPermissionOverwriteAsync(guild.EveryoneRole, new OverwritePermissions(
                        connect: PermValue.Deny
                    ));

                    // Text channel
                    try
                    {
                        ITextChannel textChannel = channel;
                        if (textChannel != null)
                        {
                            await textChannel.AddPermissionOverwriteAsync(guild.EveryoneRole, new OverwritePermissions(
                                viewChannel: PermValue.Deny
                            ));
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error updating text channel permissions for channel {ChannelId}",
                            channelId);
                    }

                    if (string.IsNullOrEmpty(customChannel.AllowedUsersJson))
                    {
                        allowedUsers = [];
                    }
                    else
                    {
                        try
                        {
                            allowedUsers = JsonSerializer.Deserialize<List<ulong>>(customChannel.AllowedUsersJson) ??
                                           [];
                        }
                        catch (Exception)
                        {
                            allowedUsers = [];
                        }
                    }

                    foreach (var allowedUserId in allowedUsers)
                    {
                        var user = await guild.GetUserAsync(allowedUserId);
                        if (user != null)
                        {
                            // Voice channel
                            await channel.AddPermissionOverwriteAsync(user, new OverwritePermissions(
                                connect: PermValue.Allow
                            ));

                            // Text channel
                            try
                            {
                                ITextChannel textChannel = channel;
                                if (textChannel != null)
                                {
                                    await textChannel.AddPermissionOverwriteAsync(user, new OverwritePermissions(
                                        viewChannel: PermValue.Allow,
                                        sendMessages: PermValue.Allow,
                                        readMessageHistory: PermValue.Allow
                                    ));
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex,
                                    "Error updating text channel permissions for user {UserId} in channel {ChannelId}",
                                    allowedUserId, channelId);
                            }
                        }
                    }
                }
                else
                {
                    // Unlock both voice and text channel

                    // Voice channel
                    var overwrites = channel.PermissionOverwrites;
                    var everyoneOverwrite = overwrites.FirstOrDefault(o => o.TargetId == guild.EveryoneRole.Id);
                    if (everyoneOverwrite.ToString() is not null)
                    {
                        var permissions = everyoneOverwrite.Permissions;
                        if (permissions.Connect == PermValue.Deny)
                        {
                            await channel.AddPermissionOverwriteAsync(guild.EveryoneRole, new OverwritePermissions(
                                connect: PermValue.Inherit
                            ));
                        }
                    }

                    // Text channel
                    try
                    {
                        ITextChannel textChannel = channel;
                        if (textChannel != null)
                        {
                            var textOverwrites = textChannel.PermissionOverwrites;
                            var textEveryoneOverwrite =
                                textOverwrites.FirstOrDefault(o => o.TargetId == guild.EveryoneRole.Id);
                            if (textEveryoneOverwrite.ToString() != null)
                            {
                                var permissions = textEveryoneOverwrite.Permissions;
                                if (permissions.ViewChannel == PermValue.Deny)
                                {
                                    await textChannel.AddPermissionOverwriteAsync(guild.EveryoneRole,
                                        new OverwritePermissions(
                                            viewChannel: PermValue.Inherit
                                        ));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error updating text channel permissions for channel {ChannelId}",
                            channelId);
                    }
                }

                // Update the database
                await dbContext.UpdateAsync(customChannel);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating custom voice channel {ChannelId}", channelId);
            return false;
        }
    }

    /// <summary>
    ///     Adds a user to the allowed list for a locked channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="userId">The user ID to allow.</param>
    /// <returns>True if added successfully, false otherwise.</returns>
    public async Task<bool> AllowUserAsync(ulong guildId, ulong channelId, ulong userId)
    {
        if (guildId == 0)
            throw new ArgumentException("Guild ID cannot be 0", nameof(guildId));

        if (channelId == 0)
            throw new ArgumentException("Channel ID cannot be 0", nameof(channelId));

        if (userId == 0)
            throw new ArgumentException("User ID cannot be 0", nameof(userId));

        if (client.GetGuild(guildId) is not IGuild guild)
            return false;

        var channel = await guild.GetVoiceChannelAsync(channelId);
        if (channel == null)
            return false;

        // Get the config for permission checks
        var config = await GetOrCreateConfigAsync(guildId);
        if (!config.AllowUserManagement)
            return false;

        // Get the channel from database
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var customChannel = await dbContext.CustomVoiceChannels.FirstOrDefaultAsync(c => c.ChannelId == channelId);
        if (customChannel == null)
            return false;

        // Parse allowed users
        var allowedUsers = new List<ulong>();
        if (!string.IsNullOrEmpty(customChannel.AllowedUsersJson))
        {
            try
            {
                allowedUsers = JsonSerializer.Deserialize<List<ulong>>(customChannel.AllowedUsersJson) ?? [];
            }
            catch (Exception)
            {
                allowedUsers = [];
            }
        }

        // Add the user if not already in the list
        if (!allowedUsers.Contains(userId))
        {
            allowedUsers.Add(userId);
            customChannel.AllowedUsersJson = JsonSerializer.Serialize(allowedUsers);
            await dbContext.UpdateAsync(customChannel);
        }

        // Update channel permissions
        if (customChannel.IsLocked)
        {
            var user = await guild.GetUserAsync(userId);
            if (user != null)
            {
                // Voice channel
                await channel.AddPermissionOverwriteAsync(user, new OverwritePermissions(
                    connect: PermValue.Allow
                ));

                // Text channel
                try
                {
                    ITextChannel textChannel = channel;
                    if (textChannel != null)
                    {
                        await textChannel.AddPermissionOverwriteAsync(user, new OverwritePermissions(
                            viewChannel: PermValue.Allow,
                            sendMessages: PermValue.Allow,
                            readMessageHistory: PermValue.Allow
                        ));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Error updating text channel permissions for user {UserId} in channel {ChannelId}",
                        userId, channelId);
                }
            }
        }

        // Remove from denied users if present
        var deniedUsers = new List<ulong>();
        if (!string.IsNullOrEmpty(customChannel.DeniedUsersJson))
        {
            try
            {
                deniedUsers = JsonSerializer.Deserialize<List<ulong>>(customChannel.DeniedUsersJson) ?? [];
            }
            catch (Exception)
            {
                deniedUsers = [];
            }
        }

        if (deniedUsers.Contains(userId))
        {
            deniedUsers.Remove(userId);
            customChannel.DeniedUsersJson = JsonSerializer.Serialize(deniedUsers);
            await dbContext.UpdateAsync(customChannel);
        }

        return true;
    }

    /// <summary>
    ///     Removes a user from the allowed list for a locked channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="userId">The user ID to disallow.</param>
    /// <returns>True if removed successfully, false otherwise.</returns>
    public async Task<bool> DenyUserAsync(ulong guildId, ulong channelId, ulong userId)
    {
        if (guildId == 0)
            throw new ArgumentException("Guild ID cannot be 0", nameof(guildId));

        if (channelId == 0)
            throw new ArgumentException("Channel ID cannot be 0", nameof(channelId));

        if (userId == 0)
            throw new ArgumentException("User ID cannot be 0", nameof(userId));

        if (client.GetGuild(guildId) is not IGuild guild)
            return false;

        var channel = await guild.GetVoiceChannelAsync(channelId);
        if (channel == null)
            return false;

        // Get the config for permission checks
        var config = await GetOrCreateConfigAsync(guildId);
        if (!config.AllowUserManagement)
            return false;

        // Get the channel from database
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var customChannel = await dbContext.CustomVoiceChannels.FirstOrDefaultAsync(c => c.ChannelId == channelId);
        if (customChannel == null)
            return false;

        // Don't allow denying the channel owner
        if (customChannel.OwnerId == userId)
            return false;

        // Parse denied users
        var deniedUsers = new List<ulong>();
        if (!string.IsNullOrEmpty(customChannel.DeniedUsersJson))
        {
            try
            {
                deniedUsers = JsonSerializer.Deserialize<List<ulong>>(customChannel.DeniedUsersJson) ?? [];
            }
            catch (Exception)
            {
                deniedUsers = [];
            }
        }

        // Add the user if not already in the list
        if (!deniedUsers.Contains(userId))
        {
            deniedUsers.Add(userId);
            customChannel.DeniedUsersJson = JsonSerializer.Serialize(deniedUsers);
            await dbContext.UpdateAsync(customChannel);
        }

        // Update channel permissions
        var user = await guild.GetUserAsync(userId);
        if (user != null)
        {
            // Voice channel
            await channel.AddPermissionOverwriteAsync(user, new OverwritePermissions(
                connect: PermValue.Deny
            ));

            // Text channel
            try
            {
                ITextChannel textChannel = channel;
                if (textChannel != null)
                {
                    await textChannel.AddPermissionOverwriteAsync(user, new OverwritePermissions(
                        viewChannel: PermValue.Deny
                    ));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating text channel permissions for user {UserId} in channel {ChannelId}",
                    userId, channelId);
            }

            // If the user is in the channel, kick them
            if (user.VoiceChannel?.Id == channelId)
            {
                try
                {
                    await user.ModifyAsync(props => props.Channel = null);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error disconnecting denied user {UserId} from voice channel {ChannelId}",
                        userId,
                        channelId);
                }
            }
        }

        // Remove from allowed users if present
        var allowedUsers = new List<ulong>();
        if (!string.IsNullOrEmpty(customChannel.AllowedUsersJson))
        {
            try
            {
                allowedUsers = JsonSerializer.Deserialize<List<ulong>>(customChannel.AllowedUsersJson) ?? [];
            }
            catch (Exception)
            {
                allowedUsers = [];
            }
        }

        if (allowedUsers.Contains(userId))
        {
            allowedUsers.Remove(userId);
            customChannel.AllowedUsersJson = JsonSerializer.Serialize(allowedUsers);
            await dbContext.UpdateAsync(customChannel);
        }

        return true;
    }

    /// <summary>
    ///     Transfers ownership of a custom voice channel to another user.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="newOwnerId">The new owner's user ID.</param>
    /// <returns>True if ownership was transferred successfully, false otherwise.</returns>
    public async Task<bool> TransferOwnershipAsync(ulong guildId, ulong channelId, ulong newOwnerId)
    {
        if (guildId == 0)
            throw new ArgumentException("Guild ID cannot be 0", nameof(guildId));

        if (channelId == 0)
            throw new ArgumentException("Channel ID cannot be 0", nameof(channelId));

        if (newOwnerId == 0)
            throw new ArgumentException("New owner ID cannot be 0", nameof(newOwnerId));

        if (client.GetGuild(guildId) is not IGuild guild)
            return false;

        var channel = await guild.GetVoiceChannelAsync(channelId);
        if (channel == null)
            return false;

        var newOwner = await guild.GetUserAsync(newOwnerId);
        if (newOwner == null)
            return false;

        // Get the channel from database
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        var customChannel = await dbContext.CustomVoiceChannels.FirstOrDefaultAsync(c => c.ChannelId == channelId);
        if (customChannel == null)
            return false;

        // Update ownership
        var oldOwnerId = customChannel.OwnerId;
        customChannel.OwnerId = newOwnerId;

        // Update permissions
        try
        {
            // Remove admin permissions from old owner
            var oldOwner = await guild.GetUserAsync(oldOwnerId);
            if (oldOwner != null)
            {
                // Voice channel
                await channel.AddPermissionOverwriteAsync(oldOwner, new OverwritePermissions(
                    connect: PermValue.Allow,
                    speak: PermValue.Allow,
                    useVoiceActivation: PermValue.Allow
                ));

                // Text channel
                try
                {
                    ITextChannel textChannel = channel;
                    if (textChannel != null)
                    {
                        await textChannel.AddPermissionOverwriteAsync(oldOwner, new OverwritePermissions(
                            viewChannel: PermValue.Allow,
                            sendMessages: PermValue.Allow,
                            readMessageHistory: PermValue.Allow
                        ));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Error updating text channel permissions for old owner {UserId} in channel {ChannelId}",
                        oldOwnerId, channelId);
                }
            }

            // Add admin permissions to new owner
            // Voice channel
            await channel.AddPermissionOverwriteAsync(newOwner, new OverwritePermissions(
                connect: PermValue.Allow,
                speak: PermValue.Allow,
                useVoiceActivation: PermValue.Allow,
                manageChannel: PermValue.Allow,
                moveMembers: PermValue.Allow,
                muteMembers: PermValue.Allow,
                deafenMembers: PermValue.Allow
            ));

            // Text channel
            try
            {
                ITextChannel textChannel = channel;
                if (textChannel != null)
                {
                    await textChannel.AddPermissionOverwriteAsync(newOwner, new OverwritePermissions(
                        viewChannel: PermValue.Allow,
                        sendMessages: PermValue.Allow,
                        readMessageHistory: PermValue.Allow,
                        manageChannel: PermValue.Allow,
                        manageMessages: PermValue.Allow
                    ));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error updating text channel permissions for new owner {UserId} in channel {ChannelId}",
                    newOwnerId, channelId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating permissions during ownership transfer for channel {ChannelId}",
                channelId);
        }

        // Save changes
        await dbContext.UpdateAsync(customChannel);

        return true;
    }

    /// <summary>
    ///     Formats a channel name using placeholders.
    /// </summary>
    private string FormatChannelName(string format, IGuildUser user, IGuild guild)
    {
        return format
            .Replace("{0}", user.Username)
            .Replace("{1}", user.DiscriminatorValue.ToString("D4"))
            .Replace("{2}", guild.Name);
    }

    /// <summary>
    ///     Sanitizes a channel name to comply with Discord's restrictions.
    /// </summary>
    private string SanitizeChannelName(string name)
    {
        // Remove illegal characters
        name = string.Join("",
            name.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '-' || c == '_'));

        // Ensure it's not empty
        if (string.IsNullOrWhiteSpace(name))
            name = "voice-channel";

        // Trim to Discord's maximum length
        if (name.Length > 100)
            name = name[..100];

        // Remove starting/ending spaces
        return name.Trim();
    }

    /// <summary>
    ///     Builds the interactive control buttons for a voice channel.
    /// </summary>
    private ComponentBuilder BuildVoiceControlButtons(ulong channelId, CustomVoiceChannel customChannel,
        CustomVoiceConfig config)
    {
        var components = new ComponentBuilder();

        // Row 1: Basic controls
        components.WithButton(
            customId: $"voice:rename:{channelId}",
            label: "Rename",
            style: ButtonStyle.Primary,
            disabled: !config.AllowNameCustomization,
            emote: new Emoji("✏️"),
            row: 0
        );

        components.WithButton(
            customId: $"voice:limit:{channelId}",
            label: "User Limit",
            style: ButtonStyle.Primary,
            disabled: !config.AllowUserLimitCustomization,
            emote: new Emoji("👥"),
            row: 0
        );

        components.WithButton(
            customId: $"voice:bitrate:{channelId}",
            label: "Bitrate",
            style: ButtonStyle.Primary,
            disabled: !config.AllowBitrateCustomization,
            emote: new Emoji("🔊"),
            row: 0
        );

        // Row 2: Lock/Keep Alive/Transfer/Delete toggles
        components.WithButton(
            customId: $"voice:{(customChannel.IsLocked ? "unlock" : "lock")}:{channelId}",
            label: customChannel.IsLocked ? "Unlock" : "Lock",
            style: customChannel.IsLocked ? ButtonStyle.Success : ButtonStyle.Danger,
            disabled: !config.AllowLocking,
            emote: new Emoji(customChannel.IsLocked ? "🔓" : "🔒"),
            row: 1
        );

        components.WithButton(
            customId: $"voice:keepalive:{channelId}:{!customChannel.KeepAlive}",
            label: customChannel.KeepAlive ? "Disable Keep Alive" : "Enable Keep Alive",
            style: customChannel.KeepAlive ? ButtonStyle.Danger : ButtonStyle.Success,
            emote: new Emoji("⏱️"),
            row: 1
        );

        components.WithButton(
            customId: $"voice:transfer:{channelId}",
            label: "Transfer",
            style: ButtonStyle.Secondary,
            emote: new Emoji("👑"),
            row: 1
        );

        components.WithButton(
            customId: $"voice:delete:{channelId}",
            label: "Delete",
            style: ButtonStyle.Danger,
            emote: new Emoji("🗑️"),
            row: 1
        );

        // Row 3: User Management button
        if (config.AllowUserManagement)
        {
            components.WithButton(
                customId: $"voice:manage:{channelId}",
                label: "Manage Users",
                style: ButtonStyle.Secondary,
                emote: new Emoji("⚙️"),
                row: 2
            );
        }

        return components;
    }

    /// <summary>
    ///     Handles voice state updates to create, manage, or clean up custom voice channels.
    /// </summary>
    private async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
    {
        if (user is not SocketGuildUser guildUser)
            return;

        var guild = guildUser.Guild;
        if (guild == null)
            return;

        try
        {
            // Get guild configuration
            var config = await GetOrCreateConfigAsync(guild.Id);

            // Check if the user joined the hub channel
            if (newState.VoiceChannel?.Id == config.HubVoiceChannelId)
            {
                // Create a custom voice channel for the user
                await CreateVoiceChannelAsync(guild, guildUser);
                return;
            }

            // Check if the user joined a custom voice channel
            if (newState.VoiceChannel != null && activeChannels.TryGetValue(guild.Id, out var channels) &&
                channels.Contains(newState.VoiceChannel.Id))
            {
                // Update last active time
                await using var dbContext = await dbFactory.CreateConnectionAsync();
                var customChannel =
                    await dbContext.CustomVoiceChannels.FirstOrDefaultAsync(c =>
                        c.ChannelId == newState.VoiceChannel.Id);
                if (customChannel != null)
                {
                    customChannel.LastActive = DateTime.UtcNow;
                    await dbContext.UpdateAsync(customChannel);
                }

                // Remove from empty channels if it was there
                emptyChannels.TryRemove(newState.VoiceChannel.Id, out _);
                if (emptyChannelTimers.TryRemove(newState.VoiceChannel.Id, out var timer))
                {
                    await timer.DisposeAsync();
                }
            }

            activeChannels.TryGetValue(guild.Id, out var oldChannels);
            // Check if the user left a custom voice channel
            if (oldState.VoiceChannel != null &&
                oldChannels != null && oldChannels.Contains(oldState.VoiceChannel.Id))
            {
                var vc = guild.GetVoiceChannel(oldState.VoiceChannel.Id);

                // Add null check and use consistent property
                if (vc != null)
                {
                    // Add a small delay to ensure user count is updated
                    await Task.Delay(100);

                    // Refresh the channel reference to get updated user count
                    vc = guild.GetVoiceChannel(oldState.VoiceChannel.Id);

                    // Check if the channel is now empty (using Users.Count for consistency)
                    if (vc != null && vc.Users.Count == 0)
                    {
                        // Get the channel to check if it should be kept
                        await using var dbContext = await dbFactory.CreateConnectionAsync();
                        var customChannel =
                            await dbContext.CustomVoiceChannels.FirstOrDefaultAsync(c =>
                                c.ChannelId == oldState.VoiceChannel.Id);

                        if (customChannel is { KeepAlive: false } && config.DeleteWhenEmpty)
                        {
                            // Mark the channel as empty
                            emptyChannels.TryAdd(oldState.VoiceChannel.Id, DateTime.UtcNow);

                            // Schedule deletion after timeout
                            if (config.EmptyChannelTimeout > 0)
                            {
                                var timer = new Timer(_ =>
                                {
                                    Task.Run(async () =>
                                    {
                                        try
                                        {
                                            await DeleteEmptyChannelAsync(guild.Id, oldState.VoiceChannel.Id);
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.LogError(ex, "Timer callback failed for channel {ChannelId}",
                                                oldState.VoiceChannel.Id);
                                        }
                                    });
                                }, null, TimeSpan.FromMinutes(config.EmptyChannelTimeout), Timeout.InfiniteTimeSpan);

                                emptyChannelTimers.TryAdd(oldState.VoiceChannel.Id, timer);
                            }
                            else
                            {
                                // Delete immediately
                                await DeleteEmptyChannelAsync(guild.Id, oldState.VoiceChannel.Id);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling voice state update for user {UserId} in guild {GuildId}", user.Id,
                guild.Id);
        }
    }

    /// <summary>
    ///     Handles channel deletion to clean up database entries.
    /// </summary>
    private async Task OnChannelDestroyed(SocketChannel channel)
    {
        if (channel is not SocketVoiceChannel voiceChannel)
            return;

        var guild = (voiceChannel as IGuildChannel)?.Guild;
        if (guild == null)
            return;

        try
        {
            // Check if this was a custom voice channel
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var customChannel = await dbContext.CustomVoiceChannels
                .FirstOrDefaultAsync(c => c.ChannelId == voiceChannel.Id);

            if (customChannel != null)
            {
                // Delete the channel from the database
                await dbContext.DeleteAsync(customChannel);

                // Remove from tracking collections
                if (activeChannels.TryGetValue(guild.Id, out var channels))
                {
                    channels.Remove(voiceChannel.Id);
                }

                emptyChannels.TryRemove(voiceChannel.Id, out _);
                if (emptyChannelTimers.TryRemove(voiceChannel.Id, out var timer))
                {
                    await timer.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling channel deletion for channel {ChannelId} in guild {GuildId}",
                channel.Id,
                guild.Id);
        }
    }

    /// <summary>
    ///     Handles guild join to set up tracking.
    /// </summary>
    private async Task OnJoinedGuild(IGuild guild)
    {
        try
        {
            // Load active channels for this guild
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var channels = await dbContext.CustomVoiceChannels.Where(c => c.GuildId == guild.Id).ToListAsync();

            if (channels.Any())
            {
                var channelIds = channels.Select(c => c.ChannelId).ToHashSet();
                activeChannels.TryAdd(guild.Id, channelIds);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling guild join for guild {GuildId}", guild.Id);
        }
    }

    /// <summary>
    ///     Handles guild leave to clean up tracking.
    /// </summary>
    private Task OnLeftGuild(SocketGuild guild)
    {
        try
        {
            // Remove tracking for this guild
            activeChannels.TryRemove(guild.Id, out _);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling guild leave for guild {GuildId}", guild.Id);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Deletes an empty channel after the timeout period.
    /// </summary>
    private async Task DeleteEmptyChannelAsync(ulong guildId, ulong channelId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return;

        var channel = guild.GetVoiceChannel(channelId);

        // Double-check that it's still empty before deleting
        if (channel != null && channel.Users.Count == 0)
        {
            await DeleteVoiceChannelAsync(guildId, channelId);
        }
        else if (channel != null)
        {
            // Channel has users again, remove from empty tracking
            emptyChannels.TryRemove(channelId, out _);
            if (emptyChannelTimers.TryRemove(channelId, out var timer))
            {
                await timer.DisposeAsync();
            }
        }
    }

    /// <summary>
    ///     Starts a background task to clean up empty channels.
    /// </summary>
    private async Task StartEmptyChannelCleanupTask()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5));

                await @lock.WaitAsync();
                try
                {
                    // Get all potential empty channels
                    var emptyChannels = new Dictionary<ulong, DateTime>(this.emptyChannels);

                    foreach (var (channelId, emptyTime) in emptyChannels)
                    {
                        try
                        {
                            // Find the guild for this channel
                            await using var dbContext = await dbFactory.CreateConnectionAsync();
                            var customChannel =
                                await dbContext.CustomVoiceChannels.FirstOrDefaultAsync(c => c.ChannelId == channelId);
                            if (customChannel == null)
                            {
                                this.emptyChannels.TryRemove(channelId, out _);
                                continue;
                            }

                            var guild = client.GetGuild(customChannel.GuildId);
                            if (guild == null)
                            {
                                this.emptyChannels.TryRemove(channelId, out _);
                                continue;
                            }

                            var config = await GetOrCreateConfigAsync(customChannel.GuildId);

                            // Check if the timeout has passed
                            if ((DateTime.UtcNow - emptyTime).TotalMinutes >= config.EmptyChannelTimeout &&
                                !customChannel.KeepAlive &&
                                config.DeleteWhenEmpty)
                            {
                                // Delete the channel
                                await DeleteVoiceChannelAsync(customChannel.GuildId, channelId);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error cleaning up empty channel {ChannelId}", channelId);
                        }
                    }
                }
                finally
                {
                    @lock.Release();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in empty channel cleanup task");
            }
        }
    }
}