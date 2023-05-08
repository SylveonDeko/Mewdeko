using System.IO;
using System.Text;
using Mewdeko.Services.Impl;
using Serilog;

var pid = Environment.ProcessId;
var shardId = 0;

if (args.Length > 0)
{
    if (!int.TryParse(args[0], out shardId))
    {
        Console.Error.WriteLine("Invalid first argument (shard id): {0}", args[0]);
        return;
    }

    if (args.Length > 1)
    {
        if (!int.TryParse(args[1], out _))
        {
            Console.Error.WriteLine("Invalid second argument (total shards): {0}", args[1]);
            return;
        }
    }
}

LogSetup.SetupLogger(shardId);
var credentials = new BotCredentials();
if (string.IsNullOrEmpty(credentials.Token))
{
    Console.Error.WriteLine("No token provided. Exiting...");
    return;
}

var tokenPart = credentials.Token.Split(".")[0];
var paddingNeeded = 28 - tokenPart.Length;
if (paddingNeeded > 0 && tokenPart.Length % 4 != 0)
{
    tokenPart = tokenPart.PadRight(28, '=');
}

var clientId = Encoding.UTF8.GetString(Convert.FromBase64String(tokenPart));

Log.Information("Attempting to migrate database to a less annoying place...");
var dbPath = Path.Combine(AppContext.BaseDirectory, "data/Mewdeko.db");
if (Environment.OSVersion.Platform == PlatformID.Unix)
{
    var folderpath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    try
    {
        if (Directory.Exists(folderpath + "/.local/share/Mewdeko"))
        {
            if (File.Exists(folderpath + $"/.local/share/Mewdeko/{clientId}/data/Mewdeko.db"))
            {
                Log.Information("Mewdeko db already exists!");
            }
            else
            {
                Directory.CreateDirectory(folderpath + $"/.local/share/Mewdeko/{clientId}/data");
                File.Copy(dbPath, folderpath + $"/.local/share/Mewdeko/{clientId}/data/Mewdeko.db");
                try
                {
                    File.Copy(dbPath + "-wal", folderpath + $"/.local/share/Mewdeko/{clientId}/data/Mewdeko.db-wal");
                    File.Copy(dbPath + "-shm", folderpath + $"/.local/share/Mewdeko/{clientId}/data/Mewdeko.db-shm");
                }
                catch
                {
                    // ignored, used if the bot didnt shutdown properly and left behind db files
                }

                Log.Information("Mewdeko folder created!");
                Log.Information($"Mewdeko folder created! Your database has been migrated to {folderpath}/.local/share/Mewdeko/Mewdeko/{clientId}/data");
            }
        }
        else
        {
            Directory.CreateDirectory(folderpath + $"/.local/share/Mewdeko/{clientId}/data");
            File.Copy(dbPath, folderpath + $"/.local/share/Mewdeko/{clientId}/data/Mewdeko.db");
            try
            {
                File.Copy(dbPath + "-wal", folderpath + $"/.local/share/Mewdeko/{clientId}/data/Mewdeko.db-wal");
                File.Copy(dbPath + "-shm", folderpath + $"/.local/share/Mewdeko/{clientId}/data/Mewdeko.db-shm");
            }
            catch
            {
                // ignored, used if the bot didnt shutdown properly and left behind db files
            }

            Log.Information($"Mewdeko folder created! Your database has been migrated to {folderpath}/.local/share/Mewdeko/Mewdeko/{clientId}/data");
        }
    }
    catch (Exception e)
    {
        Log.Error(e, "Failed to create Mewdeko folder! The application may be missing permissions!");
        Console.Read();
    }
}
else
{
    var folderpath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    try
    {
        if (Directory.Exists(folderpath + "/Mewdeko"))
        {
            if (File.Exists(folderpath + $"/Mewdeko/{clientId}/data/Mewdeko.db"))
            {
                Log.Information("Mewdeko folder already exists!");
            }
            else
            {
                Directory.CreateDirectory(folderpath + $"/Mewdeko/{clientId}/data");
                File.Copy(dbPath, folderpath + $"/Mewdeko/{clientId}/data/Mewdeko.db");
                try
                {
                    File.Copy(dbPath + "-wal", folderpath + $"/Mewdeko/{clientId}/data/Mewdeko.db-wal");
                    File.Copy(dbPath + "-shm", folderpath + $"/Mewdeko/{clientId}/data/Mewdeko.db-shm");
                }
                catch
                {
                    // ignored, used if the bot didnt shutdown properly and left behind db files
                }

                Log.Information($"Mewdeko folder created! Your database has been migrated to {folderpath}/Mewdeko/{clientId}");
            }
        }
        else
        {
            Directory.CreateDirectory(folderpath + $"/Mewdeko/{clientId}/data");
            File.Copy(dbPath, folderpath + $"/Mewdeko/{clientId}/data/Mewdeko.db");
            try
            {
                File.Copy(dbPath + "-wal", folderpath + $"/Mewdeko/{clientId}/data/Mewdeko.db-wal");
                File.Copy(dbPath + "-shm", folderpath + $"/Mewdeko/{clientId}/data/Mewdeko.db-shm");
            }
            catch
            {
                // ignored, used if the bot didnt shutdown properly and left behind db files
            }

            Log.Information($"Mewdeko folder created! Your database has been migrated to {folderpath}/Mewdeko/{clientId}");
        }
    }
    catch (Exception e)
    {
        Log.Error(e, "Failed to create Mewdeko folder! The application may be missing permissions!");
        Console.Read();
    }
}

Environment.SetEnvironmentVariable($"AFK_CACHED_{shardId}", "0");
Log.Information($"Pid: {pid}");

await new Mewdeko.Mewdeko(shardId).RunAndBlockAsync().ConfigureAwait(false);