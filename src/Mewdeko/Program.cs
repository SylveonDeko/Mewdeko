using System.IO;
using System.Text;
using Figgle;
using Mewdeko.Services.Impl;
using Serilog;

namespace Mewdeko
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // Display startup message
            Console.WriteLine(FiggleFonts.Ogre.Render("Mewdeko v8"));

            // Extract and validate shard ID
            var shardId = ExtractArgument(args, 0, "shard id");
            if (shardId < 0)
            {
                if (shardId is -1)
                    shardId = 0;
                else
                {
                    Console.WriteLine("Please provide a valid shard id as an argument. Exiting...");
                    return;
                }
            }

            // Setup logger
            LogSetup.SetupLogger(shardId);

            // Load credentials and validate
            var credentials = new BotCredentials();
            if (string.IsNullOrEmpty(credentials.Token))
            {
                throw new ArgumentException("No token provided. Exiting...");
            }

            var clientId = ExtractClientId(credentials.Token);
            var dbFilePath = BuildDbPath(clientId);

            // Perform database migration
            MigrateDatabase(dbFilePath);

            // Setup environment variables and logging
            Environment.SetEnvironmentVariable($"AFK_CACHED_{shardId}", "0");
            Log.Information($"Pid: {Environment.ProcessId}");

            // Start the bot and block until the bot is closed
            await new Mewdeko(shardId).RunAndBlockAsync().ConfigureAwait(false);
        }

        private static int ExtractArgument(IReadOnlyList<string> args, int index, string argName)
        {
            if (args.Count <= index || !int.TryParse(args[index], out var arg))
            {
                return -1; // return -1 for invalid argument
            }

            return arg;
        }

        private static string ExtractClientId(string token)
        {
            var tokenPart = token.Split('.')[0];
            var paddingNeeded = 4 - tokenPart.Length % 4;
            if (paddingNeeded > 0 && paddingNeeded < 4)
            {
                tokenPart = tokenPart.PadRight(tokenPart.Length + paddingNeeded, '=');
            }

            return Encoding.UTF8.GetString(Convert.FromBase64String(tokenPart));
        }

        private static string BuildDbPath(string clientId)
        {
            var folderPath = Environment.GetFolderPath(Environment.OSVersion.Platform == PlatformID.Unix
                ? Environment.SpecialFolder.UserProfile
                : Environment.SpecialFolder.ApplicationData);

            return Path.Combine(folderPath, Environment.OSVersion.Platform == PlatformID.Unix
                ? $".local/share/Mewdeko/{clientId}/data/Mewdeko.db"
                : $"Mewdeko/{clientId}/data/Mewdeko.db");
        }

        private static void MigrateDatabase(string dbFilePath)
        {
            var targetPath = Path.GetDirectoryName(dbFilePath);
            var sourcePath = Path.Combine(AppContext.BaseDirectory, "data", "Mewdeko.db");

            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            if (!File.Exists(dbFilePath))
            {
                File.Copy(sourcePath, dbFilePath);

                Log.Information("Database migrated to {DbFilePath}", dbFilePath);
            }
            else
            {
                Log.Information("Database already exists at {DbFilePath}", dbFilePath);
            }
        }
    }
}