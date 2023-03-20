using System.IO;
using System.Net.Http;
using System.Text;
using Mewdeko.Services.Impl;
using Serilog;

var pid = Environment.ProcessId;
var shardId = 0;
var credentials = new BotCredentials();
if (string.IsNullOrEmpty(credentials.Token))
{
    Log.Error("No token provided. Exiting...");
    return;
}

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
if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "data/Mewdeko.db")))
{
    var uri = new Uri("https://cdn.discordapp.com/attachments/915770282579484693/970711443672543252/Mewdeko.db");
    var client = new HttpClient();
    var response = await client.GetAsync(uri).ConfigureAwait(false);
    var fs = new FileStream(
        Path.Combine(AppContext.BaseDirectory, "data/Mewdeko.db"),
        FileMode.CreateNew);
    await using var _ = fs.ConfigureAwait(false);
    await response.Content.CopyToAsync(fs).ConfigureAwait(false);
}
else
{
    Log.Information("Attempting to migrate database to a less annoying place...");
    var dbPath = Path.Combine(AppContext.BaseDirectory, "data/Mewdeko.db");
    var clientId = Encoding.UTF8.GetString(Convert.FromBase64String(credentials.Token.Split(".")[0]));
    if (Environment.OSVersion.Platform == PlatformID.Unix)
    {
        var folderpath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        try
        {
            if (Directory.Exists(folderpath + "/.local/share/Mewdeko"))
            {
                if (File.Exists(folderpath + $"/.local/share/Mewdeko/{clientId}/data/Mewdeko.db"))
                {
                    Log.Information("Mewdeko folder already exists!");
                }
                else
                {
                    Directory.CreateDirectory(folderpath + $"/.local/share/Mewdeko/{clientId}");
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
                    Log.Information($"Mewdeko folder created! Your database has been migrated to {folderpath}/Mewdeko/{clientId}");
                }
            }
            else
            {
                Directory.CreateDirectory(folderpath + "/.local/share/Mewdeko");
                Directory.CreateDirectory(folderpath + $"/.local/share/Mewdeko/{clientId}");
                Log.Information($"Mewdeko folder created! Your database has been migrated to {folderpath}/Mewdeko/{clientId}");
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
                Log.Information($"Mewdeko folder created! Your database has been migrated to {folderpath}/Mewdeko/{clientId}");
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to create Mewdeko folder! The application may be missing permissions!");
            Console.Read();
        }
    }
}

Environment.SetEnvironmentVariable($"AFK_CACHED_{shardId}", "0");
Log.Information($"Pid: {pid}");

await new Mewdeko.Mewdeko(shardId).RunAndBlockAsync().ConfigureAwait(false);