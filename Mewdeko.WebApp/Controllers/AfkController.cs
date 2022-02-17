using Microsoft.AspNetCore.Mvc;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;

namespace Mewdeko.WebApp.Controllers;

[ApiController]
[Route("[controller]")]
public class AfkController : ControllerBase
{
    private readonly DbService _db;

    public AfkController(DbService db)
    {
        _db = db;
    }

    [HttpGet(Name = "GetAfkForUser")]
    public IEnumerable<AFK> Get(ulong serverId, ulong userId) =>
        _db.GetDbContext().Afk.ForGuild(serverId).Where(x => x.UserId == userId);

    [HttpGet(Name = "IsAfk")]
    public bool IsAfk(ulong serverId, ulong userId)
    {
        var result = _db.GetDbContext().Afk.ForGuild(serverId).LastOrDefault(x => x.UserId == userId);
        return result is not null && result.Message != "";
    }
}