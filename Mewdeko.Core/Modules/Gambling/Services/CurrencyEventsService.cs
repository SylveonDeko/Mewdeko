using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Core.Modules.Gambling.Common.Events;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Modules.Gambling.Common;
using NLog;

namespace Mewdeko.Modules.Gambling.Services
{
    public class CurrencyEventsService : INService
    {
        private readonly IBotConfigProvider _bc;
        private readonly DiscordSocketClient _client;
        private readonly IBotCredentials _creds;
        private readonly ICurrencyService _cs;
        private readonly DbService _db;

        private readonly ConcurrentDictionary<ulong, ICurrencyEvent> _events =
            new();

        private readonly IHttpClientFactory _http;
        private readonly Logger _log;

        public CurrencyEventsService(DbService db, DiscordSocketClient client,
            IBotCredentials creds, ICurrencyService cs, IBotConfigProvider bc,
            IHttpClientFactory http)
        {
            _db = db;
            _client = client;
            _cs = cs;
            _bc = bc;
            _creds = creds;
            _http = http;
            _log = LogManager.GetCurrentClassLogger();
        }

        public async Task<bool> TryCreateEventAsync(ulong guildId, ulong channelId, CurrencyEvent.Type type,
            EventOptions opts, Func<CurrencyEvent.Type, EventOptions, long, EmbedBuilder> embed)
        {
            var g = _client.GetGuild(guildId);
            var ch = g?.GetChannel(channelId) as SocketTextChannel;
            if (ch == null)
                return false;

            ICurrencyEvent ce;

            if (type == CurrencyEvent.Type.Reaction)
                ce = new ReactionEvent(_client, _cs, _bc, g, ch, opts, embed);
            else if (type == CurrencyEvent.Type.GameStatus)
                ce = new GameStatusEvent(_client, _cs, _bc, g, ch, opts, embed);
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
                    _log.Warn(ex);
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
            public ulong id { get; set; }
            public long Date { get; set; }
        }
    }
}