using System.Reflection;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database;

public class DbService
{
    private readonly DbContextOptions<MewdekoContext> _migrateOptions;
    private readonly DbContextOptions<MewdekoContext> _options;

    public DbService(int shardCount)
    {
        LinqToDBForEFTools.Initialize();

        var builder = new SqliteConnectionStringBuilder("Data Source=data/Mewdeko.db");
        if (shardCount > 1)
        {
            builder.DataSource = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Mewdeko.db");
        }
        else
        {
            builder.DataSource = builder.DataSource = Path.Combine(AppContext.BaseDirectory, builder.DataSource);
        }

        var optionsBuilder = new DbContextOptionsBuilder<MewdekoContext>();
        optionsBuilder.UseSqlite(builder.ToString());
        _options = optionsBuilder.Options;

        optionsBuilder = new DbContextOptionsBuilder<MewdekoContext>();
        optionsBuilder.UseSqlite(builder.ToString());
        _migrateOptions = optionsBuilder.Options;
    }

    public async void Setup()
    {
        var context = new MewdekoContext(_options);
        await using var _ = context.ConfigureAwait(false);
        var toApply = (await context.Database.GetPendingMigrationsAsync().ConfigureAwait(false)).ToList();
        if (toApply.Any())
        {
            var mContext = new MewdekoContext(_migrateOptions);
            await mContext.Database.MigrateAsync().ConfigureAwait(false);
            await mContext.SaveChangesAsync().ConfigureAwait(false);

            var env = Assembly.GetExecutingAssembly();
            var pmhs = env.GetTypes().Where(t => t.GetInterfaces().Any(i => i == typeof(IPostMigrationHandler))).ToList();
            foreach (var id in toApply)
            {
                var pmhToRuns = pmhs?.Where(pmh => pmh.GetCustomAttribute<MigrationAttribute>()?.Id == id).ToList();
                foreach (var pmh in pmhToRuns)
                {
                    pmh.GetMethod("PostMigrationHandler")?.Invoke(null, new object[]
                    {
                        id, mContext
                    });
                }
            }

            await mContext.DisposeAsync().ConfigureAwait(false);
        }

        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL").ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);
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

    public MewdekoContext GetDbContext() => GetDbContextInternal();
}