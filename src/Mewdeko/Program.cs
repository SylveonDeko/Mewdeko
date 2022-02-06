global using System;
global using System.Linq;
global using Mewdeko.Services;
global using Mewdeko;
global using System.Threading.Tasks;
using Serilog;
using System.Threading;

ThreadPool.GetMinThreads(out _, out var completionPortThreads);
ThreadPool.SetMinThreads( 200, completionPortThreads);
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


LogSetup.SetupLogger(0);
Log.Information($"Pid: {pid}");
await new Mewdeko.Services.Mewdeko(0).RunAndBlockAsync();