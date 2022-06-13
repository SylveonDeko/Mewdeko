using Discord.Commands;
using Humanizer.Bytes;
using Mewdeko.Modules.Utility.Services;
using Serilog;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Services.Impl;

public class StatsService : IStatsService
{
    public DiscordSocketClient Client { get; }
    public IHttpClientFactory Factory { get; }
    public IBotCredentials Creds { get; }
    public ICoordinator Coord { get; }
    public const string BOT_VERSION = "6.1";

    private readonly DateTime _started;

    public StatsService(
        DiscordSocketClient client, IHttpClientFactory factory, IBotCredentials creds, ICoordinator coord, CommandService cmdServ)
    {
        Client = client;
        Factory = factory;
        Creds = creds;
        Coord = coord;
        _ = new DllVersionChecker();
        _started = DateTime.UtcNow;
        _ = PostToTopGg();
        _ = PostToStatcord(coord, client, cmdServ);
    }

    public string Library => $"Discord.Net {DllVersionChecker.GetDllVersion()} ";

    public string Heap => ByteSize.FromBytes(Process.GetCurrentProcess().PrivateMemorySize64).Megabytes
        .ToString(CultureInfo.InvariantCulture);

    private TimeSpan GetUptime() => DateTime.UtcNow - _started;

    public string GetUptimeString(string separator = ", ")
    {
        var time = GetUptime();
        return $"{time.Days} days{separator}{time.Hours} hours{separator}{time.Minutes} minutes";
    }

    public async Task PostToStatcord(ICoordinator coord, DiscordSocketClient socketClient, CommandService cmdServ)
    {
        if (string.IsNullOrWhiteSpace(Creds.StatcordKey))
            return;
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync())
        {
            using var content = new StringContent(
                $"{{\n  \"id\": \"{socketClient.CurrentUser.Id}\",\n  \"key\": \"{Creds.StatcordKey}\",\n  \"servers\": \"{coord.GetGuildCount()}\",\n  \"users\": \"{coord.GetUserCount()}\",\n  \"active\":[],\n  \"commands\": \"0\",\n  \"popular\": \"[]\",\n  \"memactive\": \"{ByteSize.FromBytes(Process.GetCurrentProcess().PrivateMemorySize64).Bytes}\",\n  \"memload\": \"0\",\n  \"cpuload\": \"0\",\n  \"bandwidth\": \"0\", \n\"custom1\":  \"{cmdServ.Commands.Count()}\"}}");
            using var client = new HttpClient();
            content.Headers.Clear();
            content.Headers.Add("Content-Type", "application/json");
            await client.PostAsync("https://api.statcord.com/beta/stats", content);
        }
    }
    public async Task PostToTopGg()
    {
        if (Client.ShardId != 0)
            return;

        if (Client.CurrentUser.Id != 752236274261426212)
            return;
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        while (await timer.WaitForNextTickAsync())
        {
            try
            {
                using var http = Factory.CreateClient();
                using var content = new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        {"shard_count", Creds.TotalShards.ToString()},
                        {"shard_id", Client.ShardId.ToString()},
                        {"server_count", Coord.GetGuildCount().ToString()}
                    });
                content.Headers.Clear();
                content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                http.DefaultRequestHeaders.Add("Authorization",
                    Creds.VotesToken);

                using (await http
                             .PostAsync(new Uri($"https://top.gg/api/bots/{Client.CurrentUser.Id}/stats"),
                                 content).ConfigureAwait(false))
                {
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                // ignored
            }
        }
    }
}
