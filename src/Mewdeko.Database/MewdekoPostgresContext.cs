using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Mewdeko.Database;

public sealed class MewdekoPostgresContext(string connStr = "") : MewdekoContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        base.OnConfiguring(optionsBuilder);
        optionsBuilder
            .LogTo(Log.Information, LogLevel.Error)
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging()
            .EnableServiceProviderCaching()
            .UseNpgsql(connStr);
    }
}