using DataModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing guild configs via the api
/// </summary>
/// <param name="service"></param>
/// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
/// <param name="logger">The logger instance for structured logging.</param>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class GuildConfigController(
    GuildSettingsService service,
    IDashboardAuditContext auditContext,
    ILogger<GuildConfigController> logger) : Controller
{
    /// <summary>
    ///     Gets a guild config
    /// </summary>
    /// <param name="guildId">The guildid to get a config for</param>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> GetGuildConfig(ulong guildId)
    {
        try
        {
            var config = await service.GetGuildConfig(guildId);
            return Ok(config);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error getting guild config");
            return StatusCode(500);
        }
    }

    /// <summary>
    ///     Updates a guild config from the provided json and guildid
    /// </summary>
    /// <param name="guildId">The guildid to update a config for</param>
    /// <param name="model">The json body of the model to update</param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> UpdateGuildConfig(ulong guildId, [FromBody] GuildConfig model)
    {
        try
        {
            logger.LogInformation(guildId.ToString());
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            auditContext.RecordBefore(await service.GetGuildConfig(guildId));
            await service.UpdateGuildConfig(guildId, model);
            auditContext.RecordAfter(model);
            return Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating guild config");
            return StatusCode(500);
        }
    }
}