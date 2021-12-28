using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Mewdeko._Extensions;
using Mewdeko.Services;

namespace Mewdeko.Modules.Games.Services;

public class ActivityService : INService
{
    private DbService _db;
    private ConcurrentDictionary<ulong, ulong> GameMasterRoles { get; }

    public ActivityService(DbService db, Mewdeko.Services.Mewdeko bot)
    {
        _db = db;
        GameMasterRoles = bot.AllGuildConfigs
            .ToDictionary(x => x.GuildId, x => x.GameMasterRole)
            .ToConcurrent();
    }

    public async Task<ulong> GetGameMasterRole(ulong guildId)
    {
        GameMasterRoles.TryGetValue(guildId, out var snum);
        return snum;
    }
    public async Task GameMasterRoleSet(ulong guildid, ulong role)
    {
        using var uow = _db.GetDbContext();
        var gc = uow.GuildConfigs.ForId(guildid, set => set);
        gc.GameMasterRole = role;
        await uow.SaveChangesAsync();
        GameMasterRoles.AddOrUpdate(guildid, role, (key, old) => role);
    }
}