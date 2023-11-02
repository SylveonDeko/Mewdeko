using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Administration.Services;

public class ServerRecoveryService : INService
{
    private readonly DbService db;

    public ServerRecoveryService(DbService db)
    {
        this.db = db;
    }


    public async Task<(bool, ServerRecoveryStore)> RecoveryIsSetup(ulong guildId)
    {
        await using var uow = db.GetDbContext();

        var store = await uow.ServerRecoveryStore.FirstOrDefaultAsync(x => x.GuildId == guildId);
        return (store != null, store);
    }

    public async Task SetupRecovery(ulong guildId, string recoveryKey, string twoFactorKey)
    {
        await using var uow = db.GetDbContext();

        var toAdd = new ServerRecoveryStore
        {
            GuildId = guildId, RecoveryKey = recoveryKey, TwoFactorKey = twoFactorKey
        };

        await uow.ServerRecoveryStore.AddAsync(toAdd);
        await uow.SaveChangesAsync();
    }

    public async Task ClearRecoverySetup(ServerRecoveryStore serverRecoveryStore)
    {
        await using var uow = db.GetDbContext();
        uow.ServerRecoveryStore.Remove(serverRecoveryStore);
        await uow.SaveChangesAsync();
    }
}