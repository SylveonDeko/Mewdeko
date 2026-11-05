using DataModel;
using Mewdeko.Controllers.Common.Bridge;
using Mewdeko.Modules.Bridge.Common;
using Mewdeko.Modules.Bridge.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Manages a guild's bridged channels for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public partial class BridgeController(
    BridgeService bridges,
    DiscordShardedClient client,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Lists the guild's bridged channels.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet]
    public IActionResult Get(ulong guildId) => Ok(bridges.GetBridges(guildId).Select(ToResponse));

    /// <summary>
    ///     Creates a link code for a channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The channel and the dashboard user.</param>
    [HttpPost("code")]
    public async Task<IActionResult> CreateCode(ulong guildId, [FromBody] BridgeCodeRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found");

        if (guild.GetTextChannel(request.ChannelId) is not { } channel)
            return NotFound("Channel not found");

        var (error, code, expiresAt) = await bridges.CreateCodeAsync(guild, channel, request.UserId);
        return error switch
        {
            BridgeError.None => Ok(new BridgeCodeResponse
            {
                Code = code!, ExpiresAt = expiresAt
            }),
            BridgeError.NoPermission => StatusCode(StatusCodes.Status403Forbidden, error.ToString()),
            _ => BadRequest(error.ToString())
        };
    }

    /// <summary>
    ///     Bridges a channel using a code made on the other platform.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The code, the channel and the dashboard user.</param>
    [HttpPost("link")]
    public async Task<IActionResult> Link(ulong guildId, [FromBody] BridgeLinkRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found");

        if (guild.GetTextChannel(request.ChannelId) is not { } channel)
            return NotFound("Channel not found");

        var (error, bridge) = await bridges.LinkAsync(guild, channel, request.UserId, request.Code);
        if (error != BridgeError.None || bridge is null)
        {
            return error == BridgeError.NoPermission
                ? StatusCode(StatusCodes.Status403Forbidden, error.ToString())
                : BadRequest(error.ToString());
        }

        var response = ToResponse(bridge);
        auditContext.RecordAfter(response);
        return Ok(response);
    }

    /// <summary>
    ///     Pauses a bridge.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="bridgeId">The bridge ID.</param>
    [HttpPost("{bridgeId:guid}/pause")]
    public Task<IActionResult> Pause(ulong guildId, Guid bridgeId) =>
        Change(guildId, bridgeId, bridge => bridges.SetPausedAsync(bridge, true));

    /// <summary>
    ///     Resumes a bridge.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="bridgeId">The bridge ID.</param>
    [HttpPost("{bridgeId:guid}/resume")]
    public Task<IActionResult> Resume(ulong guildId, Guid bridgeId) =>
        Change(guildId, bridgeId, bridge => bridges.SetPausedAsync(bridge, false));

    /// <summary>
    ///     Sets whether a bridge relays messages from bots.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="bridgeId">The bridge ID.</param>
    /// <param name="request">Whether bots are relayed.</param>
    [HttpPost("{bridgeId:guid}/allow-bots")]
    public Task<IActionResult> AllowBots(ulong guildId, Guid bridgeId, [FromBody] BridgeAllowBotsRequest request) =>
        Change(guildId, bridgeId, bridge => bridges.SetAllowBotsAsync(bridge, request.Allow));

    /// <summary>
    ///     Replaces all the options of a bridge.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="bridgeId">The bridge ID.</param>
    /// <param name="request">The new options.</param>
    [HttpPost("{bridgeId:guid}/settings")]
    public async Task<IActionResult> Settings(ulong guildId, Guid bridgeId, [FromBody] BridgeSettingsRequest? request)
    {
        if (bridges.GetBridge(guildId, bridgeId) is not { } bridge)
            return BadRequest(new { error = "not_found" });

        if (request is not
            {
                Direction: { } direction, AllowedRoleIds: { } allowed, BlockedRoleIds: { } blockedRoles,
                BlockedUserIds: { } blockedUsers, MinMemberAgeMinutes: { } minAge, BlockInvites: { } invites,
                UseWordFilter: { } filter, NameFormat: { } nameFormat
            })
            return BadRequest(new { error = "invalid_request" });

        var options = new BridgeOptions(direction, allowed.Distinct().ToList(), blockedRoles.Distinct().ToList(),
            blockedUsers.Distinct().ToList(), minAge, invites, filter, nameFormat);
        if (BridgeSettings.Validate(options) is { } error)
            return BadRequest(new { error });

        auditContext.RecordBefore(ToResponse(bridge));
        await bridges.SetOptionsAsync(bridge, options);
        var after = ToResponse(bridge);
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    /// <summary>
    ///     Removes a bridge.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="bridgeId">The bridge ID.</param>
    [HttpDelete("{bridgeId:guid}")]
    public async Task<IActionResult> Remove(ulong guildId, Guid bridgeId)
    {
        if (bridges.GetBridge(guildId, bridgeId) is not { } bridge)
            return NotFound("Bridge not found");

        auditContext.RecordBefore(ToResponse(bridge));
        await bridges.RemoveAsync(bridge);
        return Ok();
    }

    private async Task<IActionResult> Change(ulong guildId, Guid bridgeId, Func<ChannelBridge, Task> change)
    {
        if (bridges.GetBridge(guildId, bridgeId) is not { } bridge)
            return NotFound("Bridge not found");

        auditContext.RecordBefore(ToResponse(bridge));
        await change(bridge);
        var after = ToResponse(bridge);
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    private BridgeResponse ToResponse(ChannelBridge bridge) => new()
    {
        BridgeId = bridge.BridgeId,
        ChannelId = bridge.ChannelId,
        ChannelName = client.GetGuild(bridge.GuildId)?.GetChannel(bridge.ChannelId)?.Name ?? "",
        Platform = Modules.Bridge.Common.BridgeProtocol.Other(Modules.Bridge.Common.BridgeProtocol.Discord),
        RemoteGuildId = bridge.RemoteGuildId,
        RemoteGuildName = bridge.RemoteGuildName,
        RemoteChannelId = bridge.RemoteChannelId,
        RemoteChannelName = bridge.RemoteChannelName,
        Paused = bridge.Paused,
        AllowBots = bridge.AllowBots,
        Direction = (BridgeDirection)bridge.Direction,
        AllowedRoleIds = BridgeSettings.ParseIds(bridge.AllowedRoleIds).ToArray(),
        BlockedRoleIds = BridgeSettings.ParseIds(bridge.BlockedRoleIds).ToArray(),
        BlockedUserIds = BridgeSettings.ParseIds(bridge.BlockedUserIds).ToArray(),
        MinMemberAgeMinutes = bridge.MinMemberAgeMinutes,
        BlockInvites = bridge.BlockInvites,
        UseWordFilter = bridge.UseWordFilter,
        NameFormat = bridge.NameFormat,
        PairId = bridge.PairId?.ToString(),
        DateAdded = bridge.DateAdded
    };
}
