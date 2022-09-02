using Mewdeko.Modules.Gambling.Common.Events;
using Serilog;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Gambling.Services;

public class CurrencyEventsService : INService
{
    private readonly DiscordSocketClient _client;
    private readonly GamblingConfigService _configService;
    private readonly ICurrencyService _cs;
    private readonly EventHandler _eventHandler;

    private readonly ConcurrentDictionary<ulong, ICurrencyEvent> _events =
        new();

    public CurrencyEventsService(DiscordSocketClient client, ICurrencyService cs, GamblingConfigService configService,
        EventHandler eventHandler)
    {
        _client = client;
        _cs = cs;
        _configService = configService;
        _eventHandler = eventHandler;
    }

    public async Task<bool> TryCreateEventAsync(ulong guildId, ulong channelId, CurrencyEvent.Type type,
        EventOptions opts, Func<CurrencyEvent.Type, EventOptions, long, EmbedBuilder> embed)
    {
        var g = _client.GetGuild(guildId);
        if (g?.GetChannel(channelId) is not SocketTextChannel ch)
            return false;

        ICurrencyEvent ce;

        switch (type)
        {
            case CurrencyEvent.Type.Reaction:
                ce = new ReactionEvent(_client, _cs, g, ch, opts, _configService.Data, embed, _eventHandler);
                break;
            case CurrencyEvent.Type.GameStatus:
                ce = new GameStatusEvent(_client, _cs, g, ch, opts, embed, _eventHandler);
                break;
            default:
                return false;
        }

        var added = _events.TryAdd(guildId, ce);
        if (!added) return added;
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