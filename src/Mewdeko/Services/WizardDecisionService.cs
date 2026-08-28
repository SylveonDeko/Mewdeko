using System.Text.Json;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Microsoft.Extensions.Caching.Memory;

namespace Mewdeko.Services;

/// <summary>
///     Service that handles first-time wizard decision logic and state management
/// </summary>
public class WizardDecisionService : INService
{
    private readonly IMemoryCache cache;
    private readonly TimeSpan cacheExpiration = TimeSpan.FromMinutes(10);
    private readonly IDataConnectionFactory dbFactory;
    private readonly GuildSettingsService guildSettings;
    private readonly ILogger<WizardDecisionService> logger;

    /// <summary>
    ///     Initializes a new instance of the WizardDecisionService
    /// </summary>
    public WizardDecisionService(
        ILogger<WizardDecisionService> logger,
        IDataConnectionFactory dbFactory,
        GuildSettingsService guildSettings,
        IMemoryCache cache)
    {
        this.logger = logger;
        this.dbFactory = dbFactory;
        this.guildSettings = guildSettings;
        this.cache = cache;
    }

    /// <summary>
    ///     Determines whether to show the wizard for a specific user and guild combination
    /// </summary>
    public async Task<WizardDecision> ShouldShowWizardAsync(ulong userId, ulong guildId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var user = await db.GetTable<DiscordUser>().FirstOrDefaultAsync(u => u.UserId == userId);
            var guildConfig = await guildSettings.GetGuildConfig(guildId);

            // Never show if guild already has wizard completed or skipped
            if (guildConfig?.WizardCompleted == true || guildConfig?.WizardSkipped == true)
            {
                return new WizardDecision
                {
                    ShowWizard = false, Reason = "Guild wizard already completed or skipped"
                };
            }

            // Ensure we have user data - create if needed
            if (user == null)
            {
                user = new DiscordUser
                {
                    UserId = userId,
                    HasCompletedAnyWizard = false,
                    DashboardExperienceLevel = 0,
                    PrefersGuidedSetup = true,
                    FirstDashboardAccess = DateTime.UtcNow,
                    WizardCompletedGuilds = "[]"
                };
                await db.InsertAsync(user);
            }
            else if (user.FirstDashboardAccess == null)
            {
                // Update existing user with first dashboard access
                await db.GetTable<DiscordUser>()
                    .Where(u => u.UserId == userId)
                    .UpdateAsync(u => new DiscordUser
                    {
                        FirstDashboardAccess = DateTime.UtcNow
                    });
            }

            // First-time dashboard user = always show full wizard
            if (!user.HasCompletedAnyWizard)
            {
                return new WizardDecision
                {
                    ShowWizard = true,
                    WizardType = WizardType.FirstTime,
                    Reason = "First-time user - never completed any wizard"
                };
            }

            // Check if user has completed wizard for this specific guild
            var completedGuilds = DeserializeCompletedGuilds(user.WizardCompletedGuilds);
            if (completedGuilds.Contains(guildId))
            {
                return new WizardDecision
                {
                    ShowWizard = false, Reason = "User already completed wizard for this guild"
                };
            }

            // Experienced user (level 2+) with unconfigured guild - show suggestion only
            if (user.DashboardExperienceLevel >= 2)
            {
                var hasBasicSetup = guildConfig?.HasBasicSetup == true || await CheckHasBasicSetup(guildId);
                return new WizardDecision
                {
                    ShowWizard = false,
                    ShowSuggestion = !hasBasicSetup,
                    Reason = hasBasicSetup
                        ? "Experienced user with configured guild"
                        : "Experienced user - show suggestion only"
                };
            }

            // Basic user (level 1) with new guild - show quick setup if they prefer guided setup
            if (user.PrefersGuidedSetup)
            {
                var hasBasicSetup = guildConfig?.HasBasicSetup == true || await CheckHasBasicSetup(guildId);
                if (!hasBasicSetup)
                {
                    return new WizardDecision
                    {
                        ShowWizard = true,
                        WizardType = WizardType.QuickSetup,
                        Reason = "User prefers guided setup for new guilds"
                    };
                }
            }

            return new WizardDecision
            {
                ShowWizard = false, Reason = "No wizard needed based on user preferences and guild state"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error determining wizard state for user {UserId} and guild {GuildId}", userId,
                guildId);
            return new WizardDecision
            {
                ShowWizard = false, Reason = "Error occurred while checking wizard state"
            };
        }
    }

    /// <summary>
    ///     Gets the current wizard state for a guild
    /// </summary>
    public async Task<WizardState> GetGuildWizardStateAsync(ulong guildId)
    {
        try
        {
            var guildConfig = await guildSettings.GetGuildConfig(guildId);

            return new WizardState
            {
                GuildId = guildId,
                Completed = guildConfig?.WizardCompleted ?? false,
                Skipped = guildConfig?.WizardSkipped ?? false,
                CompletedAt = guildConfig?.WizardCompletedAt,
                CompletedByUserId = guildConfig?.WizardCompletedByUserId ?? 0,
                HasBasicSetup = guildConfig?.HasBasicSetup ?? await CheckHasBasicSetup(guildId)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting wizard state for guild {GuildId}", guildId);
            return new WizardState
            {
                GuildId = guildId, Completed = false, Skipped = false, HasBasicSetup = false
            };
        }
    }

    /// <summary>
    ///     Marks the wizard as completed for a specific user and guild
    /// </summary>
    public async Task MarkWizardCompletedAsync(ulong userId, ulong guildId, string[] completedFeatures)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            await using var transaction = await db.BeginTransactionAsync();

            // Update user experience
            var user = await db.GetTable<DiscordUser>().FirstOrDefaultAsync(u => u.UserId == userId);
            if (user != null)
            {
                // Mark user as having completed any wizard
                var hasCompletedAnyWizard = true;

                // Progress experience level if this is their first wizard
                var newExperienceLevel = user.DashboardExperienceLevel == 0 ? 1 : user.DashboardExperienceLevel;

                // Add this guild to completed list
                var completedGuilds = DeserializeCompletedGuilds(user.WizardCompletedGuilds);
                if (!completedGuilds.Contains(guildId))
                {
                    completedGuilds.Add(guildId);
                }

                var wizardCompletedGuilds = JsonSerializer.Serialize(completedGuilds);

                await db.GetTable<DiscordUser>()
                    .Where(u => u.UserId == userId)
                    .UpdateAsync(u => new DiscordUser
                    {
                        HasCompletedAnyWizard = hasCompletedAnyWizard,
                        DashboardExperienceLevel = newExperienceLevel,
                        WizardCompletedGuilds = wizardCompletedGuilds
                    });
            }

            await db.GetTable<GuildConfig>()
                .Where(g => g.GuildId == guildId)
                .UpdateAsync(g => new GuildConfig
                {
                    WizardCompleted = true,
                    WizardCompletedAt = DateTime.UtcNow,
                    WizardCompletedByUserId = userId,
                    HasBasicSetup = completedFeatures.Length > 0
                });

            await transaction.CommitAsync();

            cache.Remove($"wizard_basic_setup_{guildId}");
            guildSettings.ClearCacheForGuild(guildId);

            logger.LogInformation("Wizard completed for user {UserId} in guild {GuildId} with {FeatureCount} features",
                userId, guildId, completedFeatures.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error marking wizard completed for user {UserId} and guild {GuildId}", userId,
                guildId);
            throw;
        }
    }

    /// <summary>
    ///     Marks the wizard as skipped for a guild
    /// </summary>
    public async Task SkipWizardAsync(ulong guildId, ulong userId)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Update guild config to mark as skipped
            await db.GetTable<GuildConfig>()
                .Where(g => g.GuildId == guildId)
                .UpdateAsync(g => new GuildConfig
                {
                    WizardSkipped = true, WizardCompletedAt = DateTime.UtcNow, WizardCompletedByUserId = userId
                });

            // Update user to mark as having completed any wizard (even though skipped)
            var user = await db.GetTable<DiscordUser>().FirstOrDefaultAsync(u => u.UserId == userId);
            if (user != null)
            {
                // Add this guild to completed list
                var completedGuilds = DeserializeCompletedGuilds(user.WizardCompletedGuilds);
                if (!completedGuilds.Contains(guildId))
                {
                    completedGuilds.Add(guildId);
                }

                var newExperienceLevel = user.DashboardExperienceLevel < 1 ? 1 : user.DashboardExperienceLevel;
                var serializedGuilds = JsonSerializer.Serialize(completedGuilds);

                await db.GetTable<DiscordUser>()
                    .Where(u => u.UserId == userId)
                    .UpdateAsync(u => new DiscordUser
                    {
                        HasCompletedAnyWizard = true,
                        WizardCompletedGuilds = serializedGuilds,
                        DashboardExperienceLevel = newExperienceLevel
                    });
            }

            cache.Remove($"wizard_basic_setup_{guildId}");
            guildSettings.ClearCacheForGuild(guildId);

            logger.LogInformation("Wizard skipped for guild {GuildId} by user {UserId}", guildId, userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error skipping wizard for guild {GuildId}", guildId);
            throw;
        }
    }

    /// <summary>
    ///     Clears the guild's wizard completion flags and, when a user is supplied, removes
    ///     the guild from that user's completed list.
    /// </summary>
    /// <param name="guildId">The guild to reset.</param>
    /// <param name="userId">Optional user whose completed-guild list should also be updated.</param>
    public async Task ResetWizardAsync(ulong guildId, ulong? userId = null)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            await db.GetTable<GuildConfig>()
                .Where(g => g.GuildId == guildId)
                .UpdateAsync(g => new GuildConfig
                {
                    WizardCompleted = false,
                    WizardSkipped = false,
                    WizardCompletedAt = null,
                    WizardCompletedByUserId = 0
                });

            if (userId.HasValue)
            {
                var user = await db.GetTable<DiscordUser>()
                    .FirstOrDefaultAsync(u => u.UserId == userId.Value);

                if (user != null)
                {
                    var completedGuilds = DeserializeCompletedGuilds(user.WizardCompletedGuilds);
                    if (completedGuilds.Remove(guildId))
                    {
                        var serializedGuilds = JsonSerializer.Serialize(completedGuilds);
                        var hasCompletedAnyWizard = completedGuilds.Count > 0;

                        await db.GetTable<DiscordUser>()
                            .Where(u => u.UserId == userId.Value)
                            .UpdateAsync(u => new DiscordUser
                            {
                                WizardCompletedGuilds = serializedGuilds, HasCompletedAnyWizard = hasCompletedAnyWizard
                            });
                    }
                }
            }

            cache.Remove($"wizard_basic_setup_{guildId}");
            guildSettings.ClearCacheForGuild(guildId);

            logger.LogInformation("Wizard reset for guild {GuildId} (user {UserId})", guildId, userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resetting wizard for guild {GuildId}", guildId);
            throw;
        }
    }

    /// <summary>
    ///     Reads a user's completed-guild list, accepting both the JSON and the legacy
    ///     comma-separated encoding.
    /// </summary>
    /// <param name="raw">The stored column value.</param>
    /// <returns>The guild IDs the user has completed the wizard for.</returns>
    public static List<ulong> DeserializeCompletedGuilds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var trimmed = raw.Trim();

        if (trimmed.StartsWith('['))
        {
            try
            {
                return JsonSerializer.Deserialize<List<ulong>>(trimmed) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        return trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => ulong.TryParse(entry, out var parsed) ? parsed : 0UL)
            .Where(id => id != 0)
            .ToList();
    }

    /// <summary>
    ///     Updates user experience level based on actions
    /// </summary>
    public async Task UpdateUserExperienceAsync(ulong userId, UserAction action)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            var user = await db.GetTable<DiscordUser>().FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return;

            var newLevel = user.DashboardExperienceLevel;

            switch (action)
            {
                case UserAction.CompletedFirstWizard:
                    newLevel = Math.Max(newLevel, 1);
                    break;

                case UserAction.ConfiguredMultipleFeatures:
                    newLevel = Math.Max(newLevel, 2);
                    break;

                case UserAction.UsedAdvancedFeatures:
                    newLevel = 3;
                    break;
            }

            if (newLevel != user.DashboardExperienceLevel)
            {
                await db.GetTable<DiscordUser>()
                    .Where(u => u.UserId == userId)
                    .UpdateAsync(u => new DiscordUser
                    {
                        DashboardExperienceLevel = newLevel
                    });

                logger.LogDebug("Updated user {UserId} experience level from {OldLevel} to {NewLevel}",
                    userId, user.DashboardExperienceLevel, newLevel);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating user experience for {UserId}", userId);
        }
    }

    /// <summary>
    ///     Updates user wizard preferences
    /// </summary>
    public async Task UpdateUserPreferencesAsync(ulong userId, bool prefersGuidedSetup)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            await db.GetTable<DiscordUser>()
                .Where(u => u.UserId == userId)
                .UpdateAsync(u => new DiscordUser
                {
                    PrefersGuidedSetup = prefersGuidedSetup
                });

            logger.LogDebug("Updated wizard preferences for user {UserId}: PrefersGuided={PrefersGuided}",
                userId, prefersGuidedSetup);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating wizard preferences for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    ///     Determines if this instance is the master instance for coordination
    /// </summary>
    public bool IsMasterInstance()
    {
        // For now, simple logic - can be enhanced later
        // Master instance could be determined by:
        // 1. Designated master flag
        // 2. Lowest instance ID
        // 3. First available instance
        return true; // Default to true for now - all instances can handle wizard operations
    }

    /// <summary>
    ///     Quick check if guild has any basic configuration
    /// </summary>
    private async Task<bool> CheckHasBasicSetup(ulong guildId)
    {
        try
        {
            var cacheKey = $"wizard_basic_setup_{guildId}";
            if (cache.TryGetValue(cacheKey, out bool cachedResult))
            {
                return cachedResult;
            }

            await using var db = await dbFactory.CreateConnectionAsync();

            // Check if guild has any of the essential configurations
            var hasGreeting = await db.GetTable<MultiGreet>().AnyAsync(mg => mg.GuildId == guildId);
            var hasModeration = await db.GetTable<GuildConfig>()
                .AnyAsync(gc => gc.GuildId == guildId &&
                                (gc.FilterWords || gc.FilterInvites || gc.FilterLinks));
            var hasXp = await db.GetTable<GuildXpSetting>().AnyAsync(xp => xp.GuildId == guildId);
            var hasStarboard = await db.GetTable<Starboard>().AnyAsync(sb => sb.GuildId == guildId);

            var hasBasicSetup = hasGreeting || hasModeration || hasXp || hasStarboard;

            // Cache the result
            cache.Set(cacheKey, hasBasicSetup, cacheExpiration);

            // Update the guild config if setup detected
            if (hasBasicSetup)
            {
                await db.GetTable<GuildConfig>()
                    .Where(g => g.GuildId == guildId)
                    .UpdateAsync(g => new GuildConfig
                    {
                        HasBasicSetup = true
                    });
            }

            return hasBasicSetup;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking basic setup for guild {GuildId}", guildId);
            return false;
        }
    }
}

/// <summary>
///     Decision result for whether to show wizard
/// </summary>
public class WizardDecision
{
    /// <summary>
    ///     Whether to show the wizard
    /// </summary>
    public bool ShowWizard { get; set; }

    /// <summary>
    ///     Whether to show a setup suggestion instead
    /// </summary>
    public bool ShowSuggestion { get; set; }

    /// <summary>
    ///     Type of wizard to show
    /// </summary>
    public WizardType WizardType { get; set; }

    /// <summary>
    ///     Reason for the decision
    /// </summary>
    public string Reason { get; set; } = "";
}

/// <summary>
///     Current wizard state for a guild
/// </summary>
public class WizardState
{
    /// <summary>
    ///     Guild ID
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Whether wizard is completed
    /// </summary>
    public bool Completed { get; set; }

    /// <summary>
    ///     Whether wizard was skipped
    /// </summary>
    public bool Skipped { get; set; }

    /// <summary>
    ///     When wizard was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    ///     User who completed the wizard
    /// </summary>
    public ulong CompletedByUserId { get; set; }

    /// <summary>
    ///     Whether guild has basic setup
    /// </summary>
    public bool HasBasicSetup { get; set; }
}

/// <summary>
///     Types of wizard flows
/// </summary>
public enum WizardType
{
    /// <summary>
    ///     No wizard
    /// </summary>
    None,

    /// <summary>
    ///     First-time user wizard with full explanations
    /// </summary>
    FirstTime,

    /// <summary>
    ///     Quick setup wizard for experienced users
    /// </summary>
    QuickSetup
}

/// <summary>
///     User actions that affect experience level
/// </summary>
public enum UserAction
{
    /// <summary>
    ///     User completed their first wizard
    /// </summary>
    CompletedFirstWizard,

    /// <summary>
    ///     User configured multiple features
    /// </summary>
    ConfiguredMultipleFeatures,

    /// <summary>
    ///     User used advanced features
    /// </summary>
    UsedAdvancedFeatures
}