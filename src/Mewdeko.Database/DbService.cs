using LinqToDB.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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
            builder.DataSource = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Mewdeko.db");
        else

        {
            if (!File.Exists(Path.Combine(AppContext.BaseDirectory, builder.DataSource)))
            {
                var uri = new Uri("https://cdn.discordapp.com/attachments/915770282579484693/946967102680621087/Mewdeko.db");
                var client = new HttpClient();
                var response = client.GetAsync(uri).Result;
                using var fs = new FileStream(
                    Path.Combine(AppContext.BaseDirectory, builder.DataSource), 
                    FileMode.CreateNew);
                response.Content.CopyToAsync(fs);
            }
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

    public MewdekoContext GetDbContext() => GetDbContextInternal();
}