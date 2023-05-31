﻿using System.Text;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace Mewdeko.Services;

public static class LogSetup
{
    public static void SetupLogger(object source)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Verbose)
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