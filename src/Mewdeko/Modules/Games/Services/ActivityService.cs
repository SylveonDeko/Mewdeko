using System.Threading.Tasks;

namespace Mewdeko.Modules.Games.Services;

public class ActivityService : INService
{
    private readonly DbService db;
    private readonly GuildSettingsService guildSettings;

    public ActivityService(DbService db, GuildSettingsService guildSettings)
    {
        this.db = db;
        this.guildSettings = guildSettings;
    }

    public async Task<ulong> GetGameMasterRole(ulong guildId) => (await guildSettings.GetGuildConfig(guildId)).GameMasterRole;

    public async Task GameMasterRoleSet(ulong guildid, ulong role)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildid, set => set);
        gc.GameMasterRole = role;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        guildSettings.UpdateGuildConfig(guildid, gc);
    }
}