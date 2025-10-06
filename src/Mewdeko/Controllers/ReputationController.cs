using LinqToDB.Async;
using Mewdeko.Controllers.Common.Reputation;
using Mewdeko.Modules.Reputation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API Controller for managing guild-level reputation system configuration and data.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class ReputationController(
    RepService service,
    DiscordShardedClient client,
    IDataConnectionFactory dbFactory)
    : Controller
{
    /// <summary>
    ///     Gets the reputation configuration for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>Reputation configuration</returns>
    [HttpGet("config")]
    public async Task<IActionResult> GetRepConfig(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var config = await db.RepConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (config == null)
            return Ok(new
            {
                guildId,
                enabled = true,
                defaultCooldownMinutes = 60,
                dailyLimit = 10,
                weeklyLimit = (int?)null,
                minAccountAgeDays = 7,
                minServerMembershipHours = 24,
                minMessageCount = 10,
                enableNegativeRep = false,
                enableAnonymous = false,
                enableDecay = false,
                decayType = "weekly",
                decayAmount = 1,
                decayInactiveDays = 30,
                notificationChannel = (ulong?)null
            });

        return Ok(config);
    }

    /// <summary>
    ///     Enables or disables the reputation system for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="enabled">Whether to enable the reputation system</param>
    /// <returns>Success response</returns>
    [HttpPost("enabled")]
    public async Task<IActionResult> SetEnabled(ulong guildId, [FromBody] bool enabled)
    {
        await service.SetEnabledAsync(guildId, enabled);
        return Ok(new
        {
            enabled
        });
    }

    /// <summary>
    ///     Sets the default cooldown between giving reputation
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="minutes">Cooldown in minutes</param>
    /// <returns>Success response</returns>
    [HttpPost("cooldown")]
    public async Task<IActionResult> SetDefaultCooldown(ulong guildId, [FromBody] int minutes)
    {
        if (minutes < 0)
            return BadRequest("Cooldown must be positive");

        await service.SetDefaultCooldownAsync(guildId, minutes);
        return Ok(new
        {
            cooldownMinutes = minutes
        });
    }

    /// <summary>
    ///     Sets the daily reputation limit
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="limit">Daily limit</param>
    /// <returns>Success response</returns>
    [HttpPost("dailyLimit")]
    public async Task<IActionResult> SetDailyLimit(ulong guildId, [FromBody] int limit)
    {
        if (limit < 0)
            return BadRequest("Limit must be positive");

        await service.SetDailyLimitAsync(guildId, limit);
        return Ok(new
        {
            dailyLimit = limit
        });
    }

    /// <summary>
    ///     Sets the weekly reputation limit
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="limit">Weekly limit (null to disable)</param>
    /// <returns>Success response</returns>
    [HttpPost("weeklyLimit")]
    public async Task<IActionResult> SetWeeklyLimit(ulong guildId, [FromBody] int? limit)
    {
        if (limit.HasValue && limit.Value < 0)
            return BadRequest("Limit must be positive");

        await service.SetWeeklyLimitAsync(guildId, limit);
        return Ok(new
        {
            weeklyLimit = limit
        });
    }

    /// <summary>
    ///     Sets the minimum account age requirement
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="days">Minimum account age in days</param>
    /// <returns>Success response</returns>
    [HttpPost("minAccountAge")]
    public async Task<IActionResult> SetMinAccountAge(ulong guildId, [FromBody] int days)
    {
        if (days < 0)
            return BadRequest("Days must be positive");

        await service.SetMinAccountAgeAsync(guildId, days);
        return Ok(new
        {
            minAccountAgeDays = days
        });
    }

    /// <summary>
    ///     Sets the minimum server membership requirement
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="hours">Minimum membership in hours</param>
    /// <returns>Success response</returns>
    [HttpPost("minServerMembership")]
    public async Task<IActionResult> SetMinServerMembership(ulong guildId, [FromBody] int hours)
    {
        if (hours < 0)
            return BadRequest("Hours must be positive");

        await service.SetMinServerMembershipAsync(guildId, hours);
        return Ok(new
        {
            minServerMembershipHours = hours
        });
    }

    /// <summary>
    ///     Sets the minimum message count requirement
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="count">Minimum message count</param>
    /// <returns>Success response</returns>
    [HttpPost("minMessageCount")]
    public async Task<IActionResult> SetMinMessageCount(ulong guildId, [FromBody] int count)
    {
        if (count < 0)
            return BadRequest("Count must be positive");

        await service.SetMinMessageCountAsync(guildId, count);
        return Ok(new
        {
            minMessageCount = count
        });
    }

    /// <summary>
    ///     Enables or disables negative reputation
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="enabled">Whether to allow negative reputation</param>
    /// <returns>Success response</returns>
    [HttpPost("negativeRep")]
    public async Task<IActionResult> SetNegativeReputation(ulong guildId, [FromBody] bool enabled)
    {
        await service.SetNegativeReputationAsync(guildId, enabled);
        return Ok(new
        {
            negativeRepEnabled = enabled
        });
    }

    /// <summary>
    ///     Enables or disables anonymous reputation
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="enabled">Whether to allow anonymous reputation</param>
    /// <returns>Success response</returns>
    [HttpPost("anonymousRep")]
    public async Task<IActionResult> SetAnonymousReputation(ulong guildId, [FromBody] bool enabled)
    {
        await service.SetAnonymousReputationAsync(guildId, enabled);
        return Ok(new
        {
            anonymousRepEnabled = enabled
        });
    }

    /// <summary>
    ///     Sets the notification channel for reputation
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="channelId">The channel ID (null to disable)</param>
    /// <returns>Success response</returns>
    [HttpPost("notificationChannel")]
    public async Task<IActionResult> SetNotificationChannel(ulong guildId, [FromBody] ulong? channelId)
    {
        await service.SetNotificationChannelAsync(guildId, channelId);
        return Ok(new
        {
            notificationChannelId = channelId
        });
    }

    /// <summary>
    ///     Gets the reputation leaderboard for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of results per page</param>
    /// <returns>Leaderboard data</returns>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard(ulong guildId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest("Invalid page or pageSize");

        var leaderboard = await service.GetLeaderboardAsync(guildId, page, pageSize);

        var guild = client.GetGuild(guildId);
        var enrichedLeaderboard = leaderboard.Select((entry, index) => new
        {
            rank = (page - 1) * pageSize + index + 1,
            entry.userId,
            username = guild?.GetUser(entry.userId)?.Username ?? "Unknown",
            entry.reputation
        });

        return Ok(enrichedLeaderboard);
    }

    /// <summary>
    ///     Gets role rewards configured for reputation milestones
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>List of role rewards</returns>
    [HttpGet("roleRewards")]
    public async Task<IActionResult> GetRoleRewards(ulong guildId)
    {
        var rewards = await service.GetRoleRewardsAsync(guildId);

        var guild = client.GetGuild(guildId);
        var enrichedRewards = rewards.Select(r => new
        {
            r.RoleId,
            roleName = guild?.GetRole(r.RoleId)?.Name ?? "Unknown",
            r.RepRequired,
            r.RemoveOnDrop,
            announceChannel = r.AnnounceChannel,
            announceDM = r.AnnounceDM,
            xpReward = r.XPReward
        }).OrderBy(r => r.RepRequired);

        return Ok(enrichedRewards);
    }

    /// <summary>
    ///     Adds or updates a role reward
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="request">Role reward configuration</param>
    /// <returns>Success or error response</returns>
    [HttpPost("roleRewards")]
    public async Task<IActionResult> AddOrUpdateRoleReward(ulong guildId, [FromBody] RoleRewardRequest request)
    {
        if (request.RepRequired < 0)
            return BadRequest("Reputation required must be positive");

        var success = await service.AddOrUpdateRoleRewardAsync(
            guildId,
            request.RoleId,
            request.RepRequired,
            request.RemoveOnDrop,
            request.AnnounceChannelId,
            request.AnnounceDM,
            request.XpReward);

        if (!success)
            return BadRequest("Failed to add or update role reward");

        return Ok();
    }

    /// <summary>
    ///     Removes a role reward
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="roleId">The role ID to remove</param>
    /// <returns>Success or error response</returns>
    [HttpDelete("roleRewards/{roleId}")]
    public async Task<IActionResult> RemoveRoleReward(ulong guildId, ulong roleId)
    {
        var success = await service.RemoveRoleRewardAsync(guildId, roleId);

        if (!success)
            return NotFound("Role reward not found");

        return Ok();
    }

    /// <summary>
    ///     Gets reputation history for a user
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="userId">The ID of the user</param>
    /// <param name="page">Page number</param>
    /// <param name="pageSize">Results per page</param>
    /// <returns>Reputation history</returns>
    [HttpGet("history/{userId}")]
    public async Task<IActionResult> GetReputationHistory(ulong guildId, ulong userId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest("Invalid page or pageSize");

        var history = await service.GetReputationHistoryAsync(guildId, userId, page, pageSize);
        return Ok(history);
    }

    /// <summary>
    ///     Gets guild reputation statistics
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>Reputation statistics</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetReputationStats(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var totalUsers = await db.UserReputations
            .Where(r => r.GuildId == guildId)
            .CountAsync();

        var totalRepGiven = await db.UserReputations
            .Where(r => r.GuildId == guildId)
            .SumAsync(r => (long?)r.TotalRep) ?? 0;

        var totalTransactions = await db.RepHistories
            .CountAsync(r => r.GuildId == guildId);

        var recentActivity = await db.RepHistories
            .Where(r => r.GuildId == guildId)
            .OrderByDescending(r => r.Timestamp)
            .Take(10)
            .Select(r => new
            {
                fromUserId = r.GiverId,
                toUserId = r.ReceiverId,
                r.Amount,
                timestamp = r.Timestamp,
                r.Reason
            })
            .ToListAsync();

        var topGivers = await db.RepHistories
            .Where(r => r.GuildId == guildId)
            .GroupBy(r => r.GiverId)
            .Select(g => new
            {
                userId = g.Key, totalGiven = g.Sum(r => r.Amount)
            })
            .OrderByDescending(x => x.totalGiven)
            .Take(10)
            .ToListAsync();

        return Ok(new
        {
            totalUsers,
            totalRepGiven,
            totalTransactions,
            averageRepPerUser = totalUsers > 0 ? totalRepGiven / totalUsers : 0,
            recentActivity,
            topGivers
        });
    }

    /// <summary>
    ///     Gets valid custom reputation types for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>List of custom reputation types</returns>
    [HttpGet("customTypes")]
    public async Task<IActionResult> GetCustomReputationTypes(ulong guildId)
    {
        var types = await service.GetValidReputationTypesAsync(guildId);
        return Ok(types);
    }
}