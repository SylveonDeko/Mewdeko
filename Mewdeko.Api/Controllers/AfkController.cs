using System.ComponentModel.DataAnnotations;
using Mewdeko.Api.Services;
using Mewdeko.Database;
using Mewdeko.Database.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Api.Controllers;

[ApiController]
[Route("api/[controller]/{guildId}/{userId}")]
public class AfkController(MewdekoContext dbContext, RedisCache cache) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> GetAfkStatus(ulong guildId, ulong userId)
    {
        var afkStatus = await cache.RetrieveAfk(guildId, userId);

        if (afkStatus == null)
            return NotFound();

        return Ok(afkStatus);
    }

    [HttpPost("")]
    public async Task<IActionResult> SetAfkStatus(ulong guildId, ulong userId, [Required] string message)
    {
        if (string.IsNullOrEmpty(message))
            return BadRequest();


        var toSave = new Afk
        {
            GuildId = guildId, UserId = userId, Message = message
        };

        await cache.CacheAfk(guildId, userId, toSave);
        await dbContext.Afk.AddAsync(toSave);
        await dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete("")]
    public async Task<IActionResult> DeleteAfkStatus(ulong guildId, ulong userId)
    {


        var afkStatus = await dbContext.Afk.OrderBy(x => x.DateAdded)
            .LastOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);

        if (afkStatus == null || string.IsNullOrEmpty(afkStatus.Message))
            return BadRequest();

        var toAdd = new Afk
        {
            GuildId = guildId, UserId = userId, DateAdded = DateTime.UtcNow, Message = ""
        };

        await cache.ClearAfk(guildId, userId);
        await dbContext.Afk.AddAsync(toAdd);
        await dbContext.SaveChangesAsync();

        return Ok();
    }
}