using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Mewdeko.Database;

/// <summary>
///     Context used only for data migration to psql
/// </summary>
public sealed class MewdekoSqLiteContext : MewdekoContext
{
    private readonly string connectionString;

    /// <summary>
    ///     Context used for only data migration to psql
    /// </summary>
    /// <param name="connectionString"></param>
    /// <param name="commandTimeout"></param>
    public MewdekoSqLiteContext(string connectionString = "Data Source=data/Mewdeko.db", int commandTimeout = 60) :
        base(new DbContextOptions<MewdekoSqLiteContext>())
    {
        this.connectionString = connectionString;
        Database.SetCommandTimeout(commandTimeout);
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        var builder = new SqliteConnectionStringBuilder(connectionString);
        builder.DataSource = Path.Combine(AppContext.BaseDirectory, builder.DataSource);
        optionsBuilder.UseSqlite(builder.ToString());
        optionsBuilder.LogTo(Log.Information, LogLevel.Error);
        optionsBuilder.EnableDetailedErrors();
        optionsBuilder.EnableSensitiveDataLogging();
    }
}