using System.Text;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace Mewdeko.Services
{
    /// <summary>
    /// Class responsible for setting up the logger configuration.
    /// </summary>
    public static class LogSetup
    {
        /// <summary>
        /// Sets up the logger configuration.
        /// </summary>
        /// <param name="source">The source object associated with the logger.</param>
        public static void SetupLogger(object source)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(LogEventLevel.Information,
                    theme: AnsiConsoleTheme.Code,
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] | #{LogSource} | {Message:lj}{NewLine}{Exception}")
                .Enrich.WithProperty("LogSource", source)
                .CreateLogger();

            Console.OutputEncoding = Encoding.UTF8;
        }
    }
}