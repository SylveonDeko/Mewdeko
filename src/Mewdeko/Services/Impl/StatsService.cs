using Discord.Rest;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Discord.WebSocket;
using Humanizer.Bytes;
using Mewdeko.Modules.Utility.Services;
using Serilog;
using System.Collections.Generic;
using System.Net.Http;

namespace Mewdeko.Services.Impl;

public class StatsService : IStatsService
{
    public const string BOT_VERSION = "3.90";

    private readonly DateTime _started;
#pragma warning disable CS0169
    private Timer BotListTimer;
#pragma warning restore CS0169

    public StatsService(
        DiscordSocketClient client, IHttpClientFactory factory, IBotCredentials creds, ICoordinator coord)
    {
        _ = new DllVersionChecker();
        _started = DateTime.UtcNow;
        if (client.ShardId != 0) 
            return;
        
        if (client.CurrentUser.Id != 752236274261426212)
            return;
        BotListTimer = new Timer(async (state) =>
        {
            try
            {
                    
                using var http = factory.CreateClient();
                using var content = new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        {"shard_count", creds.TotalShards.ToString()},
                        {"shard_id", client.ShardId.ToString()},
                        {"server_count", coord.GetGuildCount().ToString()}
                    });
                content.Headers.Clear();
                content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                http.DefaultRequestHeaders.Add("Authorization",
                    "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6Ijc1MjIzNjI3NDI2MTQyNjIxMiIsImJvdCI6dHJ1ZSwiaWF0IjoxNjA3Mzg3MDk4fQ.1VATJIr_WqRImXlx5hywaAV6BVk-V4NzybRo0e-E3T8");

                using (await http
                             .PostAsync(new Uri($"https://top.gg/api/bots/{client.CurrentUser.Id}/stats"),
                                 content).ConfigureAwait(false))
                {
                }
                var chan = client.Rest.GetChannelAsync(934661783480832000).Result as RestTextChannel;
                await chan.SendMessageAsync("Sent count to top.gg!");
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                // ignored
            }
        }, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));
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
}
