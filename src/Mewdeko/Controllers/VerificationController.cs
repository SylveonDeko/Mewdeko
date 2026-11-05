using DataModel;
using Mewdeko.Controllers.Common.Verification;
using Mewdeko.Modules.Verification.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Reads and changes a guild's verification gate for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class VerificationController(
    VerificationService service,
    DiscordShardedClient client,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Gets the verification settings.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet]
    public async Task<IActionResult> Get(ulong guildId) => Ok(ToDto(await service.GetSettingsAsync(guildId)));

    /// <summary>
    ///     Saves the verification settings. Every field is written, so a null clears an optional one.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The settings.</param>
    [HttpPost]
    public async Task<IActionResult> Update(ulong guildId, [FromBody] VerificationSettingsDto request)
    {
        auditContext.RecordBefore(ToDto(await service.GetSettingsAsync(guildId)));

        var (error, setting, _) = await service.UpdateAsync(guildId, x =>
        {
            x.Enabled = request.Enabled;
            x.ChannelId = request.ChannelId;
            x.UnverifiedRoleId = request.UnverifiedRoleId;
            x.VerifiedRoleId = request.VerifiedRoleId;
            x.MaxAttempts = request.MaxAttempts;
            x.TimeoutMinutes = request.TimeoutMinutes;
            x.KickOnFail = request.KickOnFail;
            x.LogChannelId = request.LogChannelId;
        });

        if (error != VerificationError.None)
            return BadRequest(service.DescribeError(guildId, error));

        var after = ToDto(setting);
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    /// <summary>
    ///     Creates or uses a verification channel and posts the button in it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The channel to use.</param>
    [HttpPost("setup")]
    public async Task<IActionResult> Setup(ulong guildId, [FromBody] VerificationSetupRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound();

        var channel = request.ChannelId is { } id ? guild.GetTextChannel(id) : null;
        if (request.ChannelId is not null && channel is null)
            return BadRequest("InvalidChannel");

        var result = await service.SetupAsync(guild, channel);
        return result is null ? BadRequest("SetupFailed") : Ok(ToDto(await service.GetSettingsAsync(guildId)));
    }

    /// <summary>
    ///     Posts the verification button again.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpPost("panel")]
    public async Task<IActionResult> Panel(ulong guildId) =>
        await service.PostPanelAsync(guildId) ? Ok() : BadRequest("NoChannel");

    /// <summary>
    ///     Lists the channel overwrites the gate would change. Nothing is changed.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet("permissions")]
    public async Task<IActionResult> PreviewPermissions(ulong guildId) =>
        Ok((await service.PlanPermissionsAsync(guildId)).Select(x => new VerificationPermissionChangeDto
        {
            ChannelId = x.ChannelId, ChannelName = x.ChannelName, RoleId = x.RoleId
        }));

    /// <summary>
    ///     Changes the channel overwrites the gate needs, remembering what they were.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpPost("permissions")]
    public async Task<IActionResult> ApplyPermissions(ulong guildId)
    {
        var (changed, failed) = await service.ApplyPermissionsAsync(guildId);
        return Ok(new VerificationPermissionResult
        {
            Changed = changed, Failed = failed
        });
    }

    /// <summary>
    ///     Puts back the channel overwrites the gate changed.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpDelete("permissions")]
    public async Task<IActionResult> RestorePermissions(ulong guildId)
    {
        var (restored, failed) = await service.RestorePermissionsAsync(guildId);
        return Ok(new VerificationPermissionResult
        {
            Changed = restored, Failed = failed
        });
    }

    /// <summary>
    ///     Gets how many members a bulk verification would cover.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet("verify-existing")]
    public async Task<IActionResult> CountExisting(ulong guildId) => Ok(new VerificationBulkResponse
    {
        Count = await service.CountExistingAsync(guildId)
    });

    /// <summary>
    ///     Verifies the members already in the guild without a captcha, in the background.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpPost("verify-existing")]
    public async Task<IActionResult> VerifyExisting(ulong guildId)
    {
        var count = await service.VerifyExistingAsync(guildId);
        return count is null
            ? NotFound()
            : Ok(new VerificationBulkResponse
            {
                Count = count.Value
            });
    }

    private static VerificationSettingsDto ToDto(VerificationSetting setting) => new()
    {
        Enabled = setting.Enabled,
        ChannelId = setting.ChannelId,
        UnverifiedRoleId = setting.UnverifiedRoleId,
        VerifiedRoleId = setting.VerifiedRoleId,
        MaxAttempts = setting.MaxAttempts,
        TimeoutMinutes = setting.TimeoutMinutes,
        KickOnFail = setting.KickOnFail,
        LogChannelId = setting.LogChannelId
    };
}
