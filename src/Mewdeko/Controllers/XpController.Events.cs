using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Controllers.Common.XpEvents;
using Mewdeko.Modules.Xp.Models;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

public partial class XpController
{
    /// <summary>
    ///     Gets the per-channel and per-role XP multipliers
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>The channel and role multipliers</returns>
    [HttpGet("multipliers")]
    public async Task<IActionResult> GetMultipliers(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var guild = client.GetGuild(guildId);

        var channels = await db.XpChannelMultipliers.Where(x => x.GuildId == guildId).ToListAsync();
        var roles = await db.XpRoleMultipliers.Where(x => x.GuildId == guildId).ToListAsync();

        return Ok(new
        {
            Channels = channels.Select(x => new
            {
                x.ChannelId, Name = guild?.GetChannel(x.ChannelId)?.Name, x.Multiplier
            }),
            Roles = roles.Select(x => new
            {
                x.RoleId, Name = guild?.GetRole(x.RoleId)?.Name, x.Multiplier
            })
        });
    }

    /// <summary>
    ///     Sets a channel XP multiplier, 1 removes it
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="request">The channel and multiplier</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpPost("multipliers/channels")]
    public async Task<IActionResult> SetChannelMultiplier(ulong guildId, [FromBody] XpMultiplierRequest? request)
    {
        if (request == null)
            return BadRequest(BodyError());
        if (request.Multiplier <= 0)
            return BadRequest("Multiplier must be positive");
        if (client.GetGuild(guildId)?.GetChannel(request.Id) == null)
            return NotFound("Channel not found");

        await xp.SetChannelMultiplierAsync(guildId, request.Id, request.Multiplier);
        return Ok();
    }

    /// <summary>
    ///     Removes a channel XP multiplier
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="channelId">The channel ID</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpDelete("multipliers/channels/{channelId}")]
    public async Task<IActionResult> RemoveChannelMultiplier(ulong guildId, ulong channelId)
    {
        await xp.SetChannelMultiplierAsync(guildId, channelId, 1.0);
        return Ok();
    }

    /// <summary>
    ///     Sets a role XP multiplier, 1 removes it
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="request">The role and multiplier</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpPost("multipliers/roles")]
    public async Task<IActionResult> SetRoleMultiplier(ulong guildId, [FromBody] XpMultiplierRequest? request)
    {
        if (request == null)
            return BadRequest(BodyError());
        if (request.Multiplier <= 0)
            return BadRequest("Multiplier must be positive");
        if (client.GetGuild(guildId)?.GetRole(request.Id) == null)
            return NotFound("Role not found");

        await xp.SetRoleMultiplierAsync(guildId, request.Id, request.Multiplier);
        return Ok();
    }

    /// <summary>
    ///     Removes a role XP multiplier
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="roleId">The role ID</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpDelete("multipliers/roles/{roleId}")]
    public async Task<IActionResult> RemoveRoleMultiplier(ulong guildId, ulong roleId)
    {
        await xp.SetRoleMultiplierAsync(guildId, roleId, 1.0);
        return Ok();
    }

    /// <summary>
    ///     Gets the members excluded from XP gain
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>The excluded members</returns>
    [HttpGet("excluded/users")]
    public async Task<IActionResult> GetExcludedUsers(ulong guildId)
    {
        var ids = await xp.GetExcludedItemsAsync(guildId, ExcludedItemType.User);
        var guild = client.GetGuild(guildId);
        return Ok(ids.Select(x => new
        {
            UserId = x, Username = guild?.GetUser(x)?.Username
        }));
    }

    /// <summary>
    ///     Excludes a member from XP gain
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The member to exclude</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpPost("excluded/users")]
    public async Task<IActionResult> ExcludeUser(ulong guildId, [FromBody] ulong userId)
    {
        await xp.ExcludeItemAsync(guildId, userId, ExcludedItemType.User);
        return Ok();
    }

    /// <summary>
    ///     Includes a member in XP gain again
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The member to include</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpDelete("excluded/users/{userId}")]
    public async Task<IActionResult> IncludeUser(ulong guildId, ulong userId)
    {
        await xp.IncludeItemAsync(guildId, userId, ExcludedItemType.User);
        return Ok();
    }

    /// <summary>
    ///     Gets every XP boost of a guild, newest first
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>The boosts</returns>
    [HttpGet("boosts")]
    public async Task<IActionResult> GetBoosts(ulong guildId)
    {
        var boosts = await xp.GetAllBoostEventsAsync(guildId);
        var now = DateTime.UtcNow;

        return Ok(boosts.OrderByDescending(x => x.StartTime).Select(x => new
        {
            x.Id,
            x.Name,
            x.Multiplier,
            x.StartTime,
            x.EndTime,
            Active = x.StartTime <= now && x.EndTime >= now,
            ChannelIds = ParseIds(x.ApplicableChannels),
            RoleIds = ParseIds(x.ApplicableRoles)
        }));
    }

    /// <summary>
    ///     Starts an XP boost now
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="request">The boost</param>
    /// <returns>A 200 status code if successful, or the reason the boost was refused</returns>
    [HttpPost("boosts")]
    public async Task<IActionResult> CreateBoost(ulong guildId, [FromBody] XpBoostRequest? request)
    {
        if (request == null)
            return BadRequest(BodyError());
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required");
        if (request.Multiplier <= 1.0)
            return BadRequest("Multiplier must be above 1");
        if (request.DurationMinutes < 5)
            return BadRequest("A boost must last at least 5 minutes");

        var start = DateTime.UtcNow;
        var boost = await xp.CreateXpBoostEventAsync(guildId, request.Name.Trim(), request.Multiplier, start,
            start.AddMinutes(request.DurationMinutes), request.ChannelIds, request.RoleIds);
        auditContext.RecordAfter(boost);
        return Ok();
    }

    /// <summary>
    ///     Cancels an XP boost
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="eventId">The boost ID</param>
    /// <returns>A 200 status code if successful, or a 404 when there was none</returns>
    [HttpDelete("boosts/{eventId}")]
    public async Task<IActionResult> CancelBoost(ulong guildId, int eventId)
    {
        var boosts = await xp.GetAllBoostEventsAsync(guildId);
        var boost = boosts.FirstOrDefault(x => x.Id == eventId);
        if (boost == null)
            return NotFound();

        auditContext.RecordBefore(boost);
        await xp.CancelXpBoostEventAsync(eventId);
        return Ok();
    }

    /// <summary>
    ///     Gets every competition of a guild, newest first
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>The competitions with their rewards</returns>
    [HttpGet("competitions")]
    public async Task<IActionResult> GetCompetitions(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var competitions = await db.XpCompetitions
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.StartTime)
            .ToListAsync();

        var ids = competitions.Select(x => x.Id).ToList();
        var rewards = await db.XpCompetitionRewards.Where(x => ids.Contains(x.CompetitionId)).ToListAsync();
        var entryCounts = await db.XpCompetitionEntries
            .Where(x => ids.Contains(x.CompetitionId))
            .GroupBy(x => x.CompetitionId)
            .Select(g => new
            {
                CompetitionId = g.Key, Count = g.Count()
            })
            .ToListAsync();

        var guild = client.GetGuild(guildId);
        var now = DateTime.UtcNow;

        return Ok(competitions.Select(x => new
        {
            x.Id,
            x.Name,
            x.Type,
            x.StartTime,
            x.EndTime,
            x.TargetLevel,
            x.AnnouncementChannelId,
            Active = x.StartTime <= now && x.EndTime >= now,
            Participants = entryCounts.FirstOrDefault(c => c.CompetitionId == x.Id)?.Count ?? 0,
            Rewards = rewards.Where(r => r.CompetitionId == x.Id).OrderBy(r => r.Position).Select(r => new
            {
                r.Id,
                r.Position,
                r.RoleId,
                RoleName = r.RoleId == 0 ? null : guild?.GetRole(r.RoleId)?.Name,
                r.XpAmount,
                r.CurrencyAmount
            })
        }));
    }

    /// <summary>
    ///     Creates a competition with its rewards and starts it now
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="request">The competition</param>
    /// <returns>The competition ID, or the reason it was refused</returns>
    [HttpPost("competitions")]
    public async Task<IActionResult> CreateCompetition(ulong guildId, [FromBody] XpCompetitionRequest? request)
    {
        if (request == null)
            return BadRequest(BodyError());
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required");
        if (request.DurationMinutes < 60)
            return BadRequest("A competition must last at least 60 minutes");
        if (request.Type == XpCompetitionType.ReachLevel && request.TargetLevel <= 0)
            return BadRequest("A target level is required");
        if (request.Rewards.Any(r => r.Position <= 0))
            return BadRequest("Positions start at 1");

        var start = DateTime.UtcNow;
        var competition = await xp.CreateCompetitionAsync(guildId, request.Name.Trim(), request.Type, start,
            start.AddMinutes(request.DurationMinutes), request.TargetLevel,
            request.AnnouncementChannelId == 0 ? null : request.AnnouncementChannelId);

        foreach (var reward in request.Rewards)
        {
            await xp.AddCompetitionRewardAsync(competition.Id, reward.Position, reward.RoleId, reward.XpAmount,
                reward.CurrencyAmount);
        }

        await xp.StartCompetitionAsync(competition.Id);
        auditContext.RecordAfter(competition);
        return Ok(competition.Id);
    }

    /// <summary>
    ///     Gets the standings of a competition
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="competitionId">The competition ID</param>
    /// <returns>The top 25 standings</returns>
    [HttpGet("competitions/{competitionId}/standings")]
    public async Task<IActionResult> GetCompetitionStandings(ulong guildId, int competitionId)
    {
        var competition = await xp.GetCompetitionAsync(competitionId);
        if (competition == null || competition.GuildId != guildId)
            return NotFound();

        var entries = await xp.GetCompetitionEntriesAsync(competitionId);
        var ordered = (XpCompetitionType)competition.Type switch
        {
            XpCompetitionType.HighestTotal => entries.OrderByDescending(x => x.CurrentXp),
            XpCompetitionType.ReachLevel => entries
                .OrderBy(x => x.AchievedTargetAt.HasValue ? 0 : 1)
                .ThenBy(x => x.AchievedTargetAt)
                .ThenByDescending(x => x.CurrentXp - x.StartingXp),
            _ => entries.OrderByDescending(x => x.CurrentXp - x.StartingXp)
        };

        var guild = client.GetGuild(guildId);
        return Ok(ordered.Take(25).Select((x, i) => new
        {
            Position = i + 1,
            x.UserId,
            Username = guild?.GetUser(x.UserId)?.Username ?? "Unknown",
            Gained = x.CurrentXp - x.StartingXp,
            Total = x.CurrentXp,
            x.AchievedTargetAt,
            x.FinalPlacement
        }));
    }

    /// <summary>
    ///     Ends a running competition and hands out its rewards
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="competitionId">The competition ID</param>
    /// <returns>A 200 status code if successful, or the reason it was refused</returns>
    [HttpPost("competitions/{competitionId}/end")]
    public async Task<IActionResult> EndCompetition(ulong guildId, int competitionId)
    {
        var competition = await xp.GetCompetitionAsync(competitionId);
        if (competition == null || competition.GuildId != guildId)
            return NotFound();
        if (competition.EndTime < DateTime.UtcNow)
            return BadRequest("Competition already ended");

        auditContext.RecordBefore(competition);
        await xp.FinalizeCompetitionAsync(competitionId);
        return Ok();
    }

    /// <summary>
    ///     Gets the custom level-up messages
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>The messages</returns>
    [HttpGet("level-up-messages")]
    public async Task<IActionResult> GetLevelUpMessages(ulong guildId)
    {
        var messages = await xp.GetLevelUpMessagesAsync(guildId);
        return Ok(messages.Select(x => new
        {
            x.Id, x.MessageContent, x.IsEnabled
        }));
    }

    /// <summary>
    ///     Adds a custom level-up message
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="request">The message template</param>
    /// <returns>A 200 status code if successful</returns>
    [HttpPost("level-up-messages")]
    public async Task<IActionResult> AddLevelUpMessage(ulong guildId, [FromBody] XpLevelUpMessageRequest? request)
    {
        if (request == null)
            return BadRequest(BodyError());
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Message is required");

        await xp.AddLevelUpMessageAsync(guildId, request.Message);
        return Ok();
    }

    /// <summary>
    ///     Turns a custom level-up message on or off
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="messageId">The message ID</param>
    /// <param name="enabled">Whether it is used</param>
    /// <returns>A 200 status code if successful, or a 404 when there was none</returns>
    [HttpPost("level-up-messages/{messageId}/toggle")]
    public async Task<IActionResult> ToggleLevelUpMessage(ulong guildId, int messageId, [FromBody] bool enabled)
    {
        return await xp.ToggleLevelUpMessageAsync(guildId, messageId, enabled) ? Ok() : NotFound();
    }

    /// <summary>
    ///     Removes a custom level-up message
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="messageId">The message ID</param>
    /// <returns>A 200 status code if successful, or a 404 when there was none</returns>
    [HttpDelete("level-up-messages/{messageId}")]
    public async Task<IActionResult> RemoveLevelUpMessage(ulong guildId, int messageId)
    {
        return await xp.RemoveLevelUpMessageAsync(guildId, messageId) ? Ok() : NotFound();
    }

    /// <summary>
    ///     Starts syncing every member's level roles with their XP in the background
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <returns>A 202 status code once started</returns>
    [HttpPost("sync-roles")]
    public async Task<IActionResult> SyncRoles(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var rewards = await xp.GetRoleRewardsAsync(guildId);
        if (rewards.Count == 0)
            return BadRequest("No role rewards to sync");

        _ = Task.Run(async () =>
        {
            try
            {
                await roleSyncService.SyncAllUsersAsync(guild);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already in progress"))
            {
                logger.LogDebug("Role sync already running for guild {GuildId}", guildId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during role sync for guild {GuildId}", guildId);
            }
        });

        return Accepted();
    }

    /// <summary>
    ///     Resets every member's XP
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="request">Whether bonus XP is reset too</param>
    /// <returns>How many members were reset</returns>
    [HttpPost("reset")]
    public async Task<IActionResult> ResetServerXp(ulong guildId, [FromBody] XpResetRequest? request)
    {
        var affected = await xp.ResetGuildXp(guildId, request?.ResetBonusXp ?? false);
        return Ok(affected);
    }

    private static List<ulong> ParseIds(string? ids)
    {
        return string.IsNullOrWhiteSpace(ids)
            ? []
            : ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => ulong.TryParse(x, out var id) ? id : 0UL)
                .Where(x => x != 0)
                .ToList();
    }
}
