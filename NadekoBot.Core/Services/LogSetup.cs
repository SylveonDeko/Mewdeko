using NLog;
using NLog.Config;
using NLog.Targets;

namespace NadekoBot.Core.Services
{
    public static class LogSetup
    {
        public static void SetupLogger(int shardId)
        {
            var logConfig = new LoggingConfiguration();
            var consoleTarget = new ColoredConsoleTarget()
            {
                Layout = shardId + @" ${date:format=HH\:mm\:ss} ${logger:shortName=True} | ${message}"
            };
            logConfig.AddTarget("Console", consoleTarget);

            logConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, consoleTarget));

            LogManager.Configuration = logConfig;
        }
    }
}
