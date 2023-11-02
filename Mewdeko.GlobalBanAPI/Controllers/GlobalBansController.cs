using System.Text.Json;
using Mewdeko.GlobalBanAPI.Common;
using Mewdeko.GlobalBanAPI.DbStuff;
using Mewdeko.GlobalBanAPI.DbStuff.Extensions;
using Mewdeko.GlobalBanAPI.DbStuff.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;

namespace Mewdeko.GlobalBanAPI.Controllers;

[ApiController]
[Route("/")]
[EnableRateLimiting("fixed")]
public class GlobalBansController(DbService dbService, IConnectionMultiplexer multiplexer) : ControllerBase
{
    [HttpGet("GetAllBans", Name = "GetAllBans")]
    [EnableRateLimiting("fixed")]
    public async Task<IEnumerable<GlobalBans>> Get()
    {
        var db = multiplexer.GetDatabase();
        var allBans = await db.StringGetAsync("allBans");
        return allBans.HasValue ? JsonSerializer.Deserialize<IEnumerable<GlobalBans>>(allBans) : new List<GlobalBans>();
    }

    [HttpPut("AddBan", Name = "AddBan")]
    [ApiKeyAuthorize]
    [EnableRateLimiting("fixed")]
    public async Task<IActionResult> Put([FromBody] PartialGlobalBan partialBan)
    {
        if (string.IsNullOrEmpty(partialBan.Proof))
        {
            return BadRequest("Proof is required");
        }

        if (string.IsNullOrEmpty(partialBan.Reason))
        {
            return BadRequest("Reason is required");
        }

        if (partialBan.UserId == 0)
        {
            return BadRequest("UserId is required");
        }

        if (partialBan.AddedBy == 0)
        {
            return BadRequest("AddedBy is required");
        }

        if (partialBan.Type == GbType.None)
        {
            return BadRequest("Type is required");
        }

        if (partialBan.RecommendedAction == GbActionType.None)
        {
            return BadRequest("RecommendedAction is required");
        }

        var ban = new GlobalBans
        {
            AddedBy = partialBan.AddedBy,
            Reason = partialBan.Reason,
            Proof = partialBan.Proof,
            RecommendedAction = partialBan.RecommendedAction,
            Type = partialBan.Type,
            UserId = partialBan.UserId
        };
        await using var uow = dbService.GetDbContext();
        await uow.GlobalBans.AddAsync(ban);
        await uow.SaveChangesAsync();
        var allBans = uow.GlobalBans.AllGlobalBans();
        var db = multiplexer.GetDatabase();
        await db.StringSetAsync("allBans", JsonSerializer.Serialize(allBans));
        return Ok();
    }

    [HttpPatch("ApproveBan", Name = "ApproveBan")]
    [ApiKeyAuthorize]
    [EnableRateLimiting("fixed")]
    public async Task<IActionResult> Patch([FromBody] int id)
    {
        if (id == 0)
        {
            return BadRequest("Id is required");
        }

        await using var uow = dbService.GetDbContext();
        var ban = uow.GlobalBans.GetGlobalBanById(id);
        if (ban is null)
        {
            return BadRequest("Ban not found");
        }

        ban.IsApproved = true;
        uow.GlobalBans.Update(ban);
        await uow.SaveChangesAsync();
        var allBans = uow.GlobalBans.AllGlobalBans();
        var db = multiplexer.GetDatabase();
        await db.StringSetAsync("allBans", JsonSerializer.Serialize(allBans));
        return Ok();
    }

    [HttpPatch("DenyBan", Name = "DenyBan")]
    [ApiKeyAuthorize]
    [EnableRateLimiting("fixed")]
    public async Task<IActionResult> PatchDeny([FromBody] int id)
    {
        if (id == 0)
        {
            return BadRequest("Id is required");
        }

        await using var uow = dbService.GetDbContext();
        var ban = uow.GlobalBans.GetGlobalBanById(id);
        if (ban is null)
        {
            return BadRequest("Ban not found");
        }

        uow.GlobalBans.Remove(ban);
        await uow.SaveChangesAsync();
        var allBans = uow.GlobalBans.AllGlobalBans();
        var db = multiplexer.GetDatabase();
        await db.StringSetAsync("allBans", JsonSerializer.Serialize(allBans));
        return Ok();
    }
}