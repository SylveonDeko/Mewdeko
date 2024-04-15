using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Mewdeko.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GuildConfig(DbService service) : Controller
{
    [HttpGet("{guildId}")]
    public async Task<IActionResult> GetGuildConfig(ulong guildId)
    {
        try
        {
            await using var uow = service.GetDbContext();
            var guildConfig = await uow.ForGuildId(guildId);

            if (guildConfig == null)
                return NotFound();

            return Ok(guildConfig);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error getting guild config");
            return StatusCode(500);
        }
    }
}