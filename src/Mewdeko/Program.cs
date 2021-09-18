using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Mewdeko.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

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
                //_ = Task.Run(async () =>
                //{
                //    await CreateHostBuilder(args).Build().RunAsync();
                //});
            }
            else
            {
                await new ShardsCoordinator()
                    .RunAsync()
                    .ConfigureAwait(false);
                //_ = Task.Run(async () =>
                //{
                //    await CreateHostBuilder(args).Build().RunAsync();
                //});
#if DEBUG
                await new Mewdeko(0, pid)
                    .RunAndBlockAsync();
                //_ = Task.Run(async () =>
                //{
                //    await CreateHostBuilder(args).Build().RunAsync();
                //});
#else
                await Task.Delay(-1);
#endif
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); });
        }
    }
}