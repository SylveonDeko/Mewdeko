using Mewdeko.Modules.Server_Management.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Reads and changes the channels a guild's lockdown never touches for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class LockdownController(
    DiscordShardedClient client,
    ChannelCommandService service,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Gets whether the guild is locked down and which channels and categories lockdown skips.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet("exemptions")]
    public IActionResult GetExemptions(ulong guildId)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");

        return Ok(Build(guild));
    }

    /// <summary>
    ///     Replaces the channels and categories lockdown skips.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The channel and category IDs.</param>
    [HttpPut("exemptions")]
    public async Task<IActionResult> SetExemptions(ulong guildId, [FromBody] LockdownExemptionsRequest request)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return NotFound("Guild not found");

        var ids = request.ChannelIds.Distinct().ToList();
        if (ids.Any(x => guild.GetChannel(x) is null))
            return BadRequest("Channel not found");

        auditContext.RecordBefore(Build(guild));
        await service.SetExemptionsAsync(guildId, ids);
        var after = Build(guild);
        auditContext.RecordAfter(after);
        return Ok(after);
    }

    private object Build(SocketGuild guild)
    {
        return new
        {
            lockedDown = service.IsGuildInLockdown(guild),
            channelIds = service.GetExemptions(guild.Id)
        };
    }
}

/// <summary>
///     Request to replace a guild's lockdown exemptions
/// </summary>
public class LockdownExemptionsRequest
{
    /// <summary>
    ///     The channel and category IDs lockdown skips
    /// </summary>
    public List<ulong> ChannelIds { get; set; } = [];
}
