using DataModel;
using Discord;
using Mewdeko.Controllers.Common.AutoMod;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Reads and changes a guild's native AutoMod rules and their follow-ups for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class AutoModController(
    DiscordShardedClient client,
    DiscordAutoModService service,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Gets whether the bot can manage AutoMod rules and add timeouts.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet("status")]
    public async Task<IActionResult> Status(ulong guildId)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");

        var permissions = guild.CurrentUser.GuildPermissions;
        return Ok(new AutoModStatusDto
        {
            CanManage = permissions.ManageGuild, CanModerate = permissions.ModerateMembers
        });
    }

    /// <summary>
    ///     Lists the guild's AutoMod rules with their follow-ups.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet("rules")]
    public async Task<IActionResult> List(ulong guildId)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");

        var (failure, rules) = await service.ListAsync(guild);
        if (failure is not null)
            return Refuse(failure);

        var followUps = await service.GetFollowUpsAsync(guildId);
        return Ok(rules.Select(x => ToDto(x, followUps.GetValueOrDefault(x.Id))).ToList());
    }

    /// <summary>
    ///     Gets one AutoMod rule with its follow-up.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ruleId">The rule ID.</param>
    [HttpGet("rules/{ruleId}")]
    public async Task<IActionResult> Get(ulong guildId, ulong ruleId)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");

        var (failure, rules) = await service.ListAsync(guild);
        if (failure is not null)
            return Refuse(failure);

        var rule = rules.FirstOrDefault(x => x.Id == ruleId);
        return rule is null
            ? NotFound("Rule not found")
            : Ok(ToDto(rule, await service.GetFollowUpAsync(guildId, ruleId)));
    }

    /// <summary>
    ///     Creates an AutoMod rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The rule.</param>
    [HttpPost("rules")]
    public async Task<IActionResult> Create(ulong guildId, [FromBody] AutoModRuleSpec request)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");

        var result = await service.CreateAsync(guild, request);
        if (!result.Ok)
            return Refuse(result);

        var after = ToDto(result.Rule!, null);
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    /// <summary>
    ///     Changes an AutoMod rule. Fields left out stay as they are.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ruleId">The rule ID.</param>
    /// <param name="request">The changes.</param>
    [HttpPut("rules/{ruleId}")]
    public async Task<IActionResult> Update(ulong guildId, ulong ruleId, [FromBody] AutoModRuleSpec request)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");

        if (await guild.GetAutoModRuleAsync(ruleId) is { } before)
            auditContext.RecordBefore(ToDto(before, null));

        var result = await service.UpdateAsync(guild, ruleId, request);
        if (!result.Ok)
            return Refuse(result);

        var after = ToDto(result.Rule!, await service.GetFollowUpAsync(guildId, ruleId));
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    /// <summary>
    ///     Deletes an AutoMod rule and its follow-up.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ruleId">The rule ID.</param>
    [HttpDelete("rules/{ruleId}")]
    public async Task<IActionResult> Delete(ulong guildId, ulong ruleId)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");

        if (await guild.GetAutoModRuleAsync(ruleId) is { } before)
            auditContext.RecordBefore(ToDto(before, await service.GetFollowUpAsync(guildId, ruleId)));

        var result = await service.DeleteAsync(guild, ruleId);
        return result.Ok ? Ok() : Refuse(result);
    }

    /// <summary>
    ///     Gets the follow-up for a rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ruleId">The rule ID.</param>
    [HttpGet("followups/{ruleId}")]
    public async Task<IActionResult> GetFollowUp(ulong guildId, ulong ruleId)
    {
        var followUp = await service.GetFollowUpAsync(guildId, ruleId);
        return followUp is null ? NotFound("No follow-up for that rule") : Ok(ToDto(followUp));
    }

    /// <summary>
    ///     Saves the follow-up for a rule. Every field is written.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ruleId">The rule ID.</param>
    /// <param name="request">The follow-up.</param>
    [HttpPut("followups/{ruleId}")]
    public async Task<IActionResult> SetFollowUp(ulong guildId, ulong ruleId, [FromBody] AutoModFollowUpDto request)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");

        if (await guild.GetAutoModRuleAsync(ruleId) is null)
            return NotFound("Rule not found");

        if (await service.GetFollowUpAsync(guildId, ruleId) is { } before)
            auditContext.RecordBefore(ToDto(before));

        var (saved, followUp) = await service.UpdateFollowUpAsync(guildId, ruleId, x =>
        {
            x.Warn = request.Warn;
            x.WarnReason = string.IsNullOrWhiteSpace(request.WarnReason) ? null : request.WarnReason;
            x.PunishAction = request.PunishAction;
            x.PunishMinutes = request.PunishAction == AutoModPunishAction.None ? 0 : request.PunishMinutes;
            x.EscalateAction = request.EscalateAction;
            x.EscalateHits = request.EscalateAction == AutoModEscalateAction.None ? 0 : request.EscalateHits;
            x.EscalateWindowMinutes = request.EscalateAction == AutoModEscalateAction.None
                ? 0
                : request.EscalateWindowMinutes;
            x.LogChannelId = request.LogChannelId;
        });

        if (!saved)
            return BadRequest(new AutoModErrorDto
            {
                Error = AutoModError.InvalidValue, Message = "Invalid follow-up"
            });

        var after = ToDto(followUp);
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    /// <summary>
    ///     Removes the follow-up for a rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ruleId">The rule ID.</param>
    [HttpDelete("followups/{ruleId}")]
    public async Task<IActionResult> DeleteFollowUp(ulong guildId, ulong ruleId)
    {
        if (await service.GetFollowUpAsync(guildId, ruleId) is { } before)
            auditContext.RecordBefore(ToDto(before));

        await service.ClearFollowUpAsync(guildId, ruleId);
        return Ok();
    }

    private IActionResult Refuse(AutoModResult result)
    {
        var body = new AutoModErrorDto
        {
            Error = result.Error, Message = result.Message
        };
        return result.Error switch
        {
            AutoModError.MissingPermission => StatusCode(StatusCodes.Status403Forbidden, body),
            AutoModError.NotFound => NotFound(body),
            _ => BadRequest(body)
        };
    }

    private static AutoModFollowUpDto ToDto(AutoModFollowUp followUp) => new()
    {
        Warn = followUp.Warn,
        WarnReason = followUp.WarnReason,
        PunishAction = followUp.PunishAction,
        PunishMinutes = followUp.PunishMinutes,
        EscalateAction = followUp.EscalateAction,
        EscalateHits = followUp.EscalateHits,
        EscalateWindowMinutes = followUp.EscalateWindowMinutes,
        LogChannelId = followUp.LogChannelId
    };

    private static AutoModRuleDto ToDto(IAutoModRule rule, AutoModFollowUp? followUp) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        Kind = DiscordAutoModService.KindOf(rule.TriggerType),
        Enabled = rule.Enabled,
        Keywords = rule.KeywordFilter?.ToList() ?? [],
        RegexPatterns = rule.RegexPatterns?.ToList() ?? [],
        AllowList = rule.AllowList?.ToList() ?? [],
        Presets = rule.Presets?.Select(x => x.ToString()).ToList() ?? [],
        MentionLimit = rule.MentionTotalLimit,
        MentionRaidProtection = rule.MentionRaidProtectionEnabled ?? false,
        Actions = DiscordAutoModService.ActionsOf(rule),
        ExemptRoleIds = rule.ExemptRoles?.ToList() ?? [],
        ExemptChannelIds = rule.ExemptChannels?.ToList() ?? [],
        FollowUp = followUp is null ? null : ToDto(followUp)
    };
}
