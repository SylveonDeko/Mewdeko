using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Modules.Xp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing XP-related functionality for the bot
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class XpController(
    XpService xp,
    DiscordShardedClient client,
    IDataConnectionFactory dbFactory,
    ILogger<XpController> logger,
    IServiceProvider serviceProvider,
    XpRewardManager xpRewardManager,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Gets the XP settings for a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>The XP settings for the guild</returns>
    [HttpGet("settings")]
    public async Task<IActionResult> GetXpSettings(ulong guildId)
    {
        var settings = await xp.GetGuildXpSettingsAsync(guildId);
        return Ok(settings);
    }

    /// <summary>
    ///     Updates XP settings for a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="settings">XP settings to update</param>
    /// <returns>The updated XP settings</returns>
    [HttpPost("settings")]
    public async Task<IActionResult> UpdateXpSettings(ulong guildId, [FromBody] GuildXpSetting settings)
    {
        if (settings.GuildId != guildId)
            return BadRequest("Guild ID mismatch");

        var currentSettings = await xp.GetGuildXpSettingsAsync(guildId);
        var curveTypeChanged = currentSettings.XpCurveType != settings.XpCurveType;
        auditContext.RecordBefore(currentSettings);

        var updatedSettings = await xp.UpdateGuildXpSettingsAsync(guildId, s =>
        {
            if (settings.XpPerMessage > 0) s.XpPerMessage = settings.XpPerMessage;
            if (settings.MessageXpCooldown > 0) s.MessageXpCooldown = settings.MessageXpCooldown;
            if (settings.VoiceXpPerMinute > 0) s.VoiceXpPerMinute = settings.VoiceXpPerMinute;
            if (settings.VoiceXpTimeout > 0) s.VoiceXpTimeout = settings.VoiceXpTimeout;
            if (settings.XpMultiplier > 0) s.XpMultiplier = settings.XpMultiplier;
            s.XpCurveType = settings.XpCurveType;
            s.CustomXpImageUrl = settings.CustomXpImageUrl;
            s.XpGainDisabled = settings.XpGainDisabled;
        });

        if (curveTypeChanged)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await xp.RecomputeAllLevelsAsync(guildId, (XpCurveType)settings.XpCurveType);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error recomputing levels for guild {GuildId}", guildId);
                }
            });
        }

        auditContext.RecordAfter(updatedSettings);
        return Ok(updatedSettings);
    }

    /// <summary>
    ///     Gets XP stats for a user in a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>The user's XP stats</returns>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserStats(ulong guildId, ulong userId)
    {
        var stats = await xp.GetUserXpStatsAsync(guildId, userId);
        if (stats == null)
            return NotFound();

        var guild = client.GetGuild(guildId);
        var user = guild?.GetUser(userId);

        var result = new
        {
            stats.UserId,
            stats.GuildId,
            stats.TotalXp,
            stats.Level,
            stats.LevelXp,
            stats.RequiredXp,
            stats.Rank,
            stats.BonusXp,
            Username = user?.Username ?? "Unknown",
            AvatarUrl = user?.GetAvatarUrl() ?? user?.GetDefaultAvatarUrl(),
            TimeOnLevel = await xp.GetTimeOnCurrentLevelAsync(guildId, userId)
        };

        return Ok(result);
    }

    /// <summary>
    ///     Adds XP to a user in a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="amount">Amount of XP to add</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpPost("user/{userId}/add")]
    public async Task<IActionResult> AddUserXp(ulong guildId, ulong userId, [FromBody] int amount)
    {
        if (amount <= 0)
            return BadRequest("Amount must be positive");

        auditContext.RecordBefore(await xp.GetUserXpStatsAsync(guildId, userId));
        await xp.AddXpAsync(guildId, userId, amount);
        auditContext.RecordAfter(await xp.GetUserXpStatsAsync(guildId, userId));
        return Ok();
    }

    /// <summary>
    ///     Resets a user's XP in a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="resetBonusXp">Whether to reset bonus XP as well</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpPost("user/{userId}/reset")]
    public async Task<IActionResult> ResetUserXp(ulong guildId, ulong userId, [FromBody] bool resetBonusXp = false)
    {
        auditContext.RecordBefore(await xp.GetUserXpStatsAsync(guildId, userId));
        await xp.ResetUserXpAsync(guildId, userId, resetBonusXp);
        return Ok();
    }

    /// <summary>
    ///     Sets a user's XP to a specific amount
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="amount">Amount of XP to set</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpPost("user/{userId}/set")]
    public async Task<IActionResult> SetUserXp(ulong guildId, ulong userId, [FromBody] long amount)
    {
        if (amount < 0)
            return BadRequest("Amount cannot be negative");

        auditContext.RecordBefore(await xp.GetUserXpStatsAsync(guildId, userId));
        await xp.SetUserXpAsync(guildId, userId, amount);
        auditContext.RecordAfter(await xp.GetUserXpStatsAsync(guildId, userId));
        return Ok();
    }

    /// <summary>
    ///     Gets the XP leaderboard for a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="page">Page number (1-indexed)</param>
    /// <param name="pageSize">Number of entries per page</param>
    /// <returns>A page of the XP leaderboard</returns>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard(ulong guildId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1)
            return BadRequest("Page must be at least 1");

        if (pageSize is < 1 or > 100)
            return BadRequest("Page size must be between 1 and 100");

        var leaderboard = await xp.GetLeaderboardAsync(guildId, page, pageSize);

        // Create a new collection with the enriched data
        var enrichedLeaderboard = new List<object>();
        var guild = client.GetGuild(guildId);

        foreach (var entry in leaderboard.Users)
        {
            var username = "Unknown";
            string avatarUrl = null;

            if (guild != null)
            {
                var user = guild.GetUser(entry.UserId);
                if (user != null)
                {
                    username = user.Username;
                    avatarUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl();
                }
            }

            enrichedLeaderboard.Add(new
            {
                entry.UserId,
                entry.GuildId,
                entry.TotalXp,
                entry.Level,
                entry.LevelXp,
                entry.RequiredXp,
                entry.Rank,
                entry.BonusXp,
                Username = username,
                AvatarUrl = avatarUrl
            });
        }

        return Ok(enrichedLeaderboard);
    }

    /// <summary>
    ///     Gets role rewards for a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>List of role rewards</returns>
    [HttpGet("rewards/roles")]
    public async Task<IActionResult> GetRoleRewards(ulong guildId)
    {
        var rewards = await xp.GetRoleRewardsAsync(guildId);

        var enrichedRewards = new List<object>();
        var guild = client.GetGuild(guildId);

        foreach (var reward in rewards)
        {
            string roleName = null;

            if (guild != null)
            {
                var role = guild.GetRole(reward.RoleId);
                if (role != null)
                {
                    roleName = role.Name;
                }
            }

            enrichedRewards.Add(new
            {
                reward.Id,
                reward.GuildId,
                reward.Level,
                reward.RoleId,
                RoleName = roleName ?? $"Role ID: {reward.RoleId}"
            });
        }

        return Ok(enrichedRewards);
    }

    /// <summary>
    ///     Adds a role reward for a specific level
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="reward">A <see cref="XpRoleReward" /> object.</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpPost("rewards/roles")]
    public async Task<IActionResult> AddRoleReward(ulong guildId, [FromBody] XpRoleReward reward)
    {
        if (reward.Level < 1)
            return BadRequest("Level must be at least a positive integer");

        auditContext.RecordBefore(await xp.GetRoleRewardsAsync(guildId));
        await xpRewardManager.SetRoleRewardAsync(guildId, reward.Level, reward.RoleId);
        auditContext.RecordAfter(await xp.GetRoleRewardsAsync(guildId));
        return Ok();
    }

    /// <summary>
    ///     Removes a role reward
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="rewardId">The reward ID to remove</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpDelete("rewards/roles/{rewardId}")]
    public async Task<IActionResult> RemoveRoleReward(ulong guildId, int rewardId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        // Check if the reward exists
        var exists = await db.XpRoleRewards
            .AnyAsync(r => r.Id == rewardId && r.GuildId == guildId);

        if (!exists)
            return NotFound();

        auditContext.RecordBefore(await db.XpRoleRewards
            .FirstOrDefaultAsync(r => r.Id == rewardId && r.GuildId == guildId));

        await db.XpRoleRewards
            .Where(r => r.Id == rewardId && r.GuildId == guildId)
            .DeleteAsync();

        return Ok();
    }

    /// <summary>
    ///     Gets currency rewards for a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>List of currency rewards</returns>
    [HttpGet("rewards/currency")]
    public async Task<IActionResult> GetCurrencyRewards(ulong guildId)
    {
        var rewards = await xp.GetCurrencyRewardsAsync(guildId);
        return Ok(rewards);
    }

    /// <summary>
    ///     Adds a currency reward for a specific level
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="reward">The currency reward to add</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpPost("rewards/currency")]
    public async Task<IActionResult> AddCurrencyReward(ulong guildId, [FromBody] XpCurrencyReward reward)
    {
        if (reward.Level < 1)
            return BadRequest("Level must be at least a positive integer");

        if (reward.Amount <= 0)
            return BadRequest("Amount must be positive");

        auditContext.RecordBefore(await xp.GetCurrencyRewardsAsync(guildId));
        await xpRewardManager.SetCurrencyRewardAsync(guildId, reward.Level, reward.Amount);
        auditContext.RecordAfter(await xp.GetCurrencyRewardsAsync(guildId));
        return Ok();
    }

    /// <summary>
    ///     Removes a currency reward
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="rewardId">The reward ID to remove</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpDelete("rewards/currency/{rewardId}")]
    public async Task<IActionResult> RemoveCurrencyReward(ulong guildId, int rewardId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        // Check if the reward exists
        var exists = await db.XpCurrencyRewards
            .AnyAsync(r => r.Id == rewardId && r.GuildId == guildId);

        if (!exists)
            return NotFound();

        auditContext.RecordBefore(await db.XpCurrencyRewards
            .FirstOrDefaultAsync(r => r.Id == rewardId && r.GuildId == guildId));

        await db.XpCurrencyRewards
            .Where(r => r.Id == rewardId && r.GuildId == guildId)
            .DeleteAsync();

        return Ok();
    }

    /// <summary>
    ///     Gets excluded channels for XP gain
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>List of excluded channel IDs</returns>
    [HttpGet("excluded/channels")]
    public async Task<IActionResult> GetExcludedChannels(ulong guildId)
    {
        var channels = await xp.GetExcludedItemsAsync(guildId, ExcludedItemType.Channel);
        return Ok(channels);
    }

    /// <summary>
    ///     Excludes a channel from XP gain
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="channelId">The channel ID to exclude</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpPost("excluded/channels")]
    public async Task<IActionResult> ExcludeChannel(ulong guildId, [FromBody] ulong channelId)
    {
        await xp.ExcludeItemAsync(guildId, channelId, ExcludedItemType.Channel);
        return Ok();
    }

    /// <summary>
    ///     Includes a previously excluded channel for XP gain
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="channelId">The channel ID to include</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpDelete("excluded/channels/{channelId}")]
    public async Task<IActionResult> IncludeChannel(ulong guildId, ulong channelId)
    {
        await xp.IncludeItemAsync(guildId, channelId, ExcludedItemType.Channel);
        return Ok();
    }

    /// <summary>
    ///     Gets excluded roles for XP gain
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>List of excluded role IDs</returns>
    [HttpGet("excluded/roles")]
    public async Task<IActionResult> GetExcludedRoles(ulong guildId)
    {
        var roles = await xp.GetExcludedItemsAsync(guildId, ExcludedItemType.Role);
        return Ok(roles);
    }

    /// <summary>
    ///     Excludes a role from XP gain
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="roleId">The role ID to exclude</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpPost("excluded/roles")]
    public async Task<IActionResult> ExcludeRole(ulong guildId, [FromBody] ulong roleId)
    {
        await xp.ExcludeItemAsync(guildId, roleId, ExcludedItemType.Role);
        return Ok();
    }

    /// <summary>
    ///     Includes a previously excluded role for XP gain
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="roleId">The role ID to include</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpDelete("excluded/roles/{roleId}")]
    public async Task<IActionResult> IncludeRole(ulong guildId, ulong roleId)
    {
        await xp.IncludeItemAsync(guildId, roleId, ExcludedItemType.Role);
        return Ok();
    }

    /// <summary>
    ///     Gets the XP template for a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>The template configuration</returns>
    [HttpGet("template")]
    public async Task<IActionResult> GetTemplate(ulong guildId)
    {
        var cardGenerator = serviceProvider.GetRequiredService<XpCardGenerator>();
        var template = await cardGenerator.GetTemplateAsync(guildId);
        return Ok(template);
    }

    /// <summary>
    ///     Updates the XP template for a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="template">The template to update</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpPost("template")]
    public async Task<IActionResult> UpdateTemplate(ulong guildId, [FromBody] Template template)
    {
        if (template.GuildId != guildId)
            return BadRequest("Guild ID mismatch");

        await using var db = await dbFactory.CreateConnectionAsync();

        // Load the complete existing template with all related entities
        var existingTemplate = await db.Templates
            .LoadWithAsTable(t => t.TemplateUser)
            .LoadWithAsTable(t => t.TemplateBar)
            .LoadWithAsTable(t => t.TemplateClub)
            .LoadWithAsTable(t => t.TemplateGuild)
            .FirstOrDefaultAsync(t => t.Id == template.Id && t.GuildId == guildId);

        if (existingTemplate == null)
            return NotFound();

        auditContext.RecordBefore(existingTemplate);

        try
        {
            // Update main template properties
            existingTemplate.OutputSizeX = template.OutputSizeX;
            existingTemplate.OutputSizeY = template.OutputSizeY;
            existingTemplate.TimeOnLevelFormat = template.TimeOnLevelFormat;
            existingTemplate.TimeOnLevelX = template.TimeOnLevelX;
            existingTemplate.TimeOnLevelY = template.TimeOnLevelY;
            existingTemplate.TimeOnLevelFontSize = template.TimeOnLevelFontSize;
            existingTemplate.TimeOnLevelColor = template.TimeOnLevelColor;
            existingTemplate.ShowTimeOnLevel = template.ShowTimeOnLevel;
            existingTemplate.AwardedX = template.AwardedX;
            existingTemplate.AwardedY = template.AwardedY;
            existingTemplate.AwardedFontSize = template.AwardedFontSize;
            existingTemplate.AwardedColor = template.AwardedColor;
            existingTemplate.ShowAwarded = template.ShowAwarded;

            // Update Template User properties
            if (template.TemplateUser != null)
            {
                // Update properties but maintain the ID
                var id = existingTemplate.TemplateUser.Id;
                template.TemplateUser.Id = id;

                // Copy all properties from the incoming TemplateUser
                existingTemplate.TemplateUser.FontSize = template.TemplateUser.FontSize;
                existingTemplate.TemplateUser.IconSizeX = template.TemplateUser.IconSizeX;
                existingTemplate.TemplateUser.IconSizeY = template.TemplateUser.IconSizeY;
                existingTemplate.TemplateUser.IconX = template.TemplateUser.IconX;
                existingTemplate.TemplateUser.IconY = template.TemplateUser.IconY;
                existingTemplate.TemplateUser.ShowIcon = template.TemplateUser.ShowIcon;
                existingTemplate.TemplateUser.ShowText = template.TemplateUser.ShowText;
                existingTemplate.TemplateUser.TextColor = template.TemplateUser.TextColor;
                existingTemplate.TemplateUser.TextX = template.TemplateUser.TextX;
                existingTemplate.TemplateUser.TextY = template.TemplateUser.TextY;

                await db.UpdateAsync(existingTemplate.TemplateUser);
            }

            // Update Template Bar properties
            if (template.TemplateBar != null)
            {
                // Update properties but maintain the ID
                var id = existingTemplate.TemplateBar.Id;
                template.TemplateBar.Id = id;

                // Copy all properties from incoming TemplateBar
                existingTemplate.TemplateBar.BarColor = template.TemplateBar.BarColor;
                existingTemplate.TemplateBar.BarDirection = template.TemplateBar.BarDirection;
                existingTemplate.TemplateBar.BarLength = template.TemplateBar.BarLength;
                existingTemplate.TemplateBar.BarPointAx = template.TemplateBar.BarPointAx;
                existingTemplate.TemplateBar.BarPointAy = template.TemplateBar.BarPointAy;
                existingTemplate.TemplateBar.BarPointBx = template.TemplateBar.BarPointBx;
                existingTemplate.TemplateBar.BarPointBy = template.TemplateBar.BarPointBy;
                existingTemplate.TemplateBar.BarTransparency = template.TemplateBar.BarTransparency;
                existingTemplate.TemplateBar.ShowBar = template.TemplateBar.ShowBar;

                await db.UpdateAsync(existingTemplate.TemplateBar);
            }

            // Update Template Guild properties
            if (template.TemplateGuild != null)
            {
                // Update properties but maintain the ID
                var id = existingTemplate.TemplateGuild.Id;
                template.TemplateGuild.Id = id;

                // Copy all properties from the incoming TemplateGuild
                existingTemplate.TemplateGuild.GuildLevelColor = template.TemplateGuild.GuildLevelColor;
                existingTemplate.TemplateGuild.GuildLevelFontSize = template.TemplateGuild.GuildLevelFontSize;
                existingTemplate.TemplateGuild.GuildLevelX = template.TemplateGuild.GuildLevelX;
                existingTemplate.TemplateGuild.GuildLevelY = template.TemplateGuild.GuildLevelY;
                existingTemplate.TemplateGuild.GuildRankColor = template.TemplateGuild.GuildRankColor;
                existingTemplate.TemplateGuild.GuildRankFontSize = template.TemplateGuild.GuildRankFontSize;
                existingTemplate.TemplateGuild.GuildRankX = template.TemplateGuild.GuildRankX;
                existingTemplate.TemplateGuild.GuildRankY = template.TemplateGuild.GuildRankY;
                existingTemplate.TemplateGuild.ShowGuildLevel = template.TemplateGuild.ShowGuildLevel;
                existingTemplate.TemplateGuild.ShowGuildRank = template.TemplateGuild.ShowGuildRank;

                await db.UpdateAsync(existingTemplate.TemplateGuild);
            }

            // Update Template Club properties
            if (template.TemplateClub != null)
            {
                // Update properties but maintain the ID
                var id = existingTemplate.TemplateClub.Id;
                template.TemplateClub.Id = id;

                // Copy all properties from the incoming TemplateClub
                existingTemplate.TemplateClub.ClubIconSizeX = template.TemplateClub.ClubIconSizeX;
                existingTemplate.TemplateClub.ClubIconSizeY = template.TemplateClub.ClubIconSizeY;
                existingTemplate.TemplateClub.ClubIconX = template.TemplateClub.ClubIconX;
                existingTemplate.TemplateClub.ClubIconY = template.TemplateClub.ClubIconY;
                existingTemplate.TemplateClub.ClubNameColor = template.TemplateClub.ClubNameColor;
                existingTemplate.TemplateClub.ClubNameFontSize = template.TemplateClub.ClubNameFontSize;
                existingTemplate.TemplateClub.ClubNameX = template.TemplateClub.ClubNameX;
                existingTemplate.TemplateClub.ClubNameY = template.TemplateClub.ClubNameY;
                existingTemplate.TemplateClub.ShowClubIcon = template.TemplateClub.ShowClubIcon;
                existingTemplate.TemplateClub.ShowClubName = template.TemplateClub.ShowClubName;

                await db.UpdateAsync(existingTemplate.TemplateClub);
            }

            // Update the main template
            await db.UpdateAsync(existingTemplate);

            auditContext.RecordAfter(existingTemplate);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error updating template: {ex.Message}");
        }
    }

    /// <summary>
    ///     Gets server XP statistics
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>Server XP statistics</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetServerStats(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get settings first for level calculation
        var settings = await xp.GetGuildXpSettingsAsync(guildId);

        // Use separate queries for count and sum
        var totalUsers = await db.GuildUserXps
            .CountAsync(x => x.GuildId == guildId);

        var totalXp = await db.GuildUserXps
            .Where(x => x.GuildId == guildId)
            .SumAsync(x => x.TotalXp);

        // Get highest level and data for average level calculation
        var highestLevel = 0;
        var averageLevel = 0.0;

        if (totalUsers > 0)
        {
            // Get all XP values for level calculation - only fetch the necessary field
            var allXp = await db.GuildUserXps
                .Where(x => x.GuildId == guildId)
                .Select(x => x.TotalXp)
                .ToListAsync();

            // Calculate levels
            var levels = allXp.Select(l => XpCalculator.CalculateLevel(l, (XpCurveType)settings.XpCurveType)).ToList();
            highestLevel = levels.Count > 0 ? levels.Max() : 0;
            averageLevel = levels.Count > 0 ? levels.Average() : 0;
        }

        // Get recent activity with a single query
        var recentGains = await db.GuildUserXps
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.LastActivity)
            .Take(10)
            .Select(x => new
            {
                x.UserId, x.LastActivity
            })
            .ToListAsync();

        var guild = client.GetGuild(guildId);
        var recentGainsDetails = recentGains.Select(x =>
        {
            var user = guild?.GetUser(x.UserId);
            return new
            {
                x.UserId,
                Username = user?.Username ?? "Unknown",
                AvatarUrl = user?.GetAvatarUrl() ?? user?.GetDefaultAvatarUrl(),
                Timestamp = x.LastActivity
            };
        }).ToList();

        return Ok(new
        {
            TotalUsers = totalUsers,
            TotalXp = totalXp,
            AverageLevel = Math.Round(averageLevel, 1),
            HighestLevel = highestLevel,
            RecentActivity = recentGainsDetails
        });
    }
}