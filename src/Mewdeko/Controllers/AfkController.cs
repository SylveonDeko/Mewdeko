using System.ComponentModel.DataAnnotations;
using Mewdeko.Modules.Afk.Services;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
/// Controller for managing afk stuff for the bot
/// </summary>
/// <param name="afk">The afk service</param>
[ApiController]
[Route("api/[controller]/{guildId}/{userId}")]
public class AfkController(AfkService afk) : Controller
{
    /// <summary>
    /// Gets a users afk status.
    /// </summary>
    /// <param name="guildId">The guildid to check the users afk in</param>
    /// <param name="userId">The user to check afk for</param>
    /// <returns>Returns a <see cref="Afk"/> or a 404 if the user has never had an afk in that guild before.            </returns>
    [HttpGet("")]
    public async Task<IActionResult> GetAfkStatus(ulong guildId, ulong userId)
    {
        var afkStatus = await afk.GetAfk(guildId, userId);

        if (afkStatus == null)
            return NotFound();

        return Ok(afkStatus);
    }

    /// <summary>
    /// Sets a users afk status
    /// </summary>
    /// <param name="guildId">The guild to set the afk for</param>
    /// <param name="userId">The userid of the user to set the afk for</param>
    /// <param name="message">The afk message to set.</param>
    /// <returns>A 200 status code</returns>
    [HttpPost("")]
    public async Task<IActionResult> SetAfkStatus(ulong guildId, ulong userId, [Required] string message)
    {
        if (string.IsNullOrEmpty(message))
            return BadRequest();


        await afk.AfkSet(guildId, userId, message);

        return Ok();
    }

    /// <summary>
    /// Removes a users akf status.
    /// </summary>
    /// <param name="guildId">The guild id to remove a users afk status for</param>
    /// <param name="userId">The userid of the user to remove the afk status for</param>
    /// <returns></returns>
    [HttpDelete("")]
    public async Task<IActionResult> DeleteAfkStatus(ulong guildId, ulong userId)
    {
        await afk.AfkSet(guildId, userId, "");

        return Ok();
    }
}