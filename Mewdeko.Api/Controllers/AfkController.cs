using System.ComponentModel.DataAnnotations;
using Mewdeko.Api.Services;
using Mewdeko.Database;
using Mewdeko.Database.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Api.Controllers;

[ApiController]
[Route("api/[controller]/{guildId}/{userId}")]
public class AfkController(DbService dbService, RedisCache cache) : Controller
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

        await using var uow = dbService.GetDbContext();
        var toSave = new Afk
        {
            GuildId = guildId, UserId = userId, Message = message
        };

        await cache.CacheAfk(guildId, userId, toSave);
        await uow.Afk.AddAsync(toSave);
        await uow.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete("")]
    public async Task<IActionResult> DeleteAfkStatus(ulong guildId, ulong userId)
    {
        await using var uow = dbService.GetDbContext();

        var afkStatus = await uow.Afk.OrderBy(x => x.DateAdded)
            .LastOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);

        if (afkStatus == null || string.IsNullOrEmpty(afkStatus.Message))
            return BadRequest();

        var toAdd = new Afk
        {
            GuildId = guildId, UserId = userId, DateAdded = DateTime.UtcNow, Message = ""
        };

        await cache.ClearAfk(guildId, userId);
        await uow.Afk.AddAsync(toAdd);
        await uow.SaveChangesAsync();

        return Ok();
    }
}