using System.Reflection;
using System.Text;
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

    public DbService(int shardCount, string token)
    {
        LinqToDBForEFTools.Initialize();
        var folderpath = Environment.GetFolderPath(Environment.OSVersion.Platform == PlatformID.Unix
            ? Environment.SpecialFolder.UserProfile
            : Environment.SpecialFolder.ApplicationData);
        var tokenPart = token.Split(".")[0];
        var paddingNeeded = 28 - tokenPart.Length;
        if (paddingNeeded > 0)
        {
            tokenPart = tokenPart.PadRight(28, '=');
        }

        var clientId = Encoding.UTF8.GetString(Convert.FromBase64String(tokenPart));
        var builder = new SqliteConnectionStringBuilder("Data Source=data/Mewdeko.db");
        if (shardCount > 1)
        {
            builder.DataSource = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Mewdeko.db");
        }
        else
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
                builder.DataSource = builder.DataSource = folderpath + $"/.local/share/Mewdeko/{clientId}/data/Mewdeko.db";
            else
                builder.DataSource = builder.DataSource = folderpath + $"/Mewdeko/{clientId}/data/Mewdeko.db";
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