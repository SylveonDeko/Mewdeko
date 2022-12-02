using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.WebApp.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class AfkController : ControllerBase
{
    private readonly DbService db;

    public AfkController(DbService db)
        => this.db = db;

    [HttpGet]
    [ActionName("GetAfkForUser")]
    public async Task<IEnumerable<Afk>> Get(ulong serverId, ulong userId) =>
        (await db.GetDbContext().Afk.ForGuild(serverId)).Where(x => x.UserId == userId);

    [HttpGet]
    [ActionName("IsAfk")]
    public async Task<bool> IsAfk(ulong serverId, ulong userId)
    {
        var result = (await db.GetDbContext().Afk.ForGuild(serverId)).LastOrDefault(x => x.UserId == userId);
        return result is not null && result.Message != "";
    }
}