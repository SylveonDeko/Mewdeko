namespace Mewdeko.Modules.Games.Services;

public class ActivityService(DbService db, GuildSettingsService guildSettings) : INService
{
    public async Task<ulong> GetGameMasterRole(ulong guildId) =>
        (await guildSettings.GetGuildConfig(guildId)).GameMasterRole;

    public async Task GameMasterRoleSet(ulong guildid, ulong role)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildid, set => set);
        gc.GameMasterRole = role;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guildid, gc);
    }
}