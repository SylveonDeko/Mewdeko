using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Mewdeko.Core.Services;

namespace Mewdeko
{
    public sealed class Program
    {
        [Obsolete]
        public static async Task Main(string[] args)
        {
            var pid = Process.GetCurrentProcess().Id;
            Console.WriteLine($"Pid: {pid}");
            if (args.Length == 2
                && int.TryParse(args[0], out var shardId)
                && int.TryParse(args[1], out var parentProcessId))
            {
                await new Mewdeko(shardId, parentProcessId == 0 ? pid : parentProcessId)
                    .RunAndBlockAsync();
            }
            else
            {
                await new ShardsCoordinator()
                    .RunAsync()
                    .ConfigureAwait(false);
#if DEBUG
                await new Mewdeko(0, pid)
                    .RunAndBlockAsync();
#else
                await Task.Delay(-1);
#endif
            }
        }
    }
}