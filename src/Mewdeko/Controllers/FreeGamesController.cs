using DataModel;
using Mewdeko.Controllers.Common.FreeGames;
using Mewdeko.Modules.Games.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Reads and changes a guild's free game alerts for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class FreeGamesController(
    FreeGameService service,
    DiscordShardedClient client,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Gets the channels that get free game alerts.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet]
    public async Task<IActionResult> Get(ulong guildId)
    {
        return Ok((await service.GetAlertsAsync(guildId)).Select(ToDto));
    }

    /// <summary>
    ///     Adds or changes the alerts of a channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The alert settings.</param>
    [HttpPost]
    public async Task<IActionResult> Save(ulong guildId, [FromBody] FreeGameAlertDto request)
    {
        var guild = client.GetGuild(guildId);
        if (guild?.GetTextChannel(request.ChannelId) is null)
            return BadRequest("Channel not found");
        if (!request.Epic && !request.Steam && !request.Gog)
            return BadRequest("Pick at least one store");
        if (request.RoleId is { } roleId && guild.GetRole(roleId) is null)
            return BadRequest("Role not found");

        auditContext.RecordBefore((await service.GetAlertsAsync(guildId)).Select(ToDto).ToList());
        var saved = await service.SaveAlertAsync(guildId, request.ChannelId, request.RoleId, request.Epic,
            request.Steam, request.Gog);
        auditContext.RecordAfter(ToDto(saved));
        return Ok(ToDto(saved));
    }

    /// <summary>
    ///     Removes the alerts of a channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    [HttpDelete("{channelId}")]
    public async Task<IActionResult> Remove(ulong guildId, ulong channelId)
    {
        auditContext.RecordBefore((await service.GetAlertsAsync(guildId)).Select(ToDto).ToList());
        return await service.RemoveAlertAsync(guildId, channelId) ? Ok() : NotFound("No alerts in that channel");
    }

    /// <summary>
    ///     Gets the games that are free right now.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet("now")]
    public async Task<IActionResult> Now(ulong guildId)
    {
        return Ok(await service.GetCurrentAsync());
    }

    private static FreeGameAlertDto ToDto(FreeGameAlert alert)
    {
        return new FreeGameAlertDto
        {
            ChannelId = alert.ChannelId, RoleId = alert.RoleId, Epic = alert.Epic, Steam = alert.Steam,
            Gog = alert.Gog
        };
    }
}
