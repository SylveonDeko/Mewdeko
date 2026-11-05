using Mewdeko.Modules.Administration.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Checks and re-registers a guild's slash commands for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class SlashDiagnosticsController(
    SlashDiagnosticsService service,
    DiscordShardedClient client,
    IDashboardAuditContext auditContext) : Controller
{
    /// <summary>
    ///     Compares the slash commands Discord has for the guild with what the bot expects.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">A channel to check the bot's permissions in, if any.</param>
    /// <param name="userId">A member whose access to slash commands in that channel to check, if any.</param>
    [HttpGet]
    public async Task<IActionResult> Get(ulong guildId, [FromQuery] ulong? channelId = null,
        [FromQuery] ulong? userId = null)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found");

        IGuildChannel? channel = channelId is { } cid ? guild.GetChannel(cid) : null;
        IGuildUser? member = userId is { } uid ? guild.GetUser(uid) : null;
        return Ok(await service.DiagnoseAsync(guild, channel, member));
    }

    /// <summary>
    ///     Registers the guild's own slash commands with Discord again.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpPost("resync")]
    public async Task<IActionResult> Resync(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found");

        var done = await service.ResyncAsync(guild);
        auditContext.RecordAfter(new
        {
            resynced = done
        });
        return done ? Ok() : StatusCode(502, "Could not register the guild's slash commands");
    }
}
