using Microsoft.EntityFrameworkCore;

namespace Mewdeko.GlobalBanAPI.DbStuff;

public class DbService
{
    public async void Setup()
    {
        var context = new ApiContext();
        var toApply = (await context.Database.GetPendingMigrationsAsync().ConfigureAwait(false)).ToList();
        if (toApply.Count != 0)
        {
            await context.Database.MigrateAsync().ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL").ConfigureAwait(false);

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private static ApiContext GetDbContextInternal()
    {
        var context = new ApiContext();
        var conn = context.Database.GetDbConnection();
        conn.OpenAsync();
        using var com = conn.CreateCommand();
        com.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=OFF;";
        com.ExecuteNonQueryAsync();
        return context;
    }

    public ApiContext GetDbContext() => GetDbContextInternal();
}