using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Microsoft.AspNetCore.Mvc;
using RedisPubSub = Mewdeko.Api.Reimplementations.PubSub.RedisPubSub;

namespace Mewdeko.Api.Controllers;

public class ChatTriggerController : ControllerBase
{
    private readonly DbService db;
    private readonly RedisCache.RedisCache cache;
    private readonly RedisPubSub pubSub;

    public ChatTriggerController(DbService db, RedisCache.RedisCache cache, RedisPubSub pubSub)
    {
        this.db = db;
        this.cache = cache;
        this.pubSub = pubSub;
    }

    [HttpGet("~/chattriggers/{guildId}")]
    public async Task<IEnumerable<ChatTriggers>> Get(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var result = await uow.ChatTriggers.ForId(guildId);
        return result;
    }

    [HttpPut("~/chattriggers")]
    public async Task<ActionResult> Put(ChatTriggers trigger)
    {
        pubSub.Pub(new Reimplementations.PubSub.TypedKey<ChatTriggers>("chat_triggers"), trigger);
        return Ok();
    }
}