using System.IO;
using Mewdeko.Services.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services;

public class DbService
{
    private readonly DbContextOptions<MewdekoContext> _migrateOptions;
    private readonly DbContextOptions<MewdekoContext> _options;

    public DbService(IBotCredentials creds)
    {
        var builder = new SqliteConnectionStringBuilder(creds.Db.ConnectionString);
        builder.DataSource = Path.Combine(AppContext.BaseDirectory, builder.DataSource);

        var optionsBuilder = new DbContextOptionsBuilder<MewdekoContext>();
        optionsBuilder.UseSqlite(builder.ToString());
        _options = optionsBuilder.Options;

        optionsBuilder = new DbContextOptionsBuilder<MewdekoContext>();
        optionsBuilder.UseSqlite(builder.ToString());
        _migrateOptions = optionsBuilder.Options;
    }

    public async void Setup()
    {
        await using var context = new MewdekoContext(_options);
        if ((await context.Database.GetPendingMigrationsAsync()).Any())
        {
            var mContext = new MewdekoContext(_migrateOptions);
            await mContext.Database.MigrateAsync();
            await mContext.SaveChangesAsync();
            await mContext.DisposeAsync();
        }

        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL");
        await context.SaveChangesAsync();
    }

    private MewdekoContext GetDbContextInternal()
    {
        var context = new MewdekoContext(_options);
        var conn = context.Database.GetDbConnection();
        conn.OpenAsync();
        using var com = conn.CreateCommand();
        com.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=OFF;";
        com.ExecuteNonQueryAsync();

        return context;
    }

    public IUnitOfWork GetDbContext() => new UnitOfWork(GetDbContextInternal());
}