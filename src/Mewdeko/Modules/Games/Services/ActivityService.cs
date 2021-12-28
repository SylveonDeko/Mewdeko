using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Mewdeko.Services;

namespace Mewdeko.Modules.Games.Services;

public class ActivityService : INService
{
    public DbService _db;
    public Mewdeko.Services.Mewdeko Bot;
    private ConcurrentDictionary<ulong, int> GameMasterRoles { get; }

    public ActivityService(DbService db, Mewdeko.Services.Mewdeko bot)
    {
        _db = db;
        Bot = bot;
    }

    public async Task<ulong> GetGameMasterRole(ulong guildId)
    {
        return Bot.AllGuildConfigs.Where(x => x.GuildId == guildId).Select(x => x.GameMasterRole).FirstOrDefault();
    }
    public async Task GameMasterRoleSet(ulong guildid, ulong role)
    {
        using var uow = _db.GetDbContext();
        var gc = uow.GuildConfigs.ForId(guildid, set => set);
        gc.GameMasterRole = role;
        await uow.SaveChangesAsync();
    }
}