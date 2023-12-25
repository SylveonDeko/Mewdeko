using System.IO;
using System.Text;
using Figgle;
using Mewdeko.Services.Impl;
using Serilog;

// Setup and Initialization
Console.WriteLine(FiggleFonts.Ogre.Render("Mewdeko v8"));

var shardId = ExtractArgument(args, 0, "shard id");
if (shardId < 0)
{
    Console.WriteLine("Please provide a valid shard id as an argument. Exiting...");
    return;
}

LogSetup.SetupLogger(shardId);

var credentials = new BotCredentials();
if (string.IsNullOrEmpty(credentials.Token))
{
    throw new ArgumentException("No token provided. Exiting...");
}

var clientId = ExtractToken(credentials.Token);

// Database Migration
var folderPath = Environment.OSVersion.Platform == PlatformID.Unix
    ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
    : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

MigrateDatabase(clientId, folderPath);

// Final Steps
Environment.SetEnvironmentVariable($"AFK_CACHED_{shardId}", "0");
Log.Information($"Pid: {Environment.ProcessId}");

await new Mewdeko.Mewdeko(shardId).RunAndBlockAsync().ConfigureAwait(false);
return;

// Extract and check arguments
int ExtractArgument(IReadOnlyList<string> args, int index, string argName)
{
    if (args.Count <= index || !int.TryParse(args[index], out var arg))
    {
        return 0;
    }

    return arg;
}

// Extract Token
string ExtractToken(string token)
{
    var tokenPart = token.Split(".")[0];
    var paddingNeeded = 28 - tokenPart.Length;
    if (paddingNeeded > 0 && tokenPart.Length % 4 != 0)
    {
        tokenPart = tokenPart.PadRight(28, '=');
    }

    return Encoding.UTF8.GetString(Convert.FromBase64String(tokenPart));
}

// Migrate database
void MigrateDatabase(string clientId, string folderPath)
{
    var dbPath = Path.Combine(AppContext.BaseDirectory, "data/");
    MigrateData(dbPath, folderPath + $"/.local/share/Mewdeko/{clientId}/data", clientId);
}

// Create directories and copy database files
void MigrateData(string sourcePath, string targetPath, string clientId)
{
    if (!Directory.Exists(targetPath))
    {
        Directory.CreateDirectory(targetPath);
    }

    if (!File.Exists($"{targetPath}/Mewdeko.db"))
    {
        File.Copy(sourcePath, Path.Combine(targetPath, "Mewdeko.db"));
        try
        {
            File.Copy(Path.Combine(sourcePath, "Mewdeko.db-wal"), Path.Combine(targetPath, "Mewdeko.db-wal"));
            File.Copy(Path.Combine(sourcePath, "Mewdeko.db-shm"), Path.Combine(targetPath, "Mewdeko.db-shm"));
        }
        catch
        {
            // ignored, used if the bot didn't shut down properly and left behind db files
        }

        Log.Information("Mewdeko folder created! Your database has been migrated to {TargetPath}", targetPath);
    }
    else
    {
        Log.Information("Mewdeko db already exists!");
    }
}