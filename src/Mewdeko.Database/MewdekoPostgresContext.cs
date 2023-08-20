using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database;

public sealed class MewdekoPostgresContext : MewdekoContext
{
    private readonly string connStr;

    public MewdekoPostgresContext(string connStr)
    {
        this.connStr = connStr;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        base.OnConfiguring(optionsBuilder);
        optionsBuilder
            .UseNpgsql(connStr);
    }
}