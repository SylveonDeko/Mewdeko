using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database;

public sealed class MewdekoPostgresContext(string connStr) : MewdekoContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        base.OnConfiguring(optionsBuilder);
        optionsBuilder
            .UseNpgsql(connStr);
    }
}