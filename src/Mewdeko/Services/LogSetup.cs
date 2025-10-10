using System.Diagnostics;
using System.Text;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using ILogger = Serilog.ILogger;

namespace Mewdeko.Services;

/// <summary>
///     Class responsible for setting up the logger configuration with detailed database logging.
/// </summary>
public static class LogSetup
{
    /// <summary>
    ///     Sets up the logger configuration with automatic source context detection.
    /// </summary>
    /// <param name="source">The source object associated with the logger (deprecated - use ForContext instead).</param>
    /// <returns>The configured ILogger instance.</returns>
    public static ILogger SetupLogger(object source)
    {
        var logger = Log.Logger = new LoggerConfiguration()
            // Default Microsoft logging
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
            .MinimumLevel.Override("System", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Error)

            // Database specific logging
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Transaction", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Connection", LogEventLevel.Error)
            .MinimumLevel.Override("Npgsql", LogEventLevel.Debug)
            .MinimumLevel.Override("Npgsql.Command", LogEventLevel.Debug)
            .MinimumLevel.Override("Npgsql.Connection", LogEventLevel.Debug)
            .MinimumLevel.Override("ZiggyCreatures.Caching.Fusion", LogEventLevel.Warning)

            // Enrichers
            .Enrich.FromLogContext()
            .Enrich.WithProperty("LogSource", source)

            // Output configuration with shortened SourceContext
            .WriteTo.Console(
                LogEventLevel.Information,
                theme: AnsiConsoleTheme.Code,
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {SourceContext:l}: {Message:lj}{NewLine}{Exception}")
            .CreateBootstrapLogger();

        Console.OutputEncoding = Encoding.UTF8;

        return logger;
    }

    /// <summary>
    ///     Creates a logger with automatic source context from the calling type.
    /// </summary>
    /// <typeparam name="T">The type to use as source context.</typeparam>
    /// <returns>A logger configured with the specified type as source context.</returns>
    public static ILogger CreateLogger<T>() => Log.ForContext<T>();

    /// <summary>
    ///     Creates a logger with automatic source context from the specified type.
    /// </summary>
    /// <param name="sourceType">The type to use as source context.</param>
    /// <returns>A logger configured with the specified type as source context.</returns>
    public static ILogger CreateLogger(Type sourceType) => Log.ForContext(sourceType);

    /// <summary>
    ///     Creates a logger with automatic source context detection from the calling class.
    ///     Uses stack trace inspection to determine the calling type.
    /// </summary>
    /// <returns>A logger configured with the calling type as source context.</returns>
    public static ILogger CreateLogger()
    {
        var frame = new StackFrame(1);
        var method = frame.GetMethod();
        var type = method?.DeclaringType ?? typeof(object);
        return Log.ForContext(type);
    }

    /// <summary>
    ///     Configures a LoggerConfiguration with all the standard settings for use with Microsoft.Extensions.Logging.
    /// </summary>
    /// <param name="loggerConfiguration">The logger configuration to set up.</param>
    /// <param name="sentryDsn">Optional Sentry DSN for error tracking.</param>
    public static void ConfigureLogger(LoggerConfiguration loggerConfiguration, string sentryDsn = null)
    {
        loggerConfiguration
            // Default Microsoft logging
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
            .MinimumLevel.Override("System", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Error)

            // Database specific logging
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Transaction", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Connection", LogEventLevel.Error)
            .MinimumLevel.Override("Npgsql", LogEventLevel.Debug)
            .MinimumLevel.Override("Npgsql.Command", LogEventLevel.Debug)
            .MinimumLevel.Override("Npgsql.Connection", LogEventLevel.Debug)
            .MinimumLevel.Override("ZiggyCreatures.Caching.Fusion", LogEventLevel.Warning)

            // Enrichers
            .Enrich.FromLogContext()

            // Output configuration with shortened SourceContext
            .WriteTo.Console(
                LogEventLevel.Information,
                theme: AnsiConsoleTheme.Code,
                outputTemplate:
                "{Timestamp:HH:mm:ss} [{Level:u3}] {SourceContext:l}: {Message:lj}{NewLine}{Exception}");

        // Add Sentry if DSN is provided
        if (!string.IsNullOrWhiteSpace(sentryDsn))
        {
            loggerConfiguration.WriteTo.Sentry(o =>
            {
                o.Dsn = sentryDsn;
                o.MinimumBreadcrumbLevel = LogEventLevel.Debug;
                o.MinimumEventLevel = LogEventLevel.Error;
                o.AttachStacktrace = true;
                o.SendDefaultPii = false;
                o.MaxBreadcrumbs = 100;
            });
        }

        Console.OutputEncoding = Encoding.UTF8;
    }
}