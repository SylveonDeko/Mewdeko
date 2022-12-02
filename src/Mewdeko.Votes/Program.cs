using Mewdeko.Votes.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Mewdeko.Votes;

public class Program
{
    public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        LogSetup.SetupLogger("Votes");
        return Host.CreateDefaultBuilder(args).ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
    }
}