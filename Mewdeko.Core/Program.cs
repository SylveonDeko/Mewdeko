using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Mewdeko.Core.Services;
using Serilog;

namespace Mewdeko
{
    public sealed class Program
    {
        public static async Task Main(string[] args)
        {
            var pid = System.Environment.ProcessId;

            var shardId = 0;
            if (args.Length == 1)
                int.TryParse(args[0], out shardId);

            LogSetup.SetupLogger(shardId);
            Log.Information($"Pid: {pid}");

            await new Mewdeko(shardId).RunAndBlockAsync();

        }
    }
}