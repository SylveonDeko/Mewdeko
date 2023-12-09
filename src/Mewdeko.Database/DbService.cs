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
    private readonly bool usePostgres;
    private readonly string connectionString;

    public DbService(int shardCount, string token, bool usePostgres, string psqlConnection = null)
    {
        this.usePostgres = usePostgres;
        LinqToDBForEFTools.Initialize();

        if (usePostgres)
        {
            connectionString = psqlConnection;
        }
        else
        {
            var folderpath = Environment.GetFolderPath(Environment.OSVersion.Platform == PlatformID.Unix
                ? Environment.SpecialFolder.UserProfile
                : Environment.SpecialFolder.ApplicationData);
            var tokenPart = token.Split(".")[0];
            var paddingNeeded = 28 - tokenPart.Length;
            if (paddingNeeded > 0 && tokenPart.Length % 4 != 0)
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
                    builder.DataSource = builder.DataSource =
                        folderpath + $"/.local/share/Mewdeko/{clientId}/data/Mewdeko.db";
                else
                    builder.DataSource = builder.DataSource = folderpath + $"/Mewdeko/{clientId}/data/Mewdeko.db";
            }

            connectionString = builder.ToString();
        }
    }

    public async void Setup()
    {
        var context = GetCurrentContext();
        var toApply = (await context.Database.GetPendingMigrationsAsync().ConfigureAwait(false)).ToList();
        if (toApply.Any())
        {
            await context.Database.MigrateAsync().ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);

            var env = Assembly.GetExecutingAssembly();
            var pmhs = env.GetTypes().Where(t => t.GetInterfaces().Any(i => i == typeof(IPostMigrationHandler)))
                .ToList();
            foreach (var id in toApply)
            {
                var pmhToRuns = pmhs.Where(pmh => pmh.GetCustomAttribute<MigrationAttribute>()?.Id == id).ToList();
                foreach (var pmh in pmhToRuns)
                {
                    pmh.GetMethod("PostMigrationHandler")?.Invoke(null, new object[]
                    {
                        id, context
                    });
                }
            }
        }

        if (context.Database.IsSqlite())
        {
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL").ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private MewdekoContext GetCurrentContext()
    {
        if (usePostgres)
        {
            return new MewdekoPostgresContext(connectionString);
        }

        return new MewdekoSqLiteContext(connectionString);
    }

    private MewdekoContext GetDbContextInternal()
    {
        var context = GetCurrentContext();
        if (!context.Database.IsSqlite()) return context;
        var conn = context.Database.GetDbConnection();
        conn.OpenAsync();
        using var com = conn.CreateCommand();
        com.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=OFF;";
        com.ExecuteNonQueryAsync();
        return context;
    }

    public MewdekoContext GetDbContext() => GetDbContextInternal();
}