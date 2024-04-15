using Mewdeko.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AfkController(DbService dbService) : Controller
{
    [HttpGet("{guildId}/{userId}")]
    public async Task<IActionResult> GetAfkStatus(ulong guildId, ulong userId)
    {
        await using var uow = dbService.GetDbContext();
        var afkStatus = await uow.Afk.OrderBy(x => x.DateAdded)
            .LastOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);

        if (afkStatus == null)
            return NotFound();

        return Ok(afkStatus);
    }
}