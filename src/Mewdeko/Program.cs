using System;
using System.Threading.Tasks;
using Mewdeko;
using Mewdeko.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;

var pid = System.Environment.ProcessId;

var shardId = 0;
int? totalShards = null; // 0 to read from creds.yml
if (args.Length > 0)
{
    if (!int.TryParse(args[0], out shardId))
    {
        Console.Error.WriteLine("Invalid first argument (shard id): {0}", args[0]);
        return;
    }

    if (args.Length > 1)
    {
        if (!int.TryParse(args[1], out var shardCount))
        {
            Console.Error.WriteLine("Invalid second argument (total shards): {0}", args[1]);
            return;
        }

        totalShards = shardCount;
    }
}



LogSetup.SetupLogger(shardId);
Log.Information($"Pid: {pid}");
// _ = Task.Run(() =>
// {
//     CreateHostBuilder(args).Build().Run();
// });
await new Mewdeko.Services.Mewdeko(shardId).RunAndBlockAsync();

// IHostBuilder CreateHostBuilder(string[] args) =>
//     Host.CreateDefaultBuilder(args)
//         .ConfigureWebHostDefaults(webBuilder =>
//         {
//             webBuilder.UseStartup<Startup>();
//         });