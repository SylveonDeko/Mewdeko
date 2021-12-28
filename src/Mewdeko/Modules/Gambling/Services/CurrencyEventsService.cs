using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Modules.Gambling.Common.Events;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Modules.Gambling.Services;

public class CurrencyEventsService : INService
{
    private readonly DiscordSocketClient _client;
    private readonly GamblingConfigService _configService;
    private readonly IBotCredentials _creds;
    private readonly ICurrencyService _cs;

    private readonly ConcurrentDictionary<ulong, ICurrencyEvent> _events =
        new();

    private readonly IHttpClientFactory _http;

    public CurrencyEventsService(DiscordSocketClient client,
        IBotCredentials creds, ICurrencyService cs,
        IHttpClientFactory http, GamblingConfigService configService)
    {
        _client = client;
        _cs = cs;
        _creds = creds;
        _http = http;
        _configService = configService;

        if (_client.ShardId == 0)
        {
            var t = BotlistUpvoteLoop();
        }
    }

    private async Task BotlistUpvoteLoop()
    {
        if (string.IsNullOrWhiteSpace(_creds.VotesUrl))
            return;
        while (true)
        {
            await Task.Delay(TimeSpan.FromMinutes(30)).ConfigureAwait(false);
            await TriggerVoteCheck().ConfigureAwait(false);
        }
    }

    private async Task TriggerVoteCheck()
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, _creds.VotesUrl);
            if (!string.IsNullOrWhiteSpace(_creds.VotesToken))
                req.Headers.Add("Authorization", _creds.VotesToken);
            using var http = _http.CreateClient();
            using var res = await http.SendAsync(req).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                Log.Warning("Botlist API not reached.");
                return;
            }

            var resStr = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ids = JsonConvert.DeserializeObject<VoteModel[]>(resStr)
                .Select(x => x.User)
                .Distinct();
            await _cs.AddBulkAsync(ids,
                ids.Select(x => "Voted - <https://top.gg/bot/752236274261426212/vote>"),
                ids.Select(x => 50L), true).ConfigureAwait(false);
            Log.Information($"Vote currency given to {ids.Count()} users.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in TriggerVoteCheck");
        }
    }

    public async Task<bool> TryCreateEventAsync(ulong guildId, ulong channelId, CurrencyEvent.Type type,
        EventOptions opts, Func<CurrencyEvent.Type, EventOptions, long, EmbedBuilder> embed)
    {
        var g = _client.GetGuild(guildId);
        if (g?.GetChannel(channelId) is not SocketTextChannel ch)
            return false;

        ICurrencyEvent ce;

        if (type == CurrencyEvent.Type.Reaction)
            ce = new ReactionEvent(_client, _cs, g, ch, opts, _configService.Data, embed);
        else if (type == CurrencyEvent.Type.GameStatus)
            ce = new GameStatusEvent(_client, _cs, g, ch, opts, embed);
        else
            return false;

        var added = _events.TryAdd(guildId, ce);
        if (added)
            try
            {
                ce.OnEnded += OnEventEnded;
                await ce.StartEvent().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error starting event");
                _events.TryRemove(guildId, out ce);
                return false;
            }

        return added;
    }

    private Task OnEventEnded(ulong gid)
    {
        _events.TryRemove(gid, out _);
        return Task.CompletedTask;
    }

    public class VoteModel
    {
        public ulong User { get; set; }
        public long Date { get; set; }
    }
}