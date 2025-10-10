using System.Text.Json;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Patreon.Common;
using Mewdeko.Modules.Patreon.Extensions;
using Mewdeko.Services.Strings;
using Microsoft.Extensions.Hosting;

namespace Mewdeko.Modules.Patreon.Services;

/// <summary>
///     Service for managing Patreon integration and monthly announcements.
/// </summary>
public class PatreonService : BackgroundService, INService, IReadyExecutor
{
    private static readonly JsonSerializerOptions CachedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly PatreonApiClient apiClient;
    private readonly DiscordShardedClient client;
    private readonly IBotCredentials creds;
    private readonly IDataConnectionFactory db;
    private readonly GuildSettingsService guildSettings;
    private readonly ILogger<PatreonService> logger;
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of the PatreonService class.
    /// </summary>
    /// <param name="client">The Discord sharded client.</param>
    /// <param name="db">The database connection factory.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    /// <param name="strings">The localized bot strings.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="apiClient">The Patreon API client.</param>
    /// <param name="logger">Serilog logger.</param>
    public PatreonService(
        DiscordShardedClient client,
        IDataConnectionFactory db,
        GuildSettingsService guildSettings,
        GeneratedBotStrings strings,
        IBotCredentials creds,
        PatreonApiClient apiClient, ILogger<PatreonService> logger)
    {
        this.client = client;
        this.db = db;
        this.guildSettings = guildSettings;
        this.strings = strings;
        this.creds = creds;
        this.apiClient = apiClient;
        this.logger = logger;
    }

    /// <summary>
    ///     Initializes the service when the bot is ready
    /// </summary>
    public async Task OnReadyAsync()
    {
        logger.LogInformation("PatreonService ready - checking for overdue announcements");

        // Check for any overdue announcements on startup
        await CheckForOverdueAnnouncements();
    }

    /// <summary>
    ///     Background service execution for monthly announcements
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckMonthlyAnnouncements();

                // Check every hour
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in PatreonService background task");

                // Wait 5 minutes before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    /// <summary>
    ///     Sets the Patreon announcement channel for a guild
    /// </summary>
    public async Task<bool> SetPatreonChannel(ulong guildId, ulong channelId)
    {
        var config = await guildSettings.GetGuildConfig(guildId);

        await using var uow = await db.CreateConnectionAsync();
        config.PatreonChannelId = channelId;
        config.PatreonEnabled = channelId != 0; // Enable if channel is set, disable if set to 0

        await guildSettings.UpdateGuildConfig(guildId, config);

        logger.LogInformation("Patreon channel set to {ChannelId} for guild {GuildId}", channelId, guildId);
        return true;
    }

    /// <summary>
    ///     Sets a custom announcement message for a guild
    /// </summary>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="message">The message string.</param>
    public async Task SetPatreonMessage(ulong guildId, string? message)
    {
        var config = await guildSettings.GetGuildConfig(guildId);

        await using var uow = await db.CreateConnectionAsync();
        config.PatreonMessage = string.IsNullOrWhiteSpace(message) || message == "-" ? null : message;

        await guildSettings.UpdateGuildConfig(guildId, config);

        logger.LogInformation("Patreon message updated for guild {GuildId}", guildId);
    }

    /// <summary>
    ///     Sets the day of the month for announcements
    /// </summary>
    public async Task<bool> SetAnnouncementDay(ulong guildId, int day)
    {
        if (day is < 1 or > 28) // Max 28 to avoid issues with February
            return false;

        var config = await guildSettings.GetGuildConfig(guildId);

        await using var uow = await db.CreateConnectionAsync();
        config.PatreonAnnouncementDay = day;

        await guildSettings.UpdateGuildConfig(guildId, config);

        logger.LogInformation("Patreon announcement day set to {Day} for guild {GuildId}", day, guildId);
        return true;
    }

    /// <summary>
    ///     Toggles Patreon announcements for a guild
    /// </summary>
    public async Task<bool> TogglePatreonAnnouncements(ulong guildId)
    {
        var config = await guildSettings.GetGuildConfig(guildId);

        await using var uow = await db.CreateConnectionAsync();
        config.PatreonEnabled = !config.PatreonEnabled;

        await guildSettings.UpdateGuildConfig(guildId, config);

        logger.LogInformation("Patreon announcements {Status} for guild {GuildId}",
            config.PatreonEnabled ? "enabled" : "disabled", guildId);

        return config.PatreonEnabled;
    }

    /// <summary>
    ///     Gets the current Patreon configuration for a guild
    /// </summary>
    public async Task<(ulong channelId, string? message, int day, bool enabled, DateTime? lastAnnouncement)>
        GetPatreonConfig(ulong guildId)
    {
        var config = await guildSettings.GetGuildConfig(guildId);

        return (config.PatreonChannelId, config.PatreonMessage, config.PatreonAnnouncementDay,
            config.PatreonEnabled, config.PatreonLastAnnouncement);
    }

    /// <summary>
    ///     Gets the extended Patreon configuration for a guild including OAuth details
    /// </summary>
    public async Task<(ulong channelId, string? message, int day, bool enabled, DateTime? lastAnnouncement,
        string? accessToken, string? refreshToken, string? campaignId, DateTime? tokenExpiry)> GetPatreonOAuthConfig(
        ulong guildId)
    {
        var config = await guildSettings.GetGuildConfig(guildId);

        return (config.PatreonChannelId, config.PatreonMessage, config.PatreonAnnouncementDay,
            config.PatreonEnabled, config.PatreonLastAnnouncement, config.PatreonAccessToken,
            config.PatreonRefreshToken, config.PatreonCampaignId, config.PatreonTokenExpiry);
    }

    /// <summary>
    ///     Gets the creator's identity information from Patreon
    /// </summary>
    public async Task<User?> GetCreatorIdentity(ulong guildId)
    {
        var config = await guildSettings.GetGuildConfig(guildId);

        if (string.IsNullOrEmpty(config.PatreonAccessToken))
        {
            logger.LogWarning("No Patreon access token available for guild {GuildId}", guildId);
            return null;
        }

        var userResponse = await apiClient.GetUserIdentityAsync(config.PatreonAccessToken);
        if (userResponse?.Data != null) return userResponse.Data;
        logger.LogWarning("Failed to get user identity for guild {GuildId}", guildId);
        return null;
    }

    /// <summary>
    ///     Manually triggers a Patreon announcement for a guild
    /// </summary>
    public async Task<bool> TriggerManualAnnouncement(ulong guildId)
    {
        var config = await guildSettings.GetGuildConfig(guildId);

        if (config.PatreonChannelId == 0)
            return false;

        var guild = client.GetGuild(guildId);
        if (guild == null)
            return false;

        var channel = guild.GetTextChannel(config.PatreonChannelId);
        if (channel == null)
            return false;

        await SendPatreonAnnouncement(guild, channel, config, true);
        return true;
    }

    /// <summary>
    ///     Checks for monthly announcements that need to be sent
    /// </summary>
    private async Task CheckMonthlyAnnouncements()
    {
        var now = DateTime.UtcNow;
        var currentDay = now.Day;

        await using var uow = await db.CreateConnectionAsync();

        // Get all guilds that have Patreon enabled and should announce today
        var configs = await uow.GuildConfigs
            .Where(x => x.PatreonEnabled &&
                        x.PatreonChannelId != 0 &&
                        x.PatreonAnnouncementDay == currentDay &&
                        (x.PatreonLastAnnouncement == null ||
                         x.PatreonLastAnnouncement.Value.Month != now.Month ||
                         x.PatreonLastAnnouncement.Value.Year != now.Year))
            .ToListAsync();

        foreach (var config in configs)
        {
            try
            {
                await ProcessGuildAnnouncement(config);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing Patreon announcement for guild {GuildId}", config.GuildId);
            }
        }
    }

    /// <summary>
    ///     Checks for overdue announcements on startup
    /// </summary>
    private async Task CheckForOverdueAnnouncements()
    {
        var now = DateTime.UtcNow;

        await using var uow = await db.CreateConnectionAsync();

        // Get guilds that should have announced this month but haven't
        var configs = await uow.GuildConfigs
            .Where(x => x.PatreonEnabled &&
                        x.PatreonChannelId != 0 &&
                        x.PatreonAnnouncementDay <= now.Day &&
                        (x.PatreonLastAnnouncement == null ||
                         x.PatreonLastAnnouncement.Value.Month != now.Month ||
                         x.PatreonLastAnnouncement.Value.Year != now.Year))
            .ToListAsync();

        logger.LogInformation("Found {Count} overdue Patreon announcements", configs.Count);

        foreach (var config in configs)
        {
            try
            {
                await ProcessGuildAnnouncement(config);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing overdue Patreon announcement for guild {GuildId}",
                    config.GuildId);
            }
        }
    }

    /// <summary>
    ///     Processes a single guild's Patreon announcement
    /// </summary>
    private async Task ProcessGuildAnnouncement(GuildConfig config)
    {
        var guild = client.GetGuild(config.GuildId);
        if (guild == null)
        {
            logger.LogWarning("Guild {GuildId} not found for Patreon announcement", config.GuildId);
            return;
        }

        var channel = guild.GetTextChannel(config.PatreonChannelId);
        if (channel == null)
        {
            logger.LogWarning("Patreon channel {ChannelId} not found in guild {GuildId}",
                config.PatreonChannelId, config.GuildId);
            return;
        }

        await SendPatreonAnnouncement(guild, channel, config, false);
    }

    /// <summary>
    ///     Sends the actual Patreon announcement message
    /// </summary>
    private async Task SendPatreonAnnouncement(IGuild guild, ITextChannel channel, GuildConfig config, bool isManual)
    {
        try
        {
            var socketGuild = guild as SocketGuild;
            var perms = socketGuild.CurrentUser.GetPermissions(channel);

            if (!perms.SendMessages)
            {
                logger.LogWarning("No permission to send messages in Patreon channel {ChannelId} for guild {GuildId}",
                    channel.Id, guild.Id);
                return;
            }

            // Get Patreon analytics for placeholders
            PatreonAnalytics? analytics = null;
            try
            {
                analytics = await GetAnalyticsAsync(guild.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to get Patreon analytics for announcement in guild {GuildId}", guild.Id);
            }

            // Prepare the message
            string message;
            if (!string.IsNullOrWhiteSpace(config.PatreonMessage))
            {
                // Use custom message with replacements including Patreon data
                var replacer = new ReplacementBuilder()
                    .WithServer(client, guild as SocketGuild)
                    .WithChannel(channel)
                    .WithOverride("%month%", () => DateTime.UtcNow.ToString("MMMM"))
                    .WithOverride("%year%", () => DateTime.UtcNow.Year.ToString())
                    .WithOverride("%patreon.link%", () => "https://patreon.com")
                    .WithPatreonData(analytics)
                    .Build();

                message = replacer.Replace(config.PatreonMessage);
            }
            else
            {
                // Use enhanced default message with supporter data
                var supporterCount = analytics?.ActiveSupporters ?? 0;
                var monthlyRevenue = analytics?.TotalMonthlyRevenue ?? 0;

                message = supporterCount > 0
                    ? $"🎉 It's a new month on Patreon! Thank you to our {supporterCount} amazing supporters who help us raise ${monthlyRevenue:F0}/month! Your support makes this community possible. 💖"
                    : "🎉 It's a new month on Patreon! Thank you to all our amazing supporters for making this community possible. Check out the latest rewards and consider supporting us!";
            }

            // Send the message
            if (SmartEmbed.TryParse(message, guild.Id, out var embed, out var plainText, out var components))
            {
                await channel.SendMessageAsync(plainText, embeds: embed, components: components?.Build());
            }
            else
            {
                await channel.SendMessageAsync(message);
            }

            // Update last announcement time
            await using var uow = await db.CreateConnectionAsync();
            config.PatreonLastAnnouncement = DateTime.UtcNow;
            await guildSettings.UpdateGuildConfig(guild.Id, config);

            logger.LogInformation(
                "Patreon announcement sent for guild {GuildId} in channel {ChannelId} (Manual: {IsManual})",
                guild.Id, channel.Id, isManual);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending Patreon announcement for guild {GuildId}", guild.Id);
        }
    }

    /// <summary>
    ///     Gets statistics about Patreon announcements
    /// </summary>
    public async Task<(int enabledGuilds, int totalAnnouncements)> GetPatreonStats()
    {
        await using var uow = await db.CreateConnectionAsync();

        var enabledGuilds = await uow.GuildConfigs
            .CountAsync(x => x.PatreonEnabled && x.PatreonChannelId != 0);

        var totalAnnouncements = await uow.GuildConfigs
            .CountAsync(x => x.PatreonLastAnnouncement != null);

        return (enabledGuilds, totalAnnouncements);
    }

    /// <summary>
    ///     Fetches and updates supporter data for a guild from Patreon API
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>Number of supporters updated, or -1 if failed</returns>
    public async Task<int> UpdateSupportersAsync(ulong guildId)
    {
        try
        {
            await using var uow = await db.CreateConnectionAsync();
            var guildConfig = await uow.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

            if (guildConfig?.PatreonCampaignId == null)
            {
                logger.LogWarning("No Patreon campaign ID found for guild {GuildId}", guildId);
                return -1;
            }

            // Ensure we have a valid token before proceeding
            var accessToken = await EnsureValidTokenAsync(guildId);
            if (accessToken == null)
            {
                logger.LogError("Unable to get valid Patreon access token for guild {GuildId}", guildId);
                return -1;
            }

            var supporters = new List<Member>();
            string? cursor = null;

            // Fetch all supporters with pagination
            do
            {
                var response = await apiClient.GetCampaignMembersAsync(
                    accessToken,
                    guildConfig.PatreonCampaignId,
                    cursor);

                if (response?.Data == null)
                {
                    logger.LogWarning("Failed to fetch supporters for guild {GuildId}", guildId);
                    break;
                }

                supporters.AddRange(response.Data);
                cursor = response.Links?.Next;
            } while (!string.IsNullOrEmpty(cursor));

            // Update database with supporter data
            var updatedCount = 0;
            foreach (var supporter in supporters)
            {
                try
                {
                    var existingSupporter = await uow.PatreonSupporters
                        .FirstOrDefaultAsync(x => x.GuildId == guildId && x.PatreonUserId == supporter.Id);

                    if (existingSupporter == null)
                    {
                        // Create new supporter record
                        var newSupporter = new PatreonSupporter
                        {
                            GuildId = guildId,
                            PatreonUserId = supporter.Id,
                            DiscordUserId = 0, // Will be linked later via commands
                            FullName = supporter.Attributes.FullName ?? "Unknown",
                            Email = supporter.Attributes.Email,
                            TierId = GetCurrentTierId(supporter),
                            AmountCents = supporter.Attributes.CurrentlyEntitledAmountCents ?? 0,
                            PatronStatus = supporter.Attributes.PatronStatus ?? "unknown",
                            PledgeRelationshipStart = supporter.Attributes.PledgeRelationshipStart,
                            LastChargeDate = supporter.Attributes.LastChargeDate,
                            LastChargeStatus = supporter.Attributes.LastChargeStatus,
                            LifetimeAmountCents = supporter.Attributes.CampaignLifetimeSupportCents ?? 0,
                            CurrentlyEntitledAmountCents = supporter.Attributes.CurrentlyEntitledAmountCents ?? 0,
                            LastUpdated = DateTime.UtcNow
                        };

                        await uow.InsertAsync(newSupporter);
                        updatedCount++;
                    }
                    else
                    {
                        // Update existing supporter record
                        await uow.PatreonSupporters
                            .Where(x => x.Id == existingSupporter.Id)
                            .UpdateAsync(x => new PatreonSupporter
                            {
                                FullName = supporter.Attributes.FullName ?? existingSupporter.FullName,
                                Email = supporter.Attributes.Email ?? existingSupporter.Email,
                                TierId = GetCurrentTierId(supporter),
                                AmountCents = supporter.Attributes.CurrentlyEntitledAmountCents ?? 0,
                                PatronStatus = supporter.Attributes.PatronStatus ?? existingSupporter.PatronStatus,
                                PledgeRelationshipStart =
                                    supporter.Attributes.PledgeRelationshipStart ??
                                    existingSupporter.PledgeRelationshipStart,
                                LastChargeDate =
                                    supporter.Attributes.LastChargeDate ?? existingSupporter.LastChargeDate,
                                LastChargeStatus =
                                    supporter.Attributes.LastChargeStatus ?? existingSupporter.LastChargeStatus,
                                LifetimeAmountCents =
                                    supporter.Attributes.CampaignLifetimeSupportCents ??
                                    existingSupporter.LifetimeAmountCents,
                                CurrentlyEntitledAmountCents =
                                    supporter.Attributes.CurrentlyEntitledAmountCents ??
                                    existingSupporter.CurrentlyEntitledAmountCents,
                                LastUpdated = DateTime.UtcNow
                            });
                        updatedCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error updating supporter {SupporterId} for guild {GuildId}", supporter.Id,
                        guildId);
                }
            }

            // Mark inactive supporters (not in current API response)
            logger.LogInformation(JsonSerializer.Serialize(supporters));
            var supporterIds = supporters.Select(s => s.Id).ToList();
            await uow.PatreonSupporters
                .Where(x => x.GuildId == guildId && !supporterIds.Contains(x.PatreonUserId))
                .UpdateAsync(x => new PatreonSupporter
                {
                    PatronStatus = "former_patron", LastUpdated = DateTime.UtcNow
                });

            logger.LogInformation("Updated {Count} supporters for guild {GuildId}", updatedCount, guildId);
            return updatedCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating supporters for guild {GuildId}", guildId);
            return -1;
        }
    }

    /// <summary>
    ///     Refreshes Patreon access token for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>New token response, or null if failed</returns>
    public async Task<TokenResponse?> RefreshTokenAsync(ulong guildId)
    {
        try
        {
            await using var uow = await db.CreateConnectionAsync();
            var guildConfig = await uow.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

            if (guildConfig?.PatreonRefreshToken == null)
            {
                logger.LogWarning("No Patreon refresh token found for guild {GuildId}", guildId);
                return null;
            }

            var tokenResponse = await apiClient.RefreshTokenAsync(guildConfig.PatreonRefreshToken,
                creds.PatreonClientId, creds.PatreonClientSecret);
            if (tokenResponse != null)
            {
                await uow.GuildConfigs
                    .Where(x => x.GuildId == guildId)
                    .UpdateAsync(x => new GuildConfig
                    {
                        PatreonAccessToken = tokenResponse.AccessToken,
                        PatreonRefreshToken = tokenResponse.RefreshToken,
                        PatreonTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                    });

                logger.LogInformation("Successfully refreshed Patreon token for guild {GuildId}", guildId);
            }

            return tokenResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing Patreon token for guild {GuildId}", guildId);
            return null;
        }
    }

    /// <summary>
    ///     Orchestrates a full synchronization of all Patreon data (supporters, tiers, goals) for a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID to sync.</param>
    public async Task SyncAllAsync(ulong guildId)
    {
        logger.LogInformation("Starting full Patreon data sync for guild {GuildId}", guildId);

        // Ensure tokens are valid before syncing - this will auto-refresh if needed
        var accessToken = await EnsureValidTokenAsync(guildId);
        if (accessToken == null)
        {
            logger.LogError("Could not get valid tokens for guild {GuildId}. Aborting full sync.", guildId);
            return;
        }

        await UpdateSupportersAsync(guildId);
        await UpdateTiersAndGoalsAsync(guildId);

        logger.LogInformation("Full Patreon data sync completed for guild {GuildId}", guildId);
    }

    /// <summary>
    ///     Fetches and updates Tiers and Goals for a guild from the Patreon API.
    ///     This is done in one call to be efficient, as both are 'included' on the campaign object.
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>Number of items updated, or -1 if failed</returns>
    public async Task<int> UpdateTiersAndGoalsAsync(ulong guildId)
    {
        try
        {
            await using var uow = await db.CreateConnectionAsync();
            var guildConfig = await uow.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

            if (guildConfig?.PatreonCampaignId == null)
            {
                logger.LogWarning("No Patreon campaign ID found for tier/goal sync in guild {GuildId}", guildId);
                return -1;
            }

            // Ensure we have a valid token before proceeding
            var accessToken = await EnsureValidTokenAsync(guildId);
            if (accessToken == null)
            {
                logger.LogError("Unable to get valid Patreon access token for guild {GuildId}", guildId);
                return -1;
            }

            var campaignResponse = await apiClient.GetCampaignAsync(accessToken, guildConfig.PatreonCampaignId);
            if (campaignResponse?.Included == null)
            {
                logger.LogWarning("Failed to fetch campaign data with includes for guild {GuildId}", guildId);
                return -1;
            }

            var includedData = campaignResponse.Included;
            var updatedCount = 0;

            // Process Tiers
            var apiTiers = includedData.Where(x => x.Type == "tier").ToList();
            foreach (var apiTier in apiTiers
                         .Select(apiTierResource => JsonSerializer.Serialize(apiTierResource, CachedJsonOptions))
                         .Select(tierJson => JsonSerializer.Deserialize<Tier>(tierJson, CachedJsonOptions))
                         .OfType<Tier>())
            {
                var existingTier =
                    await uow.PatreonTiers.FirstOrDefaultAsync(x => x.GuildId == guildId && x.TierId == apiTier.Id);
                // Parse Discord role ID from the API (takes first role if multiple are configured)
                var discordRoleId = apiTier.Attributes.DiscordRoleIds?.FirstOrDefault();
                var parsedRoleId = 0ul;
                if (discordRoleId != null && ulong.TryParse(discordRoleId, out var roleId))
                {
                    parsedRoleId = roleId;
                }

                if (existingTier == null)
                {
                    await uow.InsertAsync(new PatreonTier
                    {
                        GuildId = guildId,
                        TierId = apiTier.Id,
                        TierTitle = apiTier.Attributes.Title ?? "Untitled Tier",
                        AmountCents = apiTier.Attributes.AmountCents ?? 0,
                        DiscordRoleId = parsedRoleId,
                        Description = apiTier.Attributes.Description,
                        IsActive = apiTier.Attributes.Published ?? false,
                        DateAdded = DateTime.UtcNow
                    });
                }
                else
                {
                    await uow.PatreonTiers.Where(x => x.Id == existingTier.Id)
                        .UpdateAsync(x => new PatreonTier
                        {
                            TierTitle = apiTier.Attributes.Title ?? existingTier.TierTitle,
                            AmountCents = apiTier.Attributes.AmountCents ?? existingTier.AmountCents,
                            DiscordRoleId = parsedRoleId,
                            Description = apiTier.Attributes.Description ?? existingTier.Description,
                            IsActive = apiTier.Attributes.Published ?? existingTier.IsActive,
                            DateAdded = DateTime.UtcNow
                        });
                }

                updatedCount++;
            }

            logger.LogInformation("Updated {Count} tiers for guild {GuildId}", apiTiers.Count, guildId);

            // Goals are deprecated in Patreon API v2 - no longer processing them

            return updatedCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating tiers and goals for guild {GuildId}", guildId);
            return -1;
        }
    }

    /// <summary>
    ///     Gets active supporters for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>List of active supporters</returns>
    public async Task<List<PatreonSupporter>> GetActiveSupportersAsync(ulong guildId)
    {
        await using var uow = await db.CreateConnectionAsync();
        return await uow.PatreonSupporters
            .Where(x => x.GuildId == guildId && (
                x.PatronStatus == "active_patron" ||
                x.PatronStatus == "declined_patron" ||
                (x.PatronStatus == null && x.CurrentlyEntitledAmountCents > 0)))
            .OrderByDescending(x => x.AmountCents)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets Patreon tiers configured for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>List of configured tiers</returns>
    public async Task<List<PatreonTier>> GetTiersAsync(ulong guildId)
    {
        await using var uow = await db.CreateConnectionAsync();
        return await uow.PatreonTiers
            .Where(x => x.GuildId == guildId && x.IsActive)
            .OrderBy(x => x.AmountCents)
            .ToListAsync();
    }


    /// <summary>
    ///     Gets user identity information from Patreon API
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>User identity data or null if failed</returns>
    public async Task<User?> GetUserIdentityAsync(ulong guildId)
    {
        try
        {
            var accessToken = await EnsureValidTokenAsync(guildId);
            if (accessToken == null)
            {
                logger.LogError("Unable to get valid Patreon access token for user identity request in guild {GuildId}",
                    guildId);
                return null;
            }

            var response = await apiClient.GetUserIdentityAsync(accessToken);
            return response?.Data;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Patreon user identity for guild {GuildId}", guildId);
            return null;
        }
    }

    /// <summary>
    ///     Links a Discord user to a Patreon supporter
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="discordUserId">Discord user ID</param>
    /// <param name="patreonUserId">Patreon user ID</param>
    /// <returns>True if linked successfully</returns>
    public async Task<bool> LinkUserAsync(ulong guildId, ulong discordUserId, string patreonUserId)
    {
        try
        {
            await using var uow = await db.CreateConnectionAsync();
            var supporter = await uow.PatreonSupporters
                .FirstOrDefaultAsync(x => x.GuildId == guildId && x.PatreonUserId == patreonUserId);

            if (supporter == null)
            {
                return false;
            }

            await uow.PatreonSupporters
                .Where(x => x.Id == supporter.Id)
                .UpdateAsync(x => new PatreonSupporter
                {
                    DiscordUserId = discordUserId
                });

            // Trigger role sync for this user if enabled
            var config = await uow.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
            if (config?.PatreonRoleSync == true)
            {
                _ = SyncUserRolesAsync(guildId, discordUserId);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error linking user {DiscordUserId} to Patreon {PatreonUserId} for guild {GuildId}",
                discordUserId, patreonUserId, guildId);
            return false;
        }
    }

    /// <summary>
    ///     Synchronizes Discord roles for all linked Patreon supporters in a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>Number of users updated</returns>
    public async Task<int> SyncAllRolesAsync(ulong guildId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null)
            {
                logger.LogWarning("Guild {GuildId} not found for role sync", guildId);
                return 0;
            }

            await using var uow = await db.CreateConnectionAsync();

            // Check if role sync is enabled
            var config = await uow.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);
            if (config?.PatreonRoleSync != true)
            {
                logger.LogInformation("Patreon role sync is disabled for guild {GuildId}", guildId);
                return 0;
            }

            // Get all linked supporters
            var supporters = await uow.PatreonSupporters
                .Where(x => x.GuildId == guildId && x.DiscordUserId != 0 && (
                    x.PatronStatus == "active_patron" ||
                    x.PatronStatus == "declined_patron" ||
                    (x.PatronStatus == null && x.CurrentlyEntitledAmountCents > 0)))
                .ToListAsync();

            // Get tier mappings
            var tiers = await uow.PatreonTiers
                .Where(x => x.GuildId == guildId && x.IsActive && x.DiscordRoleId != 0)
                .ToListAsync();

            if (!tiers.Any())
            {
                logger.LogWarning("No Patreon tier role mappings configured for guild {GuildId}", guildId);
                return 0;
            }

            var updatedCount = 0;
            foreach (var supporter in supporters)
            {
                try
                {
                    if (await SyncUserRolesAsync(guildId, supporter.DiscordUserId))
                    {
                        updatedCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error syncing roles for user {UserId} in guild {GuildId}",
                        supporter.DiscordUserId,
                        guildId);
                }
            }

            logger.LogInformation("Synchronized roles for {Count} users in guild {GuildId}", updatedCount, guildId);
            return updatedCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing all roles for guild {GuildId}", guildId);
            return 0;
        }
    }

    /// <summary>
    ///     Synchronizes Discord roles for a specific user based on their Patreon support
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="discordUserId">Discord user ID</param>
    /// <returns>True if roles were updated</returns>
    public async Task<bool> SyncUserRolesAsync(ulong guildId, ulong discordUserId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null) return false;

            var user = guild.GetUser(discordUserId);
            if (user == null) return false;

            await using var uow = await db.CreateConnectionAsync();

            // Get supporter data
            var supporter = await uow.PatreonSupporters
                .FirstOrDefaultAsync(x => x.GuildId == guildId && x.DiscordUserId == discordUserId);

            // Get all tier mappings
            var tiers = await uow.PatreonTiers
                .Where(x => x.GuildId == guildId && x.IsActive && x.DiscordRoleId != 0)
                .OrderByDescending(x => x.AmountCents)
                .ToListAsync();

            if (!tiers.Any()) return false;

            // Get current Patreon-managed roles
            var patreonRoleIds = tiers.Select(t => t.DiscordRoleId).ToHashSet();
            var currentPatreonRoles = user.Roles.Where(r => patreonRoleIds.Contains(r.Id)).ToList();

            // Determine target role based on supporter status and amount
            IRole? targetRole = null;
            if (supporter is { AmountCents: > 0 } && (
                    supporter.PatronStatus == "active_patron" ||
                    supporter.PatronStatus == "declined_patron" ||
                    (supporter.PatronStatus == null && supporter.CurrentlyEntitledAmountCents > 0)))
            {
                // Find the highest tier the user qualifies for
                var qualifyingTier = tiers.FirstOrDefault(t => supporter.AmountCents >= t.AmountCents);
                if (qualifyingTier != null)
                {
                    targetRole = guild.GetRole(qualifyingTier.DiscordRoleId);
                }
            }

            // Remove roles that should no longer be assigned
            var rolesToRemove = currentPatreonRoles.Where(r => targetRole == null || r.Id != targetRole.Id).ToList();
            foreach (var role in rolesToRemove)
            {
                try
                {
                    await user.RemoveRoleAsync(role);
                    logger.LogInformation("Removed Patreon role {RoleName} from user {UserId} in guild {GuildId}",
                        role.Name, discordUserId, guildId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to remove role {RoleId} from user {UserId} in guild {GuildId}",
                        role.Id, discordUserId, guildId);
                }
            }

            // Add target role if not already assigned
            if (targetRole != null && !user.Roles.Any(r => r.Id == targetRole.Id))
            {
                try
                {
                    await user.AddRoleAsync(targetRole);
                    logger.LogInformation("Added Patreon role {RoleName} to user {UserId} in guild {GuildId}",
                        targetRole.Name, discordUserId, guildId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to add role {RoleId} to user {UserId} in guild {GuildId}",
                        targetRole.Id, discordUserId, guildId);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing roles for user {UserId} in guild {GuildId}", discordUserId, guildId);
            return false;
        }
    }

    /// <summary>
    ///     Maps a Patreon tier to a Discord role
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="tierId">Patreon tier ID</param>
    /// <param name="roleId">Discord role ID</param>
    /// <returns>True if mapping was successful</returns>
    public async Task<bool> MapTierToRoleAsync(ulong guildId, string tierId, ulong roleId)
    {
        try
        {
            await using var uow = await db.CreateConnectionAsync();

            var tier = await uow.PatreonTiers
                .FirstOrDefaultAsync(x => x.GuildId == guildId && x.TierId == tierId);

            if (tier == null)
            {
                logger.LogWarning("Patreon tier {TierId} not found for guild {GuildId}", tierId, guildId);
                return false;
            }

            await uow.PatreonTiers
                .Where(x => x.Id == tier.Id)
                .UpdateAsync(x => new PatreonTier
                {
                    DiscordRoleId = roleId
                });

            logger.LogInformation("Mapped Patreon tier {TierId} to Discord role {RoleId} for guild {GuildId}",
                tierId, roleId, guildId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error mapping tier {TierId} to role {RoleId} for guild {GuildId}",
                tierId, roleId, guildId);
            return false;
        }
    }

    /// <summary>
    ///     Toggles role synchronization for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>New role sync status</returns>
    public async Task<bool> ToggleRoleSyncAsync(ulong guildId)
    {
        try
        {
            await using var uow = await db.CreateConnectionAsync();
            var config = await uow.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

            if (config == null) return false;

            var newStatus = !config.PatreonRoleSync;
            await uow.GuildConfigs
                .Where(x => x.GuildId == guildId)
                .UpdateAsync(x => new GuildConfig
                {
                    PatreonRoleSync = newStatus
                });

            logger.LogInformation("Patreon role sync {Status} for guild {GuildId}",
                newStatus ? "enabled" : "disabled", guildId);
            return newStatus;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error toggling role sync for guild {GuildId}", guildId);
            return false;
        }
    }

    /// <summary>
    ///     Gets the current tier ID for a supporter from included relationships
    /// </summary>
    /// <param name="supporter">Patreon supporter data</param>
    /// <returns>Current tier ID or null</returns>
    private static string? GetCurrentTierId(Member supporter)
    {
        // Get the first entitled tier ID from relationships
        var currentlyEntitledTiers = supporter.Relationships?.CurrentlyEntitledTiers?.Data;
        return currentlyEntitledTiers?.FirstOrDefault()?.Id;
    }

    /// <summary>
    ///     Gets detailed analytics for Patreon supporters
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>Analytics data</returns>
    public async Task<PatreonAnalytics> GetAnalyticsAsync(ulong guildId)
    {
        try
        {
            await using var uow = await db.CreateConnectionAsync();

            var supporters = await uow.PatreonSupporters
                .Where(x => x.GuildId == guildId)
                .ToListAsync();

            var activeSupporters = supporters.Where(s =>
                s.PatronStatus == "active_patron" ||
                s.PatronStatus == "declined_patron" ||
                (s.PatronStatus == null && s.CurrentlyEntitledAmountCents > 0)).ToList();
            var formerSupporters = supporters.Where(s => s.PatronStatus == "former_patron").ToList();

            var totalRevenue = activeSupporters.Sum(s => s.AmountCents) / 100.0;
            var averageSupport = activeSupporters.Any() ? activeSupporters.Average(s => s.AmountCents) / 100.0 : 0;
            var lifetimeRevenue = supporters.Sum(s => s.LifetimeAmountCents) / 100.0;

            // Group by amount ranges
            var tierGroups = activeSupporters
                .GroupBy(s => GetTierGroup(s.AmountCents))
                .ToDictionary(g => g.Key, g => g.Count());

            // Monthly growth (approximate based on pledge start dates)
            var currentMonth = DateTime.UtcNow.Month;
            var currentYear = DateTime.UtcNow.Year;
            var newThisMonth = supporters.Count(s =>
                s.PledgeRelationshipStart?.Month == currentMonth &&
                s.PledgeRelationshipStart?.Year == currentYear);

            return new PatreonAnalytics
            {
                TotalSupporters = supporters.Count,
                ActiveSupporters = activeSupporters.Count,
                FormerSupporters = formerSupporters.Count,
                LinkedSupporters = supporters.Count(s => s.DiscordUserId != 0),
                TotalMonthlyRevenue = totalRevenue,
                AverageSupport = averageSupport,
                LifetimeRevenue = lifetimeRevenue,
                NewSupportersThisMonth = newThisMonth,
                TierDistribution = tierGroups,
                TopSupporters = activeSupporters
                    .OrderByDescending(s => s.AmountCents)
                    .Take(5)
                    .Select(s => new TopSupporter
                    {
                        Name = s.FullName, Amount = s.AmountCents / 100.0, IsLinked = s.DiscordUserId != 0
                    })
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting analytics for guild {GuildId}", guildId);
            return new PatreonAnalytics();
        }
    }

    /// <summary>
    ///     Gets tier group name based on amount
    /// </summary>
    /// <param name="amountCents">Amount in cents</param>
    /// <returns>Tier group name</returns>
    private static string GetTierGroup(int amountCents)
    {
        var amount = amountCents / 100.0;
        return amount switch
        {
            < 5 => "$1-$4",
            < 10 => "$5-$9",
            < 25 => "$10-$24",
            < 50 => "$25-$49",
            < 100 => "$50-$99",
            _ => "$100+"
        };
    }

    /// <summary>
    ///     Updates supporter recognition features like thank you messages
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="channelId">Channel to send thank you messages</param>
    /// <returns>Number of thank you messages sent</returns>
    public async Task<int> SendSupporterRecognitionAsync(ulong guildId, ulong channelId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null) return 0;

            var channel = guild.GetTextChannel(channelId);
            if (channel == null) return 0;

            await using var uow = await db.CreateConnectionAsync();

            // Get supporters who joined in the last 7 days
            var cutoffDate = DateTime.UtcNow.AddDays(-7);
            var newSupporters = await uow.PatreonSupporters
                .Where(x => x.GuildId == guildId &&
                            (x.PatronStatus == "active_patron" ||
                             x.PatronStatus == "declined_patron" ||
                             (x.PatronStatus == null && x.CurrentlyEntitledAmountCents > 0)) &&
                            x.PledgeRelationshipStart >= cutoffDate)
                .ToListAsync();

            var messagesSent = 0;
            foreach (var supporter in newSupporters.Take(5)) // Limit to prevent spam
            {
                try
                {
                    var embed = new EmbedBuilder()
                        .WithTitle(strings.NewPatreonSupporter(guild.Id))
                        .WithDescription(strings.PatreonWelcome(guild.Id, supporter.FullName))
                        .WithColor(0xF96854)
                        .AddField(strings.MonthlySupport(guild.Id), $"${supporter.AmountCents / 100.0:F2}", true)
                        .AddField("Started Supporting",
                            supporter.PledgeRelationshipStart?.ToString("MMMM dd, yyyy") ?? "Recently", true)
                        .WithThumbnailUrl("https://c5.patreon.com/external/logo/logomark_color_on_white.png");

                    if (supporter.DiscordUserId != 0)
                    {
                        var user = guild.GetUser(supporter.DiscordUserId);
                        if (user != null)
                        {
                            embed.AddField("Discord User", user.Mention, true);
                        }
                    }

                    await channel.SendMessageAsync(embed: embed.Build());
                    messagesSent++;

                    // Small delay to prevent rate limiting
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to send recognition message for supporter {SupporterId}",
                        supporter.PatreonUserId);
                }
            }

            logger.LogInformation("Sent {Count} supporter recognition messages in guild {GuildId}", messagesSent,
                guildId);
            return messagesSent;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending supporter recognition for guild {GuildId}", guildId);
            return 0;
        }
    }


    /// <summary>
    ///     Stores OAuth tokens and campaign information for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="accessToken">Patreon access token</param>
    /// <param name="refreshToken">Patreon refresh token</param>
    /// <param name="campaignId">Patreon campaign ID</param>
    /// <param name="expiresIn">Token expiration time in seconds</param>
    /// <returns>True if stored successfully</returns>
    public async Task<bool> StoreOAuthTokensAsync(ulong guildId, string accessToken, string refreshToken,
        string campaignId, int expiresIn)
    {
        try
        {
            var config = await guildSettings.GetGuildConfig(guildId);

            await using var uow = await db.CreateConnectionAsync();
            config.PatreonAccessToken = accessToken;
            config.PatreonRefreshToken = refreshToken;
            config.PatreonCampaignId = campaignId;
            config.PatreonTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);

            await guildSettings.UpdateGuildConfig(guildId, config);

            logger.LogInformation("Stored Patreon OAuth tokens for guild {GuildId} with campaign {CampaignId}",
                guildId, campaignId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error storing Patreon OAuth tokens for guild {GuildId}", guildId);
            return false;
        }
    }

    /// <summary>
    ///     Ensures tokens are valid and refreshes them if needed before API operations
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>Valid access token or null if refresh failed</returns>
    public async Task<string?> EnsureValidTokenAsync(ulong guildId)
    {
        try
        {
            await using var uow = await db.CreateConnectionAsync();
            var guildConfig = await uow.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

            if (guildConfig?.PatreonAccessToken == null || guildConfig.PatreonRefreshToken == null)
            {
                logger.LogWarning("No Patreon tokens found for guild {GuildId}", guildId);
                return null;
            }

            // Check if token needs refreshing (refresh 10 minutes before expiry)
            if (guildConfig.PatreonTokenExpiry <= DateTime.UtcNow.AddMinutes(10))
            {
                logger.LogInformation("Patreon token for guild {GuildId} expires soon, refreshing...", guildId);

                var refreshedToken = await RefreshTokenAsync(guildId);
                if (refreshedToken == null)
                {
                    logger.LogError("Failed to refresh Patreon token for guild {GuildId}", guildId);
                    return null;
                }

                return refreshedToken.AccessToken;
            }

            return guildConfig.PatreonAccessToken;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ensuring valid token for guild {GuildId}", guildId);
            return null;
        }
    }

    /// <summary>
    ///     Disconnects Patreon integration for a guild by clearing OAuth tokens while preserving configuration
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>True if disconnected successfully</returns>
    public async Task<bool> DisconnectPatreonAsync(ulong guildId)
    {
        try
        {
            var config = await guildSettings.GetGuildConfig(guildId);

            await using var uow = await db.CreateConnectionAsync();

            // Clear OAuth tokens and campaign information only
            // Preserve announcement settings, messages, channels, etc.
            config.PatreonAccessToken = null;
            config.PatreonRefreshToken = null;
            config.PatreonCampaignId = null;
            config.PatreonTokenExpiry = null;

            await guildSettings.UpdateGuildConfig(guildId, config);

            // Clear cached supporter/tier data since it needs fresh OAuth to re-sync
            await uow.PatreonSupporters
                .Where(x => x.GuildId == guildId)
                .DeleteAsync();

            await uow.PatreonTiers
                .Where(x => x.GuildId == guildId)
                .DeleteAsync();

            await uow.PatreonGoals
                .Where(x => x.GuildId == guildId)
                .DeleteAsync();

            logger.LogInformation("Successfully disconnected Patreon OAuth for guild {GuildId} (settings preserved)",
                guildId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disconnecting Patreon integration for guild {GuildId}", guildId);
            return false;
        }
    }
}

/// <summary>
///     Patreon analytics data
/// </summary>
public class PatreonAnalytics
{
    /// <summary>
    ///     Total number of supporters (all statuses)
    /// </summary>
    public int TotalSupporters { get; set; }

    /// <summary>
    ///     Number of active supporters
    /// </summary>
    public int ActiveSupporters { get; set; }

    /// <summary>
    ///     Number of former supporters
    /// </summary>
    public int FormerSupporters { get; set; }

    /// <summary>
    ///     Number of supporters linked to Discord
    /// </summary>
    public int LinkedSupporters { get; set; }

    /// <summary>
    ///     Total monthly revenue from active supporters
    /// </summary>
    public double TotalMonthlyRevenue { get; set; }

    /// <summary>
    ///     Average support amount
    /// </summary>
    public double AverageSupport { get; set; }

    /// <summary>
    ///     Total lifetime revenue from all supporters
    /// </summary>
    public double LifetimeRevenue { get; set; }

    /// <summary>
    ///     Number of new supporters this month
    /// </summary>
    public int NewSupportersThisMonth { get; set; }

    /// <summary>
    ///     Distribution of supporters by tier groups
    /// </summary>
    public Dictionary<string, int> TierDistribution { get; set; } = new();

    /// <summary>
    ///     Top 5 supporters
    /// </summary>
    public List<TopSupporter> TopSupporters { get; set; } = new();
}

/// <summary>
///     Top supporter information
/// </summary>
public class TopSupporter
{
    /// <summary>
    ///     Supporter name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    ///     Monthly support amount
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    ///     Whether supporter is linked to Discord
    /// </summary>
    public bool IsLinked { get; set; }
}