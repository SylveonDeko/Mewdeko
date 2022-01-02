using System;
using System.IO;
using System.Linq;
using Mewdeko.Services.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services;

public class DbService
{
    private readonly DbContextOptions<MewdekoContext> migrateOptions;
    private readonly DbContextOptions<MewdekoContext> options;

    public DbService(IBotCredentials creds)
    {
        var builder = new SqliteConnectionStringBuilder(creds.Db.ConnectionString);
        builder.DataSource = Path.Combine(AppContext.BaseDirectory, builder.DataSource);

        var optionsBuilder = new DbContextOptionsBuilder<MewdekoContext>();
        optionsBuilder.UseSqlite(builder.ToString());
        options = optionsBuilder.Options;

        optionsBuilder = new DbContextOptionsBuilder<MewdekoContext>();
        optionsBuilder.UseSqlite(builder.ToString());
        migrateOptions = optionsBuilder.Options;
    }

    public void Setup()
    {
        using var context = new MewdekoContext(options);
        if (context.Database.GetPendingMigrations().Any())
        {
            var mContext = new MewdekoContext(migrateOptions);
            mContext.Database.Migrate();
            mContext.SaveChanges();
            mContext.Dispose();
        }

        context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL");
        context.SaveChanges();
    }

    private MewdekoContext GetDbContextInternal()
    {
        var context = new MewdekoContext(options);
        context.Database.SetCommandTimeout(60);
        var conn = context.Database.GetDbConnection();
        conn.OpenAsync();
        using var com = conn.CreateCommand();
        com.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=OFF";
        com.ExecuteNonQueryAsync();

        return context;
    }

    public IUnitOfWork GetDbContext() => new UnitOfWork(GetDbContextInternal());
}