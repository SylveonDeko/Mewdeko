using System.Threading.Tasks;

namespace Mewdeko.Modules.Games.Services;

public class ActivityService : INService
{
    private readonly DbService _db;
    private readonly GuildSettingsService _guildSettings;

    public ActivityService(DbService db, GuildSettingsService guildSettings)
    {
        _db = db;
        _guildSettings = guildSettings;
    }

    public ulong GetGameMasterRole(ulong guildId) => _guildSettings.GetGuildConfig(guildId).GameMasterRole;

    public async Task GameMasterRoleSet(ulong guildid, ulong role)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guildid, set => set);
        gc.GameMasterRole = role;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _guildSettings.UpdateGuildConfig(guildid, gc);
    }
}