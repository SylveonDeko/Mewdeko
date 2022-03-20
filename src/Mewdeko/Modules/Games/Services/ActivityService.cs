using Mewdeko.Database;
using Mewdeko.Database.Extensions;

namespace Mewdeko.Modules.Games.Services;

public class ActivityService : INService
{
    private readonly DbService _db;
    private readonly Mewdeko _bot;

    public ActivityService(DbService db, Mewdeko bot)
    {
        _db = db;
        _bot = bot;
    }

    public ulong GetGameMasterRole(ulong guildId) => _bot.AllGuildConfigs[guildId].GameMasterRole;

    public async Task GameMasterRoleSet(ulong guildid, ulong role)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guildid, set => set);
        gc.GameMasterRole = role;
        await uow.SaveChangesAsync();
        _bot.AllGuildConfigs[guildid].GameMasterRole = role;
    }
}