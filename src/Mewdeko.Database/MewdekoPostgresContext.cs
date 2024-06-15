using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Mewdeko.Database;

public sealed class MewdekoPostgresContext(string connStr = "") : MewdekoContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        optionsBuilder
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging()
            .EnableServiceProviderCaching()
            .UseNpgsql(connStr);

#if DEBUG
        optionsBuilder.LogTo(Log.Information);
#else
        optionsBuilder.LogTo(Log.Information, LogLevel.Information);
#endif

        base.OnConfiguring(optionsBuilder);
    }
}