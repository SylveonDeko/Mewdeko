using DataModel;
using Mewdeko.Controllers.Common.JoinRoleTimeout;
using Mewdeko.Modules.Administration.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Reads and changes a guild's join role timeout for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class JoinRoleTimeoutController(
    JoinRoleTimeoutService service,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Gets the join role timeout settings.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet]
    public async Task<IActionResult> Get(ulong guildId) => Ok(ToDto(await service.GetSettingsAsync(guildId)));

    /// <summary>
    ///     Saves the join role timeout settings. Every field is written, so a null clears an optional one.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The settings.</param>
    [HttpPost]
    public async Task<IActionResult> Update(ulong guildId, [FromBody] JoinRoleTimeoutSettingsDto request)
    {
        auditContext.RecordBefore(ToDto(await service.GetSettingsAsync(guildId)));

        var (error, setting) = await service.UpdateAsync(guildId, x =>
        {
            x.Enabled = request.Enabled;
            x.TimeoutMinutes = request.TimeoutMinutes;
            x.RequiredRoleIds = JoinRoleTimeoutService.JoinIds(request.RequiredRoleIds);
            x.ExemptRoleIds = JoinRoleTimeoutService.JoinIds(request.ExemptRoleIds);
            x.Action = request.Action;
            x.ActionRoleId = request.ActionRoleId;
            x.WarnBeforeMinutes = request.WarnBeforeMinutes;
            x.WarnMessage = string.IsNullOrWhiteSpace(request.WarnMessage) ? null : request.WarnMessage;
            x.ActionMessage = string.IsNullOrWhiteSpace(request.ActionMessage) ? null : request.ActionMessage;
            x.LogChannelId = request.LogChannelId;
            x.IncludeExistingMembers = request.IncludeExistingMembers;
        });

        if (error != JoinRoleTimeoutError.None)
            return BadRequest(error.ToString());

        var after = ToDto(setting);
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    /// <summary>
    ///     Gets how many members are counting down and the ones due next.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="limit">The most members to list, up to 100.</param>
    [HttpGet("pending")]
    public async Task<IActionResult> Pending(ulong guildId, [FromQuery] int limit = 10)
    {
        var setting = await service.GetSettingsAsync(guildId);
        var (count, next) = await service.GetPendingAsync(guildId, limit);
        return Ok(new JoinRoleTimeoutPendingResponse
        {
            Count = count,
            Next = next.Select(x => new JoinRoleTimeoutPendingDto
            {
                UserId = x.UserId,
                JoinedAt = DateTime.SpecifyKind(x.JoinedAt, DateTimeKind.Utc),
                DueAt = DateTime.SpecifyKind(x.JoinedAt.AddMinutes(setting.TimeoutMinutes), DateTimeKind.Utc),
                WarnedAt = x.WarnedAt is { } warned ? DateTime.SpecifyKind(warned, DateTimeKind.Utc) : null
            }).ToList()
        });
    }

    private static JoinRoleTimeoutSettingsDto ToDto(JoinRoleTimeoutSetting setting) => new()
    {
        Enabled = setting.Enabled,
        TimeoutMinutes = setting.TimeoutMinutes,
        RequiredRoleIds = JoinRoleTimeoutService.ParseIds(setting.RequiredRoleIds),
        ExemptRoleIds = JoinRoleTimeoutService.ParseIds(setting.ExemptRoleIds),
        Action = setting.Action,
        ActionRoleId = setting.ActionRoleId,
        WarnBeforeMinutes = setting.WarnBeforeMinutes,
        WarnMessage = setting.WarnMessage,
        ActionMessage = setting.ActionMessage,
        LogChannelId = setting.LogChannelId,
        IncludeExistingMembers = setting.IncludeExistingMembers
    };
}
