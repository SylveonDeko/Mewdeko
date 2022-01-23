using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using Discord;
using Discord.WebSocket;
using Humanizer.Bytes;
using Mewdeko.Modules.Utility.Services;
using Serilog;

namespace Mewdeko.Services.Impl;

public class StatsService : IStatsService
{
    public const string BotVersion = "3.83";

    private readonly DiscordSocketClient _client;
    private readonly DateTime _started;

    private long _textChannels;
    private long _voiceChannels;

    public StatsService(DiscordSocketClient client, CommandHandler cmdHandler,
        IBotCredentials creds, Mewdeko Mewdeko, IDataCache cache, IHttpClientFactory factory, ICoordinator coord)
    {
        _client = client;
        _ = new DllVersionChecker();

        _started = DateTime.UtcNow;

        _client.ChannelCreated += c =>
        {
            var _ = Task.Run(() =>
            {
                if (c is ITextChannel)
                    Interlocked.Increment(ref _textChannels);
                else if (c is IVoiceChannel)
                    Interlocked.Increment(ref _voiceChannels);
            });

            return Task.CompletedTask;
        };

        _client.ChannelDestroyed += c =>
        {
            var _ = Task.Run(() =>
            {
                if (c is ITextChannel)
                    Interlocked.Decrement(ref _textChannels);
                else if (c is IVoiceChannel)
                    Interlocked.Decrement(ref _voiceChannels);
            });

            return Task.CompletedTask;
        };

        _client.GuildAvailable += g =>
        {
            var _ = Task.Run(() =>
            {
                var tc = g.Channels.Count(cx => cx is ITextChannel);
                var vc = g.Channels.Count - tc;
                Interlocked.Add(ref _textChannels, tc);
                Interlocked.Add(ref _voiceChannels, vc);
            });
            return Task.CompletedTask;
        };

        _client.JoinedGuild += g =>
        {
            var _ = Task.Run(() =>
            {
                var tc = g.Channels.Count(cx => cx is ITextChannel);
                var vc = g.Channels.Count - tc;
                Interlocked.Add(ref _textChannels, tc);
                Interlocked.Add(ref _voiceChannels, vc);
            });
            return Task.CompletedTask;
        };

        _client.GuildUnavailable += g =>
        {
            var _ = Task.Run(() =>
            {
                var tc = g.Channels.Count(cx => cx is ITextChannel);
                var vc = g.Channels.Count - tc;
                Interlocked.Add(ref _textChannels, -tc);
                Interlocked.Add(ref _voiceChannels, -vc);
            });

            return Task.CompletedTask;
        };

        _client.LeftGuild += g =>
        {
            var _ = Task.Run(() =>
            {
                var tc = g.Channels.Count(cx => cx is ITextChannel);
                var vc = g.Channels.Count - tc;
                Interlocked.Add(ref _textChannels, -tc);
                Interlocked.Add(ref _voiceChannels, -vc);
            });

            return Task.CompletedTask;
        };

        if (_client.ShardId == 0)

            _ = new Timer(async _ =>
            {
                try
                {
                    using var http = factory.CreateClient();
                    using var content = new FormUrlEncodedContent(
                        new Dictionary<string, string>
                        {
                            {"shard_count", creds.TotalShards.ToString()},
                            {"shard_id", _client.ShardId.ToString()},
                            {"server_count", coord.GetGuildCount().ToString()}
                        });
                    content.Headers.Clear();
                    content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                    http.DefaultRequestHeaders.Add("Authorization",
                        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6Ijc1MjIzNjI3NDI2MTQyNjIxMiIsImJvdCI6dHJ1ZSwiaWF0IjoxNjA3Mzg3MDk4fQ.1VATJIr_WqRImXlx5hywaAV6BVk-V4NzybRo0e-E3T8");

                    using (await http
                                 .PostAsync(new Uri($"https://top.gg/api/bots/{client.CurrentUser.Id}/stats"),
                                     content).ConfigureAwait(false))
var chan = _client.Rest.GetChannelAsync(934661783480832000).Result as RestTextChannel;
chan.SendMessageAsync("Sent count to top.gg!");
                    {
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                    // ignored
                }
            }, null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
    }

    public string Library => $"Discord.Net Labs {DllVersionChecker.GetDllVersion()} ";

    public string Heap => ByteSize.FromBytes(Process.GetCurrentProcess().PrivateMemorySize64).Megabytes
        .ToString(CultureInfo.InvariantCulture);

    public long TextChannels => Interlocked.Read(ref _textChannels);
    public long VoiceChannels => Interlocked.Read(ref _voiceChannels);

    public void Initialize()
    {
        var guilds = _client.Guilds.ToArray();
        _textChannels = guilds.Sum(g => g.Channels.Count(cx => cx is ITextChannel));
        _voiceChannels = guilds.Sum(g => g.Channels.Count(cx => cx is IVoiceChannel));
    }

    private TimeSpan GetUptime() => DateTime.UtcNow - _started;

    public string GetUptimeString(string separator = ", ")
    {
        var time = GetUptime();
        return $"{time.Days} days{separator}{time.Hours} hours{separator}{time.Minutes} minutes";
    }
}
