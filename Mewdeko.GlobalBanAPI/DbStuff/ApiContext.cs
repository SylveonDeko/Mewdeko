using Mewdeko.GlobalBanAPI.DbStuff.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.GlobalBanAPI.DbStuff;

public sealed class ApiContext : DbContext
{
    private readonly string connectionString;

    public ApiContext(string connectionString = "Data Source=ApiData.db", int commandTimeout = 60)
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

    public DbSet<GlobalBans> GlobalBans { get; set; }
    public DbSet<Keys> Keys { get; set; }
}