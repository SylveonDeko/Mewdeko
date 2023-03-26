using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.WebApp.Controllers;

public class GuildController : Controller
{
    private readonly RedisCache.RedisCache cache;
    private readonly DbService dbService;

    public GuildController(RedisCache.RedisCache cache, DbService dbService)
    {
        this.cache = cache;
        this.dbService = dbService;
    }


    [HttpGet("~/guild/{id}")]
    public async Task<GuildConfig> GetGuild(ulong id)
    {
        var config = await cache.GetGuildConfig(id);
        if (config is { })
            return config;
        await using var uow = dbService.GetDbContext();
        var newConfig = await uow.ForGuildId(id);
        cache.SetGuildConfig(id, newConfig);
        return newConfig;
    }

    [HttpPost("~/guild/{id}/prefix")]
    public async Task<ActionResult> SetPrefix(ulong id, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return BadRequest("Prefix cannot be empty");
        var config = await GetGuild(id);
        config.Prefix = prefix;
        cache.SetGuildConfig(id, config);
        await using var uow = dbService.GetDbContext();
        uow.GuildConfigs.Update(config);
        await uow.SaveChangesAsync();
        return Ok();
    }
}