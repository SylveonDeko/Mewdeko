using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Discord.Commands;
using Humanizer.Bytes;
using Mewdeko.Modules.Utility.Services;
using Serilog;

namespace Mewdeko.Services.Impl;

public class StatsService : IStatsService
{
    public readonly DiscordSocketClient Client;
    public readonly IBotCredentials Creds;
    public readonly ICoordinator Coord;
    private readonly HttpClient http;
    public const string BotVersion = "7.1";

    private readonly DateTime started;

    public StatsService(
        DiscordSocketClient client, IBotCredentials creds, ICoordinator coord, CommandService cmdServ,
        HttpClient http)
    {
        Client = client;
        Creds = creds;
        Coord = coord;
        this.http = http;
        _ = new DllVersionChecker();
        started = DateTime.UtcNow;
        _ = PostToTopGg();
        _ = PostToStatcord(coord, client, cmdServ);
    }

    public string Library => $"Discord.Net {DllVersionChecker.GetDllVersion()} ";

    public string Heap => ByteSize.FromBytes(Process.GetCurrentProcess().PrivateMemorySize64).Megabytes
        .ToString(CultureInfo.InvariantCulture);

    private TimeSpan GetUptime() => DateTime.UtcNow - started;

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
        while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
        {
            using var content = new StringContent(
                $"{{\n  \"id\": \"{socketClient.CurrentUser.Id}\",\n  \"key\": \"{Creds.StatcordKey}\",\n  \"servers\": \"{coord.GetGuildCount()}\",\n  \"users\": \"{coord.GetUserCount()}\",\n  \"active\":[],\n  \"commands\": \"0\",\n  \"popular\": \"[]\",\n  \"memactive\": \"{ByteSize.FromBytes(Process.GetCurrentProcess().PrivateMemorySize64).Bytes}\",\n  \"memload\": \"0\",\n  \"cpuload\": \"0\",\n  \"bandwidth\": \"0\", \n\"custom1\":  \"{cmdServ.Commands.Count()}\"}}");
            content.Headers.Clear();
            content.Headers.Add("Content-Type", "application/json");
            await http.PostAsync("https://api.statcord.com/beta/stats", content).ConfigureAwait(false);
        }
    }

    public async Task PostToTopGg()
    {
        if (Client.ShardId != 0)
            return;
        //
        // if (Client.CurrentUser.Id != 752236274261426212)
        //     return;
        if (string.IsNullOrEmpty(Creds.VotesToken))
            return;
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
        {
            using var httclient = new HttpClient();
            try
            {
                using var content = new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        {
                            "shard_count", Creds.TotalShards.ToString()
                        },
                        {
                            "shard_id", Client.ShardId.ToString()
                        },
                        {
                            "server_count", (Coord.GetGuildCount() / Creds.TotalShards).ToString()
                        }
                    });
                content.Headers.Clear();
                content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                httclient.DefaultRequestHeaders.Add("Authorization", Creds.VotesToken);

                using (await httclient
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