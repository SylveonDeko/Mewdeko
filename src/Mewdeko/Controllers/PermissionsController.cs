using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
/// Controller for setting permissions for commands and triggers
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PermissionsController(PermissionService permissionService, DiscordPermOverrideService dpoService) : Controller
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
}