using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing moderation actions (warnings, punishments, etc.)
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class ModerationController(
    UserPunishService userPunishService,
    DiscordShardedClient client,
    IDataConnectionFactory dbFactory,
    IDashboardAuditContext auditContext,
    ILogger<ModerationController> logger) : Controller
{
    /// <summary>
    ///     Gets all warnings for a guild
    /// </summary>
    [HttpGet("warnings")]
    public async Task<IActionResult> GetWarnings(ulong guildId)
    {
        var warnings = await userPunishService.GetAllWarnings(guildId);
        return Ok(warnings);
    }

    /// <summary>
    ///     Gets warnings for a specific user
    /// </summary>
    [HttpGet("warnings/user/{userId}")]
    public async Task<IActionResult> GetUserWarnings(ulong guildId, ulong userId)
    {
        var warnings = await userPunishService.UserWarnings(guildId, userId);
        return Ok(warnings);
    }

    /// <summary>
    ///     Issues a warning to a user on behalf of a dashboard moderator
    /// </summary>
    [HttpPost("warnings/user/{userId}")]
    public async Task<IActionResult> WarnUser(ulong guildId, ulong userId, [FromBody] WarnUserRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var moderator = await ResolveUserAsync(request.ModeratorId);
        if (moderator == null)
            return BadRequest("Moderator could not be resolved");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest("A reason is required");

        try
        {
            var punishment = await userPunishService.Warn(guild, userId, moderator, request.Reason.Trim());
            auditContext.RecordAfter(new
            {
                userId, request.Reason, moderator = moderator.Id
            });
            return Ok(new
            {
                punishmentApplied = punishment != null,
                punishment = punishment == null ? null : ((PunishmentAction)punishment.Punishment).ToString()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to warn user {UserId} in guild {GuildId}", userId, guildId);
            return StatusCode(500, "Failed to warn user");
        }
    }

    /// <summary>
    ///     Forgives a single warning by its id
    /// </summary>
    [HttpPost("warnings/{warningId:int}/forgive")]
    public async Task<IActionResult> ForgiveWarning(ulong guildId, int warningId,
        [FromBody] ModeratorRequest request)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var warning = await db.Warnings.FirstOrDefaultAsync(w => w.Id == warningId && w.GuildId == guildId);
        if (warning == null)
            return NotFound("Warning not found");

        auditContext.RecordBefore(warning);
        var moderatorName = await ResolveModeratorNameAsync(request.ModeratorId);
        await db.Warnings
            .Where(w => w.Id == warningId)
            .Set(w => w.Forgiven, true)
            .Set(w => w.ForgivenBy, moderatorName)
            .UpdateAsync();
        warning.Forgiven = true;
        warning.ForgivenBy = moderatorName;
        auditContext.RecordAfter(warning);
        return Ok(warning);
    }

    /// <summary>
    ///     Forgives every active warning a user has
    /// </summary>
    [HttpPost("warnings/user/{userId}/forgive-all")]
    public async Task<IActionResult> ForgiveAllWarnings(ulong guildId, ulong userId,
        [FromBody] ModeratorRequest request)
    {
        var moderatorName = await ResolveModeratorNameAsync(request.ModeratorId);
        auditContext.RecordBefore(await userPunishService.UserWarnings(guildId, userId));
        await userPunishService.WarnClearAsync(guildId, userId, 0, moderatorName);
        var after = await userPunishService.UserWarnings(guildId, userId);
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    /// <summary>
    ///     Permanently deletes a warning
    /// </summary>
    [HttpDelete("warnings/{warningId:int}")]
    public async Task<IActionResult> DeleteWarning(ulong guildId, int warningId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var warning = await db.Warnings.FirstOrDefaultAsync(w => w.Id == warningId && w.GuildId == guildId);
        if (warning == null)
            return NotFound("Warning not found");

        auditContext.RecordBefore(warning);
        await db.Warnings.Where(w => w.Id == warningId).DeleteAsync();
        return Ok();
    }

    /// <summary>
    ///     Gets recent moderation activity (just returns the warnings for now)
    /// </summary>
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecentActivity(ulong guildId, int limit = 20)
    {
        var warnings = await userPunishService.GetAllWarnings(guildId);
        var recentWarnings = warnings.Take(limit).ToArray();
        return Ok(recentWarnings);
    }

    /// <summary>
    ///     Gets warning punishment settings for a guild
    /// </summary>
    [HttpGet("punishments")]
    public async Task<IActionResult> GetWarningPunishments(ulong guildId)
    {
        var punishments = await userPunishService.WarnPunishList(guildId);
        return Ok(punishments.Select(p => new
        {
            p.Id,
            p.Count,
            p.Punishment,
            PunishmentName = ((PunishmentAction)p.Punishment).ToString(),
            p.Time,
            p.RoleId
        }));
    }

    /// <summary>
    ///     Adds or replaces the punishment applied at a warning count
    /// </summary>
    [HttpPut("punishments")]
    public async Task<IActionResult> SetWarningPunishment(ulong guildId, [FromBody] SetWarnPunishmentRequest request)
    {
        if (request.Count <= 0)
            return BadRequest("Warning count must be greater than zero");

        if (!Enum.IsDefined(typeof(PunishmentAction), request.Punishment))
            return BadRequest("Unknown punishment");

        var punishment = (PunishmentAction)request.Punishment;
        StoopidTime? time = null;
        if (request.TimeMinutes is > 0)
        {
            try
            {
                time = StoopidTime.FromInput($"{request.TimeMinutes.Value}m");
            }
            catch (Exception)
            {
                return BadRequest("Invalid duration");
            }
        }

        IRole? role = null;
        if (punishment == PunishmentAction.AddRole)
        {
            if (request.RoleId is null)
                return BadRequest("A role is required for the AddRole punishment");
            role = client.GetGuild(guildId)?.GetRole(request.RoleId.Value);
            if (role == null)
                return NotFound("Role not found");
        }

        auditContext.RecordBefore(await userPunishService.WarnPunishList(guildId));
        var success = await userPunishService.WarnPunish(guildId, request.Count, request.Punishment, time, role);
        if (!success)
            return BadRequest("That punishment cannot be combined with a duration, or the duration is too long");

        var after = await userPunishService.WarnPunishList(guildId);
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    /// <summary>
    ///     Removes the punishment configured for a warning count
    /// </summary>
    [HttpDelete("punishments/{count:int}")]
    public async Task<IActionResult> RemoveWarningPunishment(ulong guildId, int count)
    {
        auditContext.RecordBefore(await userPunishService.WarnPunishList(guildId));
        await userPunishService.WarnPunishRemove(guildId, count);
        var after = await userPunishService.WarnPunishList(guildId);
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    /// <summary>
    ///     Gets the warn log channel for a guild
    /// </summary>
    [HttpGet("warnlog-channel")]
    public async Task<IActionResult> GetWarnlogChannel(ulong guildId)
    {
        var channelId = await userPunishService.GetWarnlogChannel(guildId);
        return Ok(new
        {
            channelId
        });
    }

    /// <summary>
    ///     Sets the warn log channel for a guild
    /// </summary>
    [HttpPost("warnlog-channel")]
    public async Task<IActionResult> SetWarnlogChannel(ulong guildId, [FromBody] SetChannelRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var channel = guild.GetTextChannel(request.ChannelId);
        if (channel == null)
            return NotFound("Channel not found");

        auditContext.RecordBefore(new
        {
            channelId = await userPunishService.GetWarnlogChannel(guildId)
        });
        await userPunishService.SetWarnlogChannelId(guild, channel);
        auditContext.RecordAfter(new
        {
            channelId = channel.Id
        });
        return Ok(new
        {
            channelId = channel.Id
        });
    }

    private async Task<IUser?> ResolveUserAsync(ulong userId)
    {
        IUser? cached = client.GetUser(userId);
        return cached ?? await client.Rest.GetUserAsync(userId);
    }

    private async Task<string> ResolveModeratorNameAsync(ulong moderatorId)
    {
        var user = await ResolveUserAsync(moderatorId);
        return user?.Username ?? moderatorId.ToString();
    }
}

/// <summary>
///     Request to warn a user from the dashboard
/// </summary>
public class WarnUserRequest
{
    /// <summary>
    ///     The dashboard user issuing the warning
    /// </summary>
    public ulong ModeratorId { get; set; }

    /// <summary>
    ///     The reason for the warning
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
///     Request carrying the acting moderator
/// </summary>
public class ModeratorRequest
{
    /// <summary>
    ///     The dashboard user performing the action
    /// </summary>
    public ulong ModeratorId { get; set; }
}

/// <summary>
///     Request to configure the punishment for a warning count
/// </summary>
public class SetWarnPunishmentRequest
{
    /// <summary>
    ///     The number of warnings that triggers the punishment
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    ///     The punishment action as a <see cref="PunishmentAction" /> value
    /// </summary>
    public int Punishment { get; set; }

    /// <summary>
    ///     Optional duration in minutes for timed punishments
    /// </summary>
    public int? TimeMinutes { get; set; }

    /// <summary>
    ///     Role used by the AddRole punishment
    /// </summary>
    public ulong? RoleId { get; set; }
}

/// <summary>
///     Request that sets a channel
/// </summary>
public class SetChannelRequest
{
    /// <summary>
    ///     The channel id
    /// </summary>
    public ulong ChannelId { get; set; }
}