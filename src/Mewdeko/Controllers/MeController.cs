using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Controllers.Common.UserSettings;
using Mewdeko.Modules.Afk.Services;
using Mewdeko.Modules.Birthday.Services;
using Mewdeko.Modules.Highlights.Services;
using Mewdeko.Modules.Reputation.Services;
using Mewdeko.Modules.Starboard.Services;
using Mewdeko.Modules.Suggestions.Services;
using Mewdeko.Modules.UserProfile.Services;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Modules.Xp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for user-facing endpoints using standard API key auth
/// </summary>
[ApiController]
[Route("botapi/me/{guildId}/{userId}")]
[Authorize("ApiKeyPolicy")]
public class MeController(
    HighlightsService highlightsService,
    AfkService afkService,
    RepService repService,
    XpService xpService,
    BirthdayService birthdayService,
    UserProfileService userProfileService,
    SuggestionsService suggestionsService,
    StarboardService starboardService,
    InviteCountService inviteCountService,
    MessageCountService messageCountService,
    DiscordShardedClient client,
    IDataConnectionFactory dbFactory,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Validates that the user is a member of the specified guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>True if user is a member, false otherwise</returns>
    private async Task<bool> ValidateUserMembership(ulong guildId, ulong userId)
    {
        await Task.CompletedTask;
        var guild = client.GetGuild(guildId);
        if (guild == null) return false;

        var user = guild.GetUser(userId);
        return user != null;
    }

    /// <summary>
    ///     Gets all highlight words for a user in a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>List of highlight words</returns>
    [HttpGet("highlights")]
    public async Task<IActionResult> GetHighlights(ulong guildId, ulong userId)
    {
        if (!await ValidateUserMembership(guildId, userId))
            return StatusCode(403, "User is not a member of this guild");

        await using var db = await dbFactory.CreateConnectionAsync();
        var highlights = await db.Highlights
            .Where(h => h.GuildId == guildId && h.UserId == userId)
            .Select(h => new
            {
                h.Id, h.Word, h.DateAdded
            })
            .ToListAsync();

        return Ok(highlights);
    }

    /// <summary>
    ///     Adds a highlight word for a user in a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="word">The word to highlight</param>
    /// <returns>The created highlight</returns>
    [HttpPost("highlights")]
    public async Task<IActionResult> AddHighlight(ulong guildId, ulong userId, [FromBody] string word)
    {
        if (!await ValidateUserMembership(guildId, userId))
            return StatusCode(403, "User is not a member of this guild");

        if (string.IsNullOrWhiteSpace(word))
            return BadRequest("Word cannot be empty");

        await using var db = await dbFactory.CreateConnectionAsync();

        var exists = await db.Highlights
            .AnyAsync(h => h.GuildId == guildId && h.UserId == userId && h.Word == word);

        if (exists)
            return Conflict("Highlight word already exists");

        await highlightsService.AddHighlight(guildId, userId, word);

        return Ok(new
        {
            word, dateAdded = DateTime.UtcNow
        });
    }

    /// <summary>
    ///     Removes a highlight word for a user
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="highlightId">The highlight ID to remove</param>
    /// <returns>Success response</returns>
    [HttpDelete("highlights/{highlightId:int}")]
    public async Task<IActionResult> RemoveHighlight(ulong guildId, ulong userId, int highlightId)
    {
        if (!await ValidateUserMembership(guildId, userId))
            return StatusCode(403, "User is not a member of this guild");

        await using var db = await dbFactory.CreateConnectionAsync();

        var highlight = await db.Highlights
            .FirstOrDefaultAsync(h => h.Id == highlightId && h.GuildId == guildId && h.UserId == userId);

        if (highlight == null)
            return NotFound("Highlight not found");

        auditContext.RecordBefore(highlight);
        await highlightsService.RemoveHighlight(highlight);
        return Ok();
    }

    /// <summary>
    ///     Gets highlight settings for a user in a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>Highlight settings</returns>
    [HttpGet("highlights/settings")]
    public async Task<IActionResult> GetHighlightSettings(ulong guildId, ulong userId)
    {
        if (!await ValidateUserMembership(guildId, userId))
            return StatusCode(403, "User is not a member of this guild");

        await using var db = await dbFactory.CreateConnectionAsync();
        var settings = await db.HighlightSettings
            .FirstOrDefaultAsync(h => h.GuildId == guildId && h.UserId == userId);

        return Ok(new
        {
            highlightsEnabled = settings?.HighlightsOn ?? true,
            ignoredChannels = settings?.IgnoredChannels?.Split(' ').Where(c => c != "0").ToList() ?? new List<string>(),
            ignoredUsers = settings?.IgnoredUsers?.Split(' ').Where(u => u != "0").ToList() ?? new List<string>()
        });
    }

    /// <summary>
    ///     Updates highlight settings for a user in a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="request">The settings update request</param>
    /// <returns>Success response</returns>
    [HttpPut("highlights/settings")]
    public async Task<IActionResult> UpdateHighlightSettings(ulong guildId, ulong userId,
        [FromBody] HighlightSettingsRequest request)
    {
        if (!await ValidateUserMembership(guildId, userId))
            return StatusCode(403, "User is not a member of this guild");

        await highlightsService.ToggleHighlights(guildId, userId, request.HighlightsEnabled);

        return Ok();
    }

    /// <summary>
    ///     Gets AFK status for a user in a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>AFK status</returns>
    [HttpGet("afk")]
    public async Task<IActionResult> GetAfkStatus(ulong guildId, ulong userId)
    {
        if (!await ValidateUserMembership(guildId, userId))
            return StatusCode(403, "User is not a member of this guild");

        var afkData = await afkService.GetAfk(guildId, userId);
        var isAfk = await afkService.IsAfk(guildId, userId);

        return Ok(new
        {
            isAfk, message = afkData?.Message ?? "", when = afkData?.When, wasTimed = afkData?.WasTimed ?? false
        });
    }

    /// <summary>
    ///     Sets AFK status for a user
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="request">The AFK request</param>
    /// <returns>Success response</returns>
    [HttpPost("afk")]
    public async Task<IActionResult> SetAfkStatus(ulong guildId, ulong userId, [FromBody] AfkRequest request)
    {
        if (!await ValidateUserMembership(guildId, userId))
            return StatusCode(403, "User is not a member of this guild");

        var maxLength = await afkService.GetAfkLength(guildId);
        if (!string.IsNullOrEmpty(request.Message) && request.Message.Length > maxLength)
            return BadRequest($"Message exceeds maximum length of {maxLength} characters");

        var message = string.IsNullOrEmpty(request.Message) ? "" : request.Message;
        auditContext.RecordBefore(await afkService.GetAfk(guildId, userId));
        await afkService.AfkSet(guildId, userId, message, request.IsTimed, request.Until ?? DateTime.UtcNow);
        auditContext.RecordAfter(await afkService.GetAfk(guildId, userId));

        return Ok();
    }

    /// <summary>
    ///     Removes AFK status for a user
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>Success response</returns>
    [HttpDelete("afk")]
    public async Task<IActionResult> RemoveAfkStatus(ulong guildId, ulong userId)
    {
        if (!await ValidateUserMembership(guildId, userId))
            return StatusCode(403, "User is not a member of this guild");

        auditContext.RecordBefore(await afkService.GetAfk(guildId, userId));
        await afkService.AfkSet(guildId, userId, "");
        return Ok();
    }

    /// <summary>
    ///     Gets reputation stats for a user (read-only)
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>Reputation statistics</returns>
    [HttpGet("reputation")]
    public async Task<IActionResult> GetReputation(ulong guildId, ulong userId)
    {
        if (!await ValidateUserMembership(guildId, userId))
            return StatusCode(403, "User is not a member of this guild");

        var (total, rank) = await repService.GetUserReputationAsync(guildId, userId);
        var stats = await repService.GetUserStatsAsync(guildId, userId);

        return Ok(new
        {
            totalRep = total,
            rank,
            stats.TotalGiven,
            stats.TotalReceived,
            stats.CurrentStreak,
            stats.LongestStreak,
            stats.LastGivenAt,
            stats.LastReceivedAt
        });
    }

    /// <summary>
    ///     Gets global user preferences (not guild-specific)
    /// </summary>
    /// <param name="guildId">The guild ID (for route consistency)</param>
    /// <param name="userId">The user ID</param>
    /// <returns>User preferences</returns>
    [HttpGet("preferences")]
    public async Task<IActionResult> GetUserPreferences(ulong guildId, ulong userId)
    {
        // For global preferences, we don't need guild membership validation
        // but we keep the route structure for consistency

        var userPrefs = await xpService.GetUserPreferencesAsync(userId);

        await using var db = await dbFactory.CreateConnectionAsync();
        var user = await db.DiscordUsers.FirstOrDefaultAsync(u => u.UserId == userId);

        return Ok(new
        {
            levelUpPingsDisabled = user?.LevelUpPingsDisabled ?? false,
            pronounsDisabled = user?.PronounsDisabled ?? false,
            prefersGuidedSetup = user?.PrefersGuidedSetup ?? false,
            dashboardExperienceLevel = user?.DashboardExperienceLevel ?? 0,
            hasCompletedAnyWizard = user?.HasCompletedAnyWizard ?? false
        });
    }

    /// <summary>
    ///     Updates global user preferences
    /// </summary>
    /// <param name="guildId">The guild ID (for route consistency)</param>
    /// <param name="userId">The user ID</param>
    /// <param name="request">The preferences update request</param>
    /// <returns>Success response</returns>
    [HttpPut("preferences")]
    public async Task<IActionResult> UpdateUserPreferences(ulong guildId, ulong userId,
        [FromBody] UserPreferencesRequest request)
    {
        // For global preferences, we don't need guild membership validation

        await using var db = await dbFactory.CreateConnectionAsync();
        var user = await db.GetOrCreateUser(client.GetUser(userId));

        if (request.LevelUpPingsDisabled.HasValue)
            user.LevelUpPingsDisabled = request.LevelUpPingsDisabled.Value;

        if (request.PronounsDisabled.HasValue)
            user.PronounsDisabled = request.PronounsDisabled.Value;

        if (request.PrefersGuidedSetup.HasValue)
            user.PrefersGuidedSetup = request.PrefersGuidedSetup.Value;

        if (request.DashboardExperienceLevel.HasValue)
            user.DashboardExperienceLevel = request.DashboardExperienceLevel.Value;

        await db.UpdateAsync(user);

        return Ok();
    }

    /// <summary>
    ///     Gets user profile information
    /// </summary>
    /// <param name="guildId">The guild ID (for route consistency)</param>
    /// <param name="userId">The user ID</param>
    /// <returns>User profile data</returns>
    [HttpGet("profile")]
    public async Task<IActionResult> GetUserProfile(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var user = await db.DiscordUsers.FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
        {
            return Ok(new
            {
                bio = "",
                zodiacSign = "",
                profilePrivacy = 0, // Public
                birthdayDisplayMode = 0, // Default
                greetDmsOptOut = false,
                statsOptOut = false,
                birthday = (DateTime?)null,
                birthdayTimezone = "UTC",
                birthdayAnnouncementsEnabled = false,
                profileColor = (uint?)null,
                profileImageUrl = "",
                switchFriendCode = "",
                pronouns = ""
            });
        }

        return Ok(new
        {
            bio = user.Bio ?? "",
            zodiacSign = user.ZodiacSign ?? "",
            profilePrivacy = user.ProfilePrivacy,
            birthdayDisplayMode = user.BirthdayDisplayMode,
            greetDmsOptOut = user.GreetDmsOptOut,
            statsOptOut = user.StatsOptOut,
            birthday = user.Birthday,
            birthdayTimezone = user.BirthdayTimezone ?? "UTC",
            birthdayAnnouncementsEnabled = user.BirthdayAnnouncementsEnabled,
            profileColor = user.ProfileColor,
            profileImageUrl = user.ProfileImageUrl ?? "",
            switchFriendCode = user.SwitchFriendCode ?? "",
            pronouns = user.Pronouns ?? ""
        });
    }

    /// <summary>
    ///     Updates user profile settings
    /// </summary>
    /// <param name="guildId">The guild ID (for route consistency)</param>
    /// <param name="userId">The user ID</param>
    /// <param name="request">Profile update request</param>
    /// <returns>Success response</returns>
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateUserProfile(ulong guildId, ulong userId,
        [FromBody] UserProfileRequest request)
    {
        var user = client.GetUser(userId);
        if (user == null)
            return NotFound("User not found");

        // Update individual fields using existing services
        if (request.Bio != null)
            await userProfileService.SetBio(user, request.Bio);

        if (request.ZodiacSign != null)
            await userProfileService.SetZodiac(user, request.ZodiacSign);

        if (request.SwitchFriendCode != null)
            await userProfileService.SetSwitchFc(user, request.SwitchFriendCode);

        if (request.ProfileImageUrl != null)
            await userProfileService.SetProfileImage(user, request.ProfileImageUrl);

        // Update database fields directly
        await using var db = await dbFactory.CreateConnectionAsync();
        var dbUser = await db.GetOrCreateUser(user);

        if (request.ProfilePrivacy.HasValue)
            dbUser.ProfilePrivacy = request.ProfilePrivacy.Value;

        if (request.BirthdayDisplayMode.HasValue)
            dbUser.BirthdayDisplayMode = request.BirthdayDisplayMode.Value;

        if (request.GreetDmsOptOut.HasValue)
            dbUser.GreetDmsOptOut = request.GreetDmsOptOut.Value;

        if (request.StatsOptOut.HasValue)
            dbUser.StatsOptOut = request.StatsOptOut.Value;

        if (request.Birthday.HasValue)
            dbUser.Birthday = request.Birthday.Value;

        if (request.BirthdayTimezone != null)
            dbUser.BirthdayTimezone = request.BirthdayTimezone;

        if (request.BirthdayAnnouncementsEnabled.HasValue)
            dbUser.BirthdayAnnouncementsEnabled = request.BirthdayAnnouncementsEnabled.Value;

        if (request.ProfileColor.HasValue)
            dbUser.ProfileColor = request.ProfileColor.Value;

        if (request.Pronouns != null)
            dbUser.Pronouns = request.Pronouns;

        auditContext.RecordAfter(dbUser);
        await db.UpdateAsync(dbUser);

        return Ok();
    }

    /// <summary>
    ///     Gets user's suggestions in a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>User's suggestions with statuses</returns>
    [HttpGet("suggestions")]
    public async Task<IActionResult> GetMySuggestions(ulong guildId, ulong userId)
    {
        if (!await ValidateUserMembership(guildId, userId))
            return StatusCode(403, "User is not a member of this guild");

        var suggestions = await suggestionsService.ForUser(guildId, userId);
        var enrichedSuggestions = suggestions.Select(s => new
        {
            s.Id,
            s.SuggestionId,
            s.Suggestion1,
            s.CurrentState,
            StateName = Enum.GetName(typeof(SuggestionsService.SuggestState), s.CurrentState) ?? "Unknown",
            s.DateAdded,
            s.EmoteCount1,
            s.EmoteCount2,
            s.EmoteCount3,
            s.EmoteCount4,
            s.EmoteCount5
        }).OrderByDescending(s => s.DateAdded);

        return Ok(enrichedSuggestions);
    }

    /// <summary>
    ///     Gets user's currency balance and transaction history
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>Currency data</returns>
    [HttpGet("currency")]
    public async Task<IActionResult> GetMyCurrency(ulong guildId, ulong userId)
    {
        if (!await ValidateUserMembership(guildId, userId))
            return StatusCode(403, "User is not a member of this guild");

        await using var db = await dbFactory.CreateConnectionAsync();

        // Get guild balance
        var balance = await db.GuildUserBalances
            .Where(b => b.GuildId == guildId && b.UserId == userId)
            .Select(b => b.Balance)
            .FirstOrDefaultAsync();

        // Get recent transactions
        var transactions = await db.TransactionHistories
            .Where(t => t.GuildId == guildId && t.UserId == userId)
            .OrderByDescending(t => t.DateAdded)
            .Take(20)
            .Select(t => new
            {
                t.Id, t.Amount, t.Description, t.DateAdded
            })
            .ToListAsync();

        return Ok(new
        {
            balance, recentTransactions = transactions
        });
    }

    /// <summary>
    ///     Gets user's giveaway activity
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>Giveaway entries and wins</returns>
    [HttpGet("giveaways")]
    public async Task<IActionResult> GetMyGiveaways(ulong guildId, ulong userId)
    {
        if (!await ValidateUserMembership(guildId, userId))
            return StatusCode(403, "User is not a member of this guild");

        await using var db = await dbFactory.CreateConnectionAsync();

        var entries = await db.GiveawayUsers
            .Where(g => g.UserId == userId)
            .Join(db.Giveaways,
                gu => gu.GiveawayId,
                g => g.Id,
                (gu, g) => new
                {
                    gu, g
                })
            .Where(x => x.g.ServerId == guildId)
            .Select(x => new
            {
                x.g.Id,
                x.g.Item,
                WinnerCount = x.g.Winners,
                x.g.When,
                x.g.DateAdded,
                IsEnded = x.g.Ended == 1,
                EntryDate = x.gu.DateAdded,
                x.g.UserId // Giveaway creator
            })
            .OrderByDescending(x => x.When)
            .ToListAsync();

        return Ok(entries);
    }

    /// <summary>
    ///     Gets user's reminders
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>User's reminders</returns>
    [HttpGet("reminders")]
    public async Task<IActionResult> GetMyReminders(ulong guildId, ulong userId)
    {
        if (!await ValidateUserMembership(guildId, userId))
            return StatusCode(403, "User is not a member of this guild");

        await using var db = await dbFactory.CreateConnectionAsync();

        var reminders = await db.Reminders
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.When)
            .Select(r => new
            {
                r.Id,
                r.Message,
                r.When,
                r.DateAdded,
                r.ChannelId,
                r.ServerId,
                IsExpired = r.When < DateTime.UtcNow
            })
            .ToListAsync();

        return Ok(reminders);
    }

    /// <summary>
    ///     Gets global cross-server analytics for the user (not guild-specific)
    /// </summary>
    /// <param name="guildId">The guild ID (for route consistency, not used in logic)</param>
    /// <param name="userId">The user ID</param>
    /// <returns>Global cross-server user analytics</returns>
    [HttpGet("analytics")]
    public async Task<IActionResult> GetMyGlobalAnalytics(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get user's presence across all bot guilds
        var userGuilds = client.Guilds.Where(g => g.GetUser(userId) != null).ToList();

        var analytics = new
        {
            // XP across servers
            TotalServers = userGuilds.Count,
            XpData = await db.GuildUserXps
                .Where(x => x.UserId == userId)
                .Select(x => new
                {
                    x.GuildId,
                    GuildName = userGuilds.FirstOrDefault(g => g.Id == x.GuildId).Name ?? "Unknown",
                    x.TotalXp,
                    Level = XpCalculator.CalculateLevel(x.TotalXp, XpCurveType.Linear), // Default curve
                    x.LastActivity
                })
                .ToListAsync(),

            // Global currency
            GlobalBalance = await db.GlobalUserBalances
                .Where(b => b.UserId == userId)
                .Select(b => b.Balance)
                .FirstOrDefaultAsync(),

            // Total suggestions across all servers
            TotalSuggestions = await db.Suggestions
                .Where(s => s.UserId == userId && userGuilds.Select(g => g.Id).Contains(s.GuildId))
                .CountAsync(),

            // Recent activity summary
            RecentActivity = new
            {
                LastAfkSet = await db.Afks
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.DateAdded)
                    .Select(a => a.DateAdded)
                    .FirstOrDefaultAsync(),
                LastSuggestion = await db.Suggestions
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.DateAdded)
                    .Select(s => s.DateAdded)
                    .FirstOrDefaultAsync(),
                LastXpGain = await db.GuildUserXps
                    .Where(x => x.UserId == userId)
                    .OrderByDescending(x => x.LastActivity)
                    .Select(x => x.LastActivity)
                    .FirstOrDefaultAsync()
            }
        };

        return Ok(analytics);
    }

    /// <summary>
    ///     Gets user's invite statistics for a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>Invite statistics</returns>
    [HttpGet("invites")]
    public async Task<IActionResult> GetMyInvites(ulong guildId, ulong userId)
    {
        if (!await ValidateUserMembership(guildId, userId))
            return StatusCode(403, "User is not a member of this guild");

        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var inviteCount = await inviteCountService.GetInviteCount(userId, guildId);
        var invitedUsers = await inviteCountService.GetInvitedUsers(userId, guild);

        return Ok(new
        {
            inviteCount,
            invitedUsers = invitedUsers.OfType<IGuildUser>().Select(u => new
            {
                id = u.Id,
                username = u.Username,
                displayName = u.DisplayName,
                joinedAt = u.JoinedAt?.ToString() ?? "Unknown"
            }).ToList()
        });
    }

    /// <summary>
    ///     Gets user's message statistics for a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>Message statistics</returns>
    [HttpGet("messages")]
    public async Task<IActionResult> GetMyMessages(ulong guildId, ulong userId)
    {
        if (!await ValidateUserMembership(guildId, userId))
            return StatusCode(403, "User is not a member of this guild");

        var totalMessages = await messageCountService.GetMessageCount(
            MessageCountService.CountQueryType.User, guildId, userId);

        // Get detailed message counts by channel
        var (messageCounts, enabled) = await messageCountService.GetAllCountsForEntity(
            MessageCountService.CountQueryType.User, userId, guildId);

        var channelBreakdown = messageCounts.Select(mc => new
        {
            channelId = mc.ChannelId,
            channelName = client.GetGuild(guildId)?.GetChannel(mc.ChannelId)?.Name ?? "Unknown",
            count = mc.Count,
            lastActivity = mc.MessageTimestamps?.OrderByDescending(t => t.Timestamp)
                .FirstOrDefault()?.Timestamp.ToString() ?? "Unknown"
        }).OrderByDescending(c => c.count).ToList();

        return Ok(new
        {
            totalMessages, enabled, channelBreakdown
        });
    }

    /// <summary>
    ///     Gets user's starboard statistics for a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>Starboard statistics</returns>
    [HttpGet("starboard")]
    public async Task<IActionResult> GetMyStarboard(ulong guildId, ulong userId)
    {
        if (!await ValidateUserMembership(guildId, userId))
            return StatusCode(403, "User is not a member of this guild");

        var stats = await starboardService.GetUserStarboardStats(guildId, userId);

        return Ok(stats);
    }

    /// <summary>
    ///     Toggles greet DMs opt-out using the service method
    /// </summary>
    /// <param name="guildId">The guild ID (for route consistency)</param>
    /// <param name="userId">The user ID</param>
    /// <returns>New opt-out status</returns>
    [HttpPost("profile/toggle-greet-dms")]
    public async Task<IActionResult> ToggleGreetDms(ulong guildId, ulong userId)
    {
        var user = client.GetUser(userId);
        if (user == null)
            return NotFound("User not found");

        var newStatus = await userProfileService.ToggleDmGreetOptOutAsync(user);

        return Ok(new
        {
            greetDmsOptOut = newStatus
        });
    }

    /// <summary>
    ///     Toggles stats opt-out using the service method
    /// </summary>
    /// <param name="guildId">The guild ID (for route consistency)</param>
    /// <param name="userId">The user ID</param>
    /// <returns>New opt-out status</returns>
    [HttpPost("profile/toggle-stats")]
    public async Task<IActionResult> ToggleStats(ulong guildId, ulong userId)
    {
        var user = client.GetUser(userId);
        if (user == null)
            return NotFound("User not found");

        var newStatus = await userProfileService.ToggleOptOut(user);

        return Ok(new
        {
            statsOptOut = newStatus
        });
    }

    /// <summary>
    ///     Toggles birthday announcements using the service method
    /// </summary>
    /// <param name="guildId">The guild ID (for route consistency)</param>
    /// <param name="userId">The user ID</param>
    /// <returns>New announcement status</returns>
    [HttpPost("profile/toggle-birthday-announcements")]
    public async Task<IActionResult> ToggleBirthdayAnnouncements(ulong guildId, ulong userId)
    {
        var user = client.GetUser(userId);
        if (user == null)
            return NotFound("User not found");

        var newStatus = await birthdayService.ToggleBirthdayAnnouncementsAsync(user);

        return Ok(new
        {
            birthdayAnnouncementsEnabled = newStatus
        });
    }

    /// <summary>
    ///     Toggles level-up pings preference
    /// </summary>
    /// <param name="guildId">The guild ID (for route consistency)</param>
    /// <param name="userId">The user ID</param>
    /// <returns>New preference status</returns>
    [HttpPost("preferences/toggle-levelup-pings")]
    public async Task<IActionResult> ToggleLevelUpPings(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var user = await db.GetOrCreateUser(client.GetUser(userId));

        user.LevelUpPingsDisabled = !user.LevelUpPingsDisabled;
        await db.UpdateAsync(user);

        return Ok(new
        {
            levelUpPingsDisabled = user.LevelUpPingsDisabled
        });
    }

    /// <summary>
    ///     Toggles pronoun fetching preference
    /// </summary>
    /// <param name="guildId">The guild ID (for route consistency)</param>
    /// <param name="userId">The user ID</param>
    /// <returns>New preference status</returns>
    [HttpPost("preferences/toggle-pronouns")]
    public async Task<IActionResult> TogglePronouns(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var user = await db.GetOrCreateUser(client.GetUser(userId));

        user.PronounsDisabled = !user.PronounsDisabled;
        await db.UpdateAsync(user);

        return Ok(new
        {
            pronounsDisabled = user.PronounsDisabled
        });
    }

    /// <summary>
    ///     Toggles guided setup preference
    /// </summary>
    /// <param name="guildId">The guild ID (for route consistency)</param>
    /// <param name="userId">The user ID</param>
    /// <returns>New preference status</returns>
    [HttpPost("preferences/toggle-guided-setup")]
    public async Task<IActionResult> ToggleGuidedSetup(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var user = await db.GetOrCreateUser(client.GetUser(userId));

        user.PrefersGuidedSetup = !user.PrefersGuidedSetup;
        await db.UpdateAsync(user);

        return Ok(new
        {
            prefersGuidedSetup = user.PrefersGuidedSetup
        });
    }

    /// <summary>
    ///     Resets wizard completion state for a user
    /// </summary>
    /// <param name="guildId">The guild ID (for route consistency)</param>
    /// <param name="userId">The user ID</param>
    /// <returns>Success response</returns>
    [HttpPost("wizard/reset")]
    public async Task<IActionResult> ResetWizardState(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var user = await db.GetOrCreateUser(client.GetUser(userId));

        user.HasCompletedAnyWizard = false;
        user.WizardCompletedGuilds = null;
        user.PrefersGuidedSetup = true; // Reset to prefer guided setup

        await db.UpdateAsync(user);

        return Ok(new
        {
            hasCompletedAnyWizard = false, wizardCompletedGuilds = (string?)null, prefersGuidedSetup = true
        });
    }

    /// <summary>
    ///     Resets wizard completion for a specific guild
    /// </summary>
    /// <param name="guildId">The guild ID to reset wizard for</param>
    /// <param name="userId">The user ID</param>
    /// <param name="resetGuildId">The guildid to reset the wizard for.</param>
    /// <returns>Success response</returns>
    [HttpPost("wizard/reset/{resetGuildId}")]
    public async Task<IActionResult> ResetGuildWizard(ulong guildId, ulong userId, ulong resetGuildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var user = await db.GetOrCreateUser(client.GetUser(userId));

        if (!string.IsNullOrEmpty(user.WizardCompletedGuilds))
        {
            var completedGuilds = user.WizardCompletedGuilds.Split(',').ToList();
            completedGuilds.Remove(resetGuildId.ToString());

            user.WizardCompletedGuilds = completedGuilds.Count > 0 ? string.Join(",", completedGuilds) : null;
            user.HasCompletedAnyWizard = completedGuilds.Count > 0;

            await db.UpdateAsync(user);
        }

        return Ok(new
        {
            resetGuildId,
            hasCompletedAnyWizard = user.HasCompletedAnyWizard,
            wizardCompletedGuilds = user.WizardCompletedGuilds
        });
    }
}