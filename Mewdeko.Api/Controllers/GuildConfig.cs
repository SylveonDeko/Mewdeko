using Mewdeko.Api.Services;
using Mewdeko.Database.Models;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Mewdeko.Api.Controllers;

[ApiController]
[Route("api/[controller]/{guildId}")]
public class GuildConfigController(GuildSettingsService service) : Controller
{
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
            Log.Error(e, "Error getting guild config");
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateGuildConfig(ulong guildId, [FromBody] GuildConfig model)
    {
        try
        {
            await service.UpdateGuildConfig(guildId, model);
            return Ok();
        }
        catch (Exception e)
        {
            Log.Error(e, "Error updating guild config");
            return StatusCode(500);
        }
    }
}