using System.Text.Json;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Controllers.Common.Wizard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceUserAction = Mewdeko.Services.UserAction;
using WizardTypeController = Mewdeko.Controllers.Common.Wizard.WizardType;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for first-time wizard functionality
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class WizardController : Controller
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly GuildSettingsService guildSettings;
    private readonly ILogger<WizardController> logger;
    private readonly WizardDecisionService wizardService;

    /// <summary>
    ///     Initializes a new instance of the WizardController
    /// </summary>
    public WizardController(
        WizardDecisionService wizardService,
        IDataConnectionFactory dbFactory,
        DiscordShardedClient client,
        ILogger<WizardController> logger,
        GuildSettingsService guildSettings)
    {
        this.wizardService = wizardService;
        this.dbFactory = dbFactory;
        this.client = client;
        this.logger = logger;
        this.guildSettings = guildSettings;
    }

    /// <summary>
    ///     Determines whether to show wizard for specific user and guild
    /// </summary>
    /// <param name="userId">Discord user ID</param>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>Decision on whether to show wizard</returns>
    [HttpGet("should-show/{userId:long}/{guildId:long}")]
    public async Task<ActionResult<WizardDecisionResponse>> ShouldShowWizard(ulong userId, ulong guildId)
    {
        try
        {
            var decision = await wizardService.ShouldShowWizardAsync(userId, guildId);
            await using var db = await dbFactory.CreateConnectionAsync();
            var user = await db.GetTable<DiscordUser>().FirstOrDefaultAsync(u => u.UserId == userId);
            var guildConfig = await guildSettings.GetGuildConfig(guildId);

            var completedGuilds = JsonSerializer.Deserialize<List<ulong>>(user?.WizardCompletedGuilds ?? "[]");

            return Ok(new WizardDecisionResponse
            {
                ShowWizard = decision.ShowWizard,
                ShowSuggestion = decision.ShowSuggestion,
                WizardType = (WizardTypeController)decision.WizardType,
                Reason = decision.Reason,
                Context = new WizardContext
                {
                    ExperienceLevel = user?.DashboardExperienceLevel ?? 0,
                    IsFirstDashboardAccess = user?.FirstDashboardAccess == null ||
                                             user.FirstDashboardAccess.Value.Date == DateTime.UtcNow.Date,
                    CompletedWizardCount = completedGuilds.Count,
                    GuildHasBasicSetup = guildConfig?.HasBasicSetup ?? false
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking wizard state for user {UserId} and guild {GuildId}", userId, guildId);
            return StatusCode(500, "Error checking wizard state");
        }
    }

    /// <summary>
    ///     Gets current wizard state for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>Current wizard state</returns>
    [HttpGet("state/{guildId:long}")]
    public async Task<ActionResult<WizardStateResponse>> GetWizardState(ulong guildId)
    {
        try
        {
            var state = await wizardService.GetGuildWizardStateAsync(guildId);

            return Ok(new WizardStateResponse
            {
                GuildId = state.GuildId,
                Completed = state.Completed,
                Skipped = state.Skipped,
                CompletedAt = state.CompletedAt,
                CompletedByUserId = state.CompletedByUserId,
                HasBasicSetup = state.HasBasicSetup,
                CurrentStep = 1, // Could be enhanced to track actual step
                ConfiguredFeatures = await GetConfiguredFeatures(guildId)
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting wizard state for guild {GuildId}", guildId);
            return StatusCode(500, "Error getting wizard state");
        }
    }

    /// <summary>
    ///     Updates wizard state for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="request">Update request</param>
    /// <returns>Success status</returns>
    [HttpPost("state/{guildId:long}")]
    public async Task<ActionResult> UpdateWizardState(ulong guildId, [FromBody] WizardStateUpdateRequest request)
    {
        try
        {
            // Only master instance can update wizard state
            if (!wizardService.IsMasterInstance())
            {
                return BadRequest("Only master instance can update wizard state");
            }

            if (request.MarkCompleted)
            {
                await wizardService.MarkWizardCompletedAsync(request.UserId, guildId, request.ConfiguredFeatures);
                await wizardService.UpdateUserExperienceAsync(request.UserId, ServiceUserAction.CompletedFirstWizard);
            }
            else if (request.MarkSkipped)
            {
                await wizardService.SkipWizardAsync(guildId, request.UserId);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating wizard state for guild {GuildId}", guildId);
            return StatusCode(500, "Error updating wizard state");
        }
    }

    /// <summary>
    ///     Completes the wizard setup for a guild
    /// </summary>
    /// <param name="userId">Discord user ID</param>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="configuredFeatures">Array of configured feature IDs</param>
    /// <returns>Completion result</returns>
    [HttpPost("complete/{userId:long}/{guildId:long}")]
    public async Task<ActionResult<WizardCompleteResponse>> CompleteWizard(ulong userId, ulong guildId,
        [FromBody] string[] configuredFeatures)
    {
        try
        {
            await using var userDb = await dbFactory.CreateConnectionAsync();
            var user = await userDb.GetTable<DiscordUser>().FirstOrDefaultAsync(u => u.UserId == userId);
            var wasFirstWizard = user?.HasCompletedAnyWizard != true;

            await wizardService.MarkWizardCompletedAsync(userId, guildId, configuredFeatures);
            await wizardService.UpdateUserExperienceAsync(userId, ServiceUserAction.CompletedFirstWizard);

            // Update experience level if they configured multiple features
            if (configuredFeatures.Length >= 3)
            {
                await wizardService.UpdateUserExperienceAsync(userId, ServiceUserAction.ConfiguredMultipleFeatures);
            }

            // Get updated user data
            var updatedUser = await userDb.GetTable<DiscordUser>().FirstOrDefaultAsync(u => u.UserId == userId);

            var nextSteps = GenerateNextSteps(configuredFeatures, guildId);

            return Ok(new WizardCompleteResponse
            {
                Success = true,
                GuildId = guildId,
                UserId = userId,
                ConfiguredFeatures = configuredFeatures,
                FailedFeatures = [],
                CompletedAt = DateTime.UtcNow,
                NewExperienceLevel = updatedUser?.DashboardExperienceLevel ?? 0,
                WasFirstWizard = wasFirstWizard,
                NextSteps = nextSteps
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error completing wizard for user {UserId} and guild {GuildId}", userId, guildId);
            return Ok(new WizardCompleteResponse
            {
                Success = false, GuildId = guildId, UserId = userId, ErrorMessage = "Failed to complete wizard setup"
            });
        }
    }

    /// <summary>
    ///     Skips the wizard for a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <param name="userId">User ID who is skipping</param>
    /// <returns>Success status</returns>
    [HttpPost("skip/{guildId:long}")]
    public async Task<ActionResult> SkipWizard(ulong guildId, [FromBody] ulong userId)
    {
        try
        {
            await wizardService.SkipWizardAsync(guildId, userId);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error skipping wizard for guild {GuildId}", guildId);
            return StatusCode(500, "Error skipping wizard");
        }
    }

    /// <summary>
    ///     Checks bot permissions in a guild
    /// </summary>
    /// <param name="guildId">Discord guild ID</param>
    /// <returns>Permission check results</returns>
    [HttpGet("permissions/{guildId:long}")]
    public async Task<ActionResult<PermissionCheckResponse>> CheckBotPermissions(ulong guildId)
    {
        try
        {
            var guild = client.GetGuild(guildId);
            if (guild == null)
            {
                return NotFound("Guild not found");
            }

            var botUser = guild.GetUser(client.CurrentUser.Id);
            if (botUser == null)
            {
                return NotFound("Bot not found in guild");
            }

            var permissions = botUser.GuildPermissions;
            var permissionResults = new List<PermissionCheckResult>();
            var missingCritical = new List<GuildPermission>();
            var missingRecommended = new List<GuildPermission>();

            // Define required permissions
            var criticalPermissions = new[]
            {
                GuildPermission.ViewChannel, GuildPermission.SendMessages, GuildPermission.ReadMessageHistory,
                GuildPermission.UseExternalEmojis, GuildPermission.EmbedLinks
            };

            var recommendedPermissions = new[]
            {
                GuildPermission.ManageMessages, GuildPermission.ManageRoles, GuildPermission.ManageChannels,
                GuildPermission.KickMembers, GuildPermission.BanMembers, GuildPermission.AddReactions,
                GuildPermission.AttachFiles, GuildPermission.MentionEveryone
            };

            // Check critical permissions
            foreach (var permission in criticalPermissions)
            {
                var hasPermission = permissions.Has(permission);
                if (!hasPermission)
                {
                    missingCritical.Add(permission);
                }

                permissionResults.Add(new PermissionCheckResult
                {
                    Permission = permission,
                    PermissionName = permission.ToString(),
                    HasPermission = hasPermission,
                    Importance = PermissionImportance.Critical,
                    Description = GetPermissionDescription(permission),
                    RequiredForFeatures = GetRequiredFeatures(permission)
                });
            }

            // Check recommended permissions
            foreach (var permission in recommendedPermissions)
            {
                var hasPermission = permissions.Has(permission);
                if (!hasPermission)
                {
                    missingRecommended.Add(permission);
                }

                permissionResults.Add(new PermissionCheckResult
                {
                    Permission = permission,
                    PermissionName = permission.ToString(),
                    HasPermission = hasPermission,
                    Importance = PermissionImportance.Recommended,
                    Description = GetPermissionDescription(permission),
                    RequiredForFeatures = GetRequiredFeatures(permission)
                });
            }

            var hasAllRequired = missingCritical.Count == 0;
            var canFunction = missingCritical.Count <= 2; // Allow some flexibility
            var healthStatus = GetHealthStatus(missingCritical.Count, missingRecommended.Count);

            var response = new PermissionCheckResponse
            {
                GuildId = guildId,
                BotId = client.CurrentUser.Id,
                HasAllRequiredPermissions = hasAllRequired,
                PermissionResults = permissionResults.ToArray(),
                MissingCriticalPermissions = missingCritical.ToArray(),
                MissingRecommendedPermissions = missingRecommended.ToArray(),
                CanFunction = canFunction,
                HealthStatus = healthStatus
            };

            // Generate invite URL if permissions are missing
            if (!hasAllRequired)
            {
                // Use the standard permission set that Mewdeko normally requests
                const ulong standardPermissions = 66186303;
                response.SuggestedInviteUrl =
                    $"https://discord.com/oauth2/authorize?client_id={client.CurrentUser.Id}&permissions={standardPermissions}&scope=bot%20applications.commands&guild_id={guildId}";
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking permissions for guild {GuildId}", guildId);
            return StatusCode(500, "Error checking bot permissions");
        }
    }

    /// <summary>
    ///     Updates user wizard preferences
    /// </summary>
    /// <param name="userId">Discord user ID</param>
    /// <param name="request">Preferences update request</param>
    /// <returns>Updated preferences</returns>
    [HttpPost("user-preferences/{userId:long}")]
    public async Task<ActionResult<UserPreferencesResponse>> UpdateUserPreferences(ulong userId,
        [FromBody] WizardPreferencesRequest request)
    {
        try
        {
            await wizardService.UpdateUserPreferencesAsync(userId, request.PrefersGuidedSetup);

            await using var db = await dbFactory.CreateConnectionAsync();

            // Update experience level if specified
            if (request.PreferredExperienceLevel.HasValue)
            {
                await db.GetTable<DiscordUser>()
                    .Where(u => u.UserId == userId)
                    .UpdateAsync(u => new DiscordUser
                    {
                        DashboardExperienceLevel = request.PreferredExperienceLevel.Value
                    });
            }

            var user = await db.GetTable<DiscordUser>().FirstOrDefaultAsync(u => u.UserId == userId);
            var completedGuilds = JsonSerializer.Deserialize<List<ulong>>(user?.WizardCompletedGuilds ?? "[]");

            return Ok(new UserPreferencesResponse
            {
                UserId = userId,
                PrefersGuidedSetup = user?.PrefersGuidedSetup ?? true,
                ExperienceLevel = user?.DashboardExperienceLevel ?? 0,
                HasCompletedAnyWizard = user?.HasCompletedAnyWizard ?? false,
                WizardCompletedCount = completedGuilds.Count,
                FirstDashboardAccess = user?.FirstDashboardAccess
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating user preferences for user {UserId}", userId);
            return StatusCode(500, "Error updating user preferences");
        }
    }

    /// <summary>
    ///     Sets up welcome messages feature
    /// </summary>
    [HttpPost("setup/welcome")]
    public async Task<ActionResult<FeatureConfigResult>> SetupWelcome([FromBody] WelcomeSetupRequest request)
    {
        try
        {
            // Configure welcome messages
            var multiGreet = new MultiGreet
            {
                GuildId = request.GuildId,
                ChannelId = request.ChannelId,
                Message = request.WelcomeMessage,
                GreetBots = false,
                DeleteTime = request.AutoDelete ? request.AutoDeleteTimer : 0,
                Disabled = false
            };

            await using var db = await dbFactory.CreateConnectionAsync();
            await db.InsertAsync(multiGreet);

            // Configure DM greeting if requested
            if (request.SendDmGreeting)
            {
                await db.GetTable<GuildConfig>()
                    .Where(g => g.GuildId == request.GuildId)
                    .UpdateAsync(g => new GuildConfig
                    {
                        SendDmGreetMessage = true, DmGreetMessageText = request.DmGreetingMessage
                    });
            }

            return Ok(new FeatureConfigResult
            {
                FeatureId = WizardFeatures.MultiGreets,
                FeatureName = "Welcome Messages",
                Success = true,
                ConfigurationApplied = new Dictionary<string, object>
                {
                    ["ChannelId"] = request.ChannelId,
                    ["Message"] = request.WelcomeMessage,
                    ["SendDmGreeting"] = request.SendDmGreeting,
                    ["AutoDelete"] = request.AutoDelete
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting up welcome messages for guild {GuildId}", request.GuildId);
            return Ok(new FeatureConfigResult
            {
                FeatureId = WizardFeatures.MultiGreets,
                FeatureName = "Welcome Messages",
                Success = false,
                ErrorMessage = "Failed to configure welcome messages"
            });
        }
    }

    /// <summary>
    ///     Sets up moderation features
    /// </summary>
    [HttpPost("setup/moderation")]
    public async Task<ActionResult<FeatureConfigResult>> SetupModeration([FromBody] ModerationSetupRequest request)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            await db.GetTable<GuildConfig>()
                .Where(g => g.GuildId == request.GuildId)
                .UpdateAsync(g => new GuildConfig
                {
                    FilterInvites = request.FilterInvites,
                    FilterLinks = request.FilterLinks,
                    FilterWords = request.FilterWords,
                    WarnlogChannelId = request.LogChannelId ?? 0
                });

            // Add custom filtered words if specified
            if (request.CustomFilteredWords.Any())
            {
                var filteredWords = request.CustomFilteredWords.Select(word => new FilteredWord
                {
                    GuildId = request.GuildId, Word = word
                });

                await db.BulkCopyAsync(filteredWords);
            }

            return Ok(new FeatureConfigResult
            {
                FeatureId = WizardFeatures.Moderation,
                FeatureName = "Moderation",
                Success = true,
                ConfigurationApplied = new Dictionary<string, object>
                {
                    ["FilterInvites"] = request.FilterInvites,
                    ["FilterLinks"] = request.FilterLinks,
                    ["FilterWords"] = request.FilterWords,
                    ["CustomWordsCount"] = request.CustomFilteredWords.Length,
                    ["LogChannelId"] = request.LogChannelId
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting up moderation for guild {GuildId}", request.GuildId);
            return Ok(new FeatureConfigResult
            {
                FeatureId = WizardFeatures.Moderation,
                FeatureName = "Moderation",
                Success = false,
                ErrorMessage = "Failed to configure moderation features"
            });
        }
    }

    // Helper methods
    private async Task<string[]> GetConfiguredFeatures(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var features = new List<string>();

        if (await db.GetTable<MultiGreet>().AnyAsync(mg => mg.GuildId == guildId))
            features.Add(WizardFeatures.MultiGreets);

        var guildConfig = await guildSettings.GetGuildConfig(guildId);
        if (guildConfig != null)
        {
            if (guildConfig.FilterWords || guildConfig.FilterInvites || guildConfig.FilterLinks)
                features.Add(WizardFeatures.Moderation);
        }

        if (await db.GetTable<GuildXpSetting>().AnyAsync(xp => xp.GuildId == guildId))
            features.Add(WizardFeatures.XpSystem);

        if (await db.GetTable<Starboard>().AnyAsync(sb => sb.GuildId == guildId))
            features.Add(WizardFeatures.Starboard);

        return features.ToArray();
    }

    private static string[] GenerateNextSteps(string[] configuredFeatures, ulong guildId)
    {
        var steps = new List<string>();

        if (configuredFeatures.Contains(WizardFeatures.MultiGreets))
        {
            steps.Add("Test your welcome messages by having someone join the server");
        }

        if (configuredFeatures.Contains(WizardFeatures.Moderation))
        {
            steps.Add("Review moderation settings and adjust as needed");
        }

        if (configuredFeatures.Contains(WizardFeatures.XpSystem))
        {
            steps.Add("Check XP settings and configure role rewards");
        }

        steps.Add("Explore other dashboard features to customize your server further");
        steps.Add("Join our support server if you need help with advanced configuration");

        return steps.ToArray();
    }

    private static string GetPermissionDescription(GuildPermission permission)
    {
        return permission switch
        {
            GuildPermission.ViewChannel => "Required to see and interact with channels",
            GuildPermission.SendMessages => "Required to send messages and responses",
            GuildPermission.ReadMessageHistory => "Required to read previous messages for moderation",
            GuildPermission.ManageMessages => "Required for moderation features like message deletion",
            GuildPermission.ManageRoles => "Required for role rewards, auto-roles, and moderation",
            GuildPermission.ManageChannels => "Required for advanced features like tickets",
            GuildPermission.KickMembers => "Required for moderation actions",
            GuildPermission.BanMembers => "Required for ban-related moderation features",
            GuildPermission.AddReactions => "Required for interactive features and starboard",
            GuildPermission.EmbedLinks => "Required to display rich embeds and formatted messages",
            GuildPermission.AttachFiles => "Required to send images and files",
            GuildPermission.UseExternalEmojis => "Required to use custom emojis from other servers",
            GuildPermission.MentionEveryone => "Required for certain notification features",
            _ => "Required for various bot features"
        };
    }

    private static string[] GetRequiredFeatures(GuildPermission permission)
    {
        return permission switch
        {
            GuildPermission.ViewChannel => new[]
            {
                "All Features"
            },
            GuildPermission.SendMessages => new[]
            {
                "All Features"
            },
            GuildPermission.ReadMessageHistory => new[]
            {
                "Moderation", "Logging", "Starboard"
            },
            GuildPermission.ManageMessages => new[]
            {
                "Moderation", "Auto-Delete", "Cleanup"
            },
            GuildPermission.ManageRoles => new[]
            {
                "XP Rewards", "Auto-Roles", "Moderation"
            },
            GuildPermission.ManageChannels => new[]
            {
                "Tickets", "Custom Voice"
            },
            GuildPermission.KickMembers => new[]
            {
                "Moderation", "Auto-Moderation"
            },
            GuildPermission.BanMembers => new[]
            {
                "Moderation", "Auto-Ban"
            },
            GuildPermission.AddReactions => new[]
            {
                "Starboard", "Polls", "Interactive Features"
            },
            GuildPermission.EmbedLinks => new[]
            {
                "Most Features", "Rich Embeds"
            },
            GuildPermission.AttachFiles => new[]
            {
                "XP Cards", "Charts", "Image Commands"
            },
            GuildPermission.UseExternalEmojis => new[]
            {
                "Custom Reactions"
            },
            GuildPermission.MentionEveryone => new[]
            {
                "Announcements", "Important Notifications"
            },
            _ => new[]
            {
                "Various Features"
            }
        };
    }

    private static PermissionHealthStatus GetHealthStatus(int missingCritical, int missingRecommended)
    {
        if (missingCritical == 0 && missingRecommended == 0)
            return PermissionHealthStatus.Excellent;

        if (missingCritical == 0 && missingRecommended <= 2)
            return PermissionHealthStatus.Good;

        if (missingCritical <= 1)
            return PermissionHealthStatus.Warning;

        return PermissionHealthStatus.Poor;
    }
}