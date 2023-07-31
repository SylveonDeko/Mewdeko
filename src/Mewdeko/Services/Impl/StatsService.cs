using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using Discord.Commands;
using Humanizer.Bytes;
using Mewdeko.Modules.Utility.Services;
using Serilog;
using Swan.Formatters;

namespace Mewdeko.Services.Impl;

public class StatsService : IStatsService
{
    public readonly DiscordSocketClient Client;
    public readonly IBotCredentials Creds;
    public readonly ICoordinator Coord;
    private readonly IDataCache cache;
    private readonly HttpClient http;
    public const string BotVersion = "7.1";

    private readonly DateTime started;

    public StatsService(
        DiscordSocketClient client, IBotCredentials creds, ICoordinator coord, CommandService cmdServ,
        HttpClient http, IDataCache cache)
    {
        Client = client;
        Creds = creds;
        Coord = coord;
        this.http = http;
        this.cache = cache;
        _ = new DllVersionChecker();
        started = DateTime.UtcNow;
        _ = PostToTopGg();
        _ = PostToStatcord(coord, client, cmdServ);
        client.Ready += SetTopGuilds;
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
                            "server_count", (Coord.GetGuildCount()).ToString()
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

    public Task SetTopGuilds()
    {
        _ = Task.Run(async () =>
        {
            if (Client.ShardId != 0)
                return;

            var periodicTimer = new PeriodicTimer(TimeSpan.FromHours(12));
            // Set it once before executing the 12h loop
            var guilds = await Client.Rest.GetGuildsAsync(true);

            do
            {
                try
                {
                    Log.Information("Updating top guilds");
                    var servers = guilds.OrderByDescending(x => x.ApproximateMemberCount.Value).Take(7).Select(x => new MewdekoPartialGuild()
                    {
                        IconUrl = x.IconId.StartsWith("a_") ? x.IconUrl.Replace(".jpg", ".gif") : x.IconUrl, MemberCount = x.ApproximateMemberCount.Value, Name = x.Name
                    });

                    var serialied = Json.Serialize(servers);
                    await cache.Redis.GetDatabase().StringSetAsync($"{Client.CurrentUser.Id}_topguilds", serialied).ConfigureAwait(false);
                    Log.Information("Updated top guilds");
                }
                catch (Exception e)
                {
                    Log.Error("Failed to update top guilds: {0}", e);
                }
            } while (await periodicTimer.WaitForNextTickAsync());
        });
        return Task.CompletedTask;
    }

    public class MewdekoPartialGuild
    {
        public string Name { get; set; }
        public string IconUrl { get; set; }
        public int MemberCount { get; set; }
    }
}