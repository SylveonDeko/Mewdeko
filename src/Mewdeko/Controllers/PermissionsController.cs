using Discord.Commands;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
/// Controller for setting permissions for commands and triggers
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
public class PermissionsController(PermissionService permissionService, DiscordPermOverrideService dpoService, CommandService cmdServ) : Controller
{
    /// <summary>
    /// Gets all dpos for a guild
    /// </summary>
    /// <param name="guildId">the guild to get permissions for</param>
    /// <returns></returns>
    [HttpGet("dpo/{guildId}")]
    public async Task<IActionResult> GetPermissionOverridesForGuildAsync(ulong guildId)
    {
        var overrides = await dpoService.GetAllOverrides(guildId);
        return Ok(overrides);
    }

    /// <summary>
    /// Add a discord permission override
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="commandName"></param>
    /// <param name="permissions"></param>
    /// <returns></returns>
    [HttpPost("dpo/{guildId}")]
    public async Task<IActionResult> AddDpo(ulong guildId, [FromBody] DpoRequest request)
    {
        var com = cmdServ.Search(request.Command);
        if (!com.IsSuccess)
            return BadRequest(com);
        var perms = (GuildPermission)request.Permissions;
        await dpoService.AddOverride(guildId, request.Command, perms);
        return Ok(dpoService);
    }

    /// <summary>
    /// Remove a dpo
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="commandName"></param>
    /// <returns></returns>
    [HttpDelete("dpo/{guildId}/{commandName}")]
    public async Task<IActionResult> RemoveDpo(ulong guildId, string commandName)
    {
        var com = cmdServ.Search(commandName);
        if (!com.IsSuccess)
            return BadRequest(com);
        await dpoService.RemoveOverride(guildId, commandName);
        return Ok();
    }

    /// <summary>
    /// Gets regular permissions for a guild
    /// </summary>
    /// <param name="guildId">the guild to get permissions for</param>
    /// <returns></returns>
    [HttpGet("regular/{guildId}")]
    public async Task<IActionResult> GetPermissionsForGuildAsync(ulong guildId)
    {
        var perms = await permissionService.GetCacheFor(guildId);
        return Ok(perms);
    }

    /// <summary>
    /// E
    /// </summary>
    public class Stupidity
    {
        /// <summary>
        /// E
        /// </summary>
        public string Command { get; set; }
    }

    /// <summary>
    /// E
    /// </summary>
    public class DpoRequest
    {
        /// <summary>
        /// e
        /// </summary>
        public string Command { get; set; }
        /// <summary>
        /// e
        /// </summary>
        public ulong Permissions { get; set; }
    }


}