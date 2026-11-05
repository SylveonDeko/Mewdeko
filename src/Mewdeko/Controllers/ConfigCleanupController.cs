using Mewdeko.Controllers.Common.UtilityExtras;
using Mewdeko.Modules.Administration.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Previews and applies the cleanup of settings that point at deleted channels or roles for the dashboard.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class ConfigCleanupController(ConfigCleanupService cleanup) : Controller
{
    /// <summary>
    ///     Lists the settings that point at deleted channels or roles without changing anything.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet]
    public async Task<IActionResult> Preview(ulong guildId)
    {
        var findings = await cleanup.ScanAsync(guildId);
        if (findings == null)
            return StatusCode(503, "The server is not fully loaded yet");

        return Ok(findings.Select(f => new CleanupFindingDto
        {
            Feature = f.Target.Feature,
            Setting = f.Target.Column,
            Kind = f.Target.Kind.ToString(),
            MissingId = f.MissingId.ToString(),
            Rows = f.Rows,
            Action = f.Target.Action.ToString()
        }));
    }

    /// <summary>
    ///     Fixes the settings that point at deleted channels or roles.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpPost]
    public async Task<IActionResult> Apply(ulong guildId)
    {
        var findings = await cleanup.ScanAsync(guildId);
        if (findings == null)
            return StatusCode(503, "The server is not fully loaded yet");

        return Ok(new CleanupResultDto
        {
            Fixed = await cleanup.ApplyAsync(guildId, findings)
        });
    }
}
