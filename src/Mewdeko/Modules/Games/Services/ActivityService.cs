using System.Collections.Concurrent;
using Mewdeko._Extensions;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;

namespace Mewdeko.Modules.Games.Services;

public class ActivityService : INService
{
    private readonly DbService _db;
    private ConcurrentDictionary<ulong, ulong> GameMasterRoles { get; }

    public ActivityService(DbService db, Mewdeko bot)
    {
        _db = db;
        GameMasterRoles = bot.AllGuildConfigs
            .ToDictionary(x => x.GuildId, x => x.GameMasterRole)
            .ToConcurrent();
    }

    public Task<ulong> GetGameMasterRole(ulong guildId)
    {
        GameMasterRoles.TryGetValue(guildId, out var snum);
        return Task.FromResult(snum);
    }
    public async Task GameMasterRoleSet(ulong guildid, ulong role)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guildid, set => set);
        gc.GameMasterRole = role;
        await uow.SaveChangesAsync();
        GameMasterRoles.AddOrUpdate(guildid, role, (_, _) => role);
    }
}