using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Moderation.Common;
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
    ModCaseService modCases,
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
    ///     Forgives every active warning in the guild
    /// </summary>
    [HttpPost("warnings/forgive-all")]
    public async Task<IActionResult> ForgiveEveryWarning(ulong guildId, [FromBody] ModeratorRequest request)
    {
        var moderatorName = await ResolveModeratorNameAsync(request.ModeratorId);
        auditContext.RecordBefore(new
        {
            active = await userPunishService.CountActiveWarningsAsync(guildId)
        });
        var cleared = await userPunishService.WarnClearAllAsync(guildId, moderatorName);
        auditContext.RecordAfter(new
        {
            cleared
        });
        return Ok(new
        {
            cleared
        });
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

    /// <summary>
    ///     Gets the reason aliases of a guild
    /// </summary>
    [HttpGet("reason-aliases")]
    public async Task<IActionResult> GetReasonAliases(ulong guildId)
    {
        var aliases = await modCases.GetReasonAliasesAsync(guildId);
        return Ok(aliases.Select(x => new
        {
            x.Name,
            x.Reason
        }));
    }

    /// <summary>
    ///     Adds or replaces a reason alias
    /// </summary>
    [HttpPut("reason-aliases")]
    public async Task<IActionResult> SetReasonAlias(ulong guildId, [FromBody] SetReasonAliasRequest request)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length is 0 or > 32 || name == "-" || name.Any(char.IsWhiteSpace))
            return BadRequest("Alias names must be one word of up to 32 characters");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest("A reason is required");

        auditContext.RecordBefore(await modCases.GetReasonAliasesAsync(guildId));
        await modCases.SetReasonAliasAsync(guildId, name, request.Reason);
        var after = await modCases.GetReasonAliasesAsync(guildId);
        auditContext.RecordAfter(after);
        return Ok(after.Select(x => new
        {
            x.Name,
            x.Reason
        }));
    }

    /// <summary>
    ///     Removes a reason alias
    /// </summary>
    [HttpDelete("reason-aliases/{name}")]
    public async Task<IActionResult> RemoveReasonAlias(ulong guildId, string name)
    {
        auditContext.RecordBefore(await modCases.GetReasonAliasesAsync(guildId));
        if (!await modCases.RemoveReasonAliasAsync(guildId, name))
            return NotFound("Alias not found");

        auditContext.RecordAfter(await modCases.GetReasonAliasesAsync(guildId));
        return Ok();
    }

    /// <summary>
    ///     Gets the custom punishment DMs of a guild, with null for actions that use the default DM
    /// </summary>
    [HttpGet("punish-dms")]
    public async Task<IActionResult> GetPunishDms(ulong guildId)
    {
        var set = await userPunishService.GetPunishDms(guildId);
        return Ok(Enum.GetValues<PunishDmAction>().ToDictionary(x => x.ToString(), x => set.GetValueOrDefault(x)));
    }

    /// <summary>
    ///     Sets the custom DM for a punishment action
    /// </summary>
    [HttpPut("punish-dms/{action}")]
    public async Task<IActionResult> SetPunishDm(ulong guildId, string action, [FromBody] SetPunishDmRequest request)
    {
        if (!Enum.TryParse<PunishDmAction>(action, true, out var parsed))
            return BadRequest("Unknown punishment");

        if (string.IsNullOrWhiteSpace(request.Template))
            return BadRequest("A message is required");

        auditContext.RecordBefore(await userPunishService.GetPunishDms(guildId));
        await userPunishService.SetPunishDm(guildId, parsed, request.Template);
        auditContext.RecordAfter(await userPunishService.GetPunishDms(guildId));
        return Ok();
    }

    /// <summary>
    ///     Puts the DM for a punishment action back to the default
    /// </summary>
    [HttpDelete("punish-dms/{action}")]
    public async Task<IActionResult> ResetPunishDm(ulong guildId, string action)
    {
        if (!Enum.TryParse<PunishDmAction>(action, true, out var parsed))
            return BadRequest("Unknown punishment");

        auditContext.RecordBefore(await userPunishService.GetPunishDms(guildId));
        await userPunishService.SetPunishDm(guildId, parsed, null);
        auditContext.RecordAfter(await userPunishService.GetPunishDms(guildId));
        return Ok();
    }

    /// <summary>
    ///     Lists mod cases, newest first, with optional filters
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">Only cases against this member</param>
    /// <param name="moderatorId">Only cases by this moderator</param>
    /// <param name="action">Only cases of this action</param>
    /// <param name="page">The zero based page</param>
    /// <param name="pageSize">Cases per page, up to 100</param>
    [HttpGet("cases")]
    public async Task<IActionResult> GetCases(ulong guildId, [FromQuery] ulong? userId = null,
        [FromQuery] ulong? moderatorId = null, [FromQuery] string? action = null, [FromQuery] int page = 0,
        [FromQuery] int pageSize = 25)
    {
        ModCaseAction? parsedAction = null;
        if (!string.IsNullOrWhiteSpace(action))
        {
            if (!Enum.TryParse<ModCaseAction>(action, true, out var value))
                return BadRequest("Unknown action");
            parsedAction = value;
        }

        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 0);

        await using var db = await dbFactory.CreateConnectionAsync();
        var query = db.ModCases.Where(x => x.GuildId == guildId);
        if (userId is { } user)
            query = query.Where(x => x.UserId == user);
        if (moderatorId is { } moderator)
            query = query.Where(x => x.ModeratorId == moderator);
        if (parsedAction is { } filterAction)
            query = query.Where(x => x.Action == filterAction);

        var total = await query.CountAsync();
        var cases = await query
            .OrderByDescending(x => x.CaseNumber)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            total,
            page,
            pageSize,
            cases = cases.Select(ToCaseResponse)
        });
    }

    /// <summary>
    ///     Gets one mod case
    /// </summary>
    [HttpGet("cases/{caseNumber:int}")]
    public async Task<IActionResult> GetCase(ulong guildId, int caseNumber)
    {
        var entry = await modCases.GetCaseAsync(guildId, caseNumber);
        return entry is null ? NotFound("Case not found") : Ok(ToCaseResponse(entry));
    }

    /// <summary>
    ///     Changes the reason of a mod case
    /// </summary>
    [HttpPut("cases/{caseNumber:int}/reason")]
    public async Task<IActionResult> SetCaseReason(ulong guildId, int caseNumber, [FromBody] SetCaseReasonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest("A reason is required");

        var before = await modCases.GetCaseAsync(guildId, caseNumber);
        if (before is null)
            return NotFound("Case not found");

        auditContext.RecordBefore(ToCaseResponse(before));
        await modCases.SetReasonAsync(guildId, caseNumber, request.Reason);
        var after = (await modCases.GetCaseAsync(guildId, caseNumber))!;
        auditContext.RecordAfter(ToCaseResponse(after));
        return Ok(ToCaseResponse(after));
    }

    /// <summary>
    ///     Deletes a mod case
    /// </summary>
    [HttpDelete("cases/{caseNumber:int}")]
    public async Task<IActionResult> DeleteCase(ulong guildId, int caseNumber)
    {
        var before = await modCases.GetCaseAsync(guildId, caseNumber);
        if (before is null)
            return NotFound("Case not found");

        auditContext.RecordBefore(ToCaseResponse(before));
        await modCases.DeleteCaseAsync(guildId, caseNumber);
        return Ok();
    }

    /// <summary>
    ///     Exports every mod case as CSV
    /// </summary>
    [HttpGet("cases/export")]
    public async Task<IActionResult> ExportCases(ulong guildId)
    {
        var cases = await modCases.GetAllCasesAsync(guildId);
        return Ok(new
        {
            csv = ModCaseService.BuildExport(cases)
        });
    }

    /// <summary>
    ///     Gets how many cases each moderator has
    /// </summary>
    [HttpGet("cases/leaderboard")]
    public async Task<IActionResult> GetCaseLeaderboard(ulong guildId, [FromQuery] int take = 15)
    {
        var rows = await modCases.GetLeaderboardAsync(guildId, Math.Clamp(take, 1, 50));
        return Ok(rows.Select(x => new
        {
            moderatorId = x.ModeratorId,
            moderatorName = client.GetUser(x.ModeratorId)?.Username ?? x.ModeratorId.ToString(),
            count = x.Count
        }));
    }

    /// <summary>
    ///     Gets the channels cases and member reports are posted to
    /// </summary>
    [HttpGet("case-channels")]
    public async Task<IActionResult> GetCaseChannels(ulong guildId)
    {
        var settings = await modCases.GetSettingsAsync(guildId);
        return Ok(new
        {
            logChannelId = settings.LogChannelId,
            reportChannelId = settings.ReportChannelId
        });
    }

    /// <summary>
    ///     Sets the channels cases and member reports are posted to, with null turning one off
    /// </summary>
    [HttpPut("case-channels")]
    public async Task<IActionResult> SetCaseChannels(ulong guildId, [FromBody] SetCaseChannelsRequest request)
    {
        var before = await modCases.GetSettingsAsync(guildId);
        auditContext.RecordBefore(new
        {
            logChannelId = before.LogChannelId,
            reportChannelId = before.ReportChannelId
        });
        await modCases.SetLogChannelAsync(guildId, request.LogChannelId);
        await modCases.SetReportChannelAsync(guildId, request.ReportChannelId);
        var result = new
        {
            logChannelId = request.LogChannelId,
            reportChannelId = request.ReportChannelId
        };
        auditContext.RecordAfter(result);
        return Ok(result);
    }

    /// <summary>
    ///     Lists moderator notes, newest first, for one member or across the guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">Only notes on this member</param>
    /// <param name="take">How many notes to return, up to 100</param>
    [HttpGet("notes")]
    public async Task<IActionResult> GetNotes(ulong guildId, [FromQuery] ulong? userId = null,
        [FromQuery] int take = 50)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var query = db.ModNotes.Where(x => x.GuildId == guildId);
        if (userId is { } user)
            query = query.Where(x => x.UserId == user);

        var notes = await query.OrderByDescending(x => x.Id).Take(Math.Clamp(take, 1, 100)).ToListAsync();
        return Ok(notes.Select(x => new
        {
            id = x.Id,
            userId = x.UserId,
            userName = client.GetUser(x.UserId)?.Username ?? x.UserId.ToString(),
            moderatorId = x.ModeratorId,
            moderatorName = client.GetUser(x.ModeratorId)?.Username ?? x.ModeratorId.ToString(),
            note = x.Note,
            dateAdded = DateTime.SpecifyKind(x.DateAdded, DateTimeKind.Utc)
        }));
    }

    /// <summary>
    ///     Adds a moderator note to a member
    /// </summary>
    [HttpPost("notes")]
    public async Task<IActionResult> AddNote(ulong guildId, [FromBody] AddModNoteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Note))
            return BadRequest("A note is required");
        if (request.Note.Length > 1000)
            return BadRequest("Notes can be up to 1000 characters");

        var note = await modCases.AddNoteAsync(guildId, request.UserId, request.ModeratorId, request.Note);
        auditContext.RecordAfter(new
        {
            note.Id,
            note.UserId,
            note.Note
        });
        return Ok(new
        {
            id = note.Id,
            userId = note.UserId,
            userName = client.GetUser(note.UserId)?.Username ?? note.UserId.ToString(),
            moderatorId = note.ModeratorId,
            moderatorName = client.GetUser(note.ModeratorId)?.Username ?? note.ModeratorId.ToString(),
            note = note.Note,
            dateAdded = DateTime.SpecifyKind(note.DateAdded, DateTimeKind.Utc)
        });
    }

    /// <summary>
    ///     Removes one moderator note
    /// </summary>
    [HttpDelete("notes/{noteId:int}")]
    public async Task<IActionResult> RemoveNote(ulong guildId, int noteId)
    {
        if (!await modCases.RemoveNoteAsync(guildId, noteId))
            return NotFound("Note not found");

        auditContext.RecordBefore(new
        {
            noteId
        });
        return Ok();
    }

    /// <summary>
    ///     Removes every moderator note on a member
    /// </summary>
    [HttpDelete("notes/user/{userId}")]
    public async Task<IActionResult> ClearNotes(ulong guildId, ulong userId)
    {
        var removed = await modCases.ClearNotesAsync(guildId, userId);
        auditContext.RecordBefore(new
        {
            userId,
            removed
        });
        return Ok(new
        {
            removed
        });
    }

    private object ToCaseResponse(ModCase entry) => new
    {
        caseNumber = entry.CaseNumber,
        userId = entry.UserId,
        userName = client.GetUser(entry.UserId)?.Username ?? entry.UserId.ToString(),
        moderatorId = entry.ModeratorId,
        moderatorName = client.GetUser(entry.ModeratorId)?.Username ?? entry.ModeratorId.ToString(),
        action = entry.Action.ToString(),
        reason = entry.Reason,
        durationSeconds = entry.DurationSeconds,
        dateAdded = DateTime.SpecifyKind(entry.DateAdded, DateTimeKind.Utc)
    };

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
///     Request that sets a reason alias
/// </summary>
public class SetReasonAliasRequest
{
    /// <summary>
    ///     The short name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     The full reason
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
///     Request that sets a punishment DM
/// </summary>
public class SetPunishDmRequest
{
    /// <summary>
    ///     The message, plain text or embed code
    /// </summary>
    public string? Template { get; set; }
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
/// <summary>
///     Request that changes the reason of a mod case
/// </summary>
public class SetCaseReasonRequest
{
    /// <summary>
    ///     The new reason
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
///     Request that sets the case and report channels, with null turning one off
/// </summary>
public class SetCaseChannelsRequest
{
    /// <summary>
    ///     The channel new cases are posted to
    /// </summary>
    public ulong? LogChannelId { get; set; }

    /// <summary>
    ///     The channel member reports are posted to
    /// </summary>
    public ulong? ReportChannelId { get; set; }
}

/// <summary>
///     Request that adds a moderator note
/// </summary>
public class AddModNoteRequest
{
    /// <summary>
    ///     The member the note is about
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The dashboard user writing the note
    /// </summary>
    public ulong ModeratorId { get; set; }

    /// <summary>
    ///     The note
    /// </summary>
    public string? Note { get; set; }
}
