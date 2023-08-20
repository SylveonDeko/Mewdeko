using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database;

public sealed class MewdekoSqLiteContext : MewdekoContext
{
    private readonly string connectionString;

    public MewdekoSqLiteContext(string connectionString = "Data Source=data/Mewdeko.db", int commandTimeout = 60)
    {
        this.connectionString = connectionString;
        Database.SetCommandTimeout(commandTimeout);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        var builder = new SqliteConnectionStringBuilder(connectionString);
        builder.DataSource = Path.Combine(AppContext.BaseDirectory, builder.DataSource);
        optionsBuilder.UseSqlite(builder.ToString());
    }
}