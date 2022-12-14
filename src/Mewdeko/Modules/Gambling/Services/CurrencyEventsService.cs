using System.Threading.Tasks;
using Mewdeko.Modules.Gambling.Common.Events;
using Serilog;

namespace Mewdeko.Modules.Gambling.Services;

public class CurrencyEventsService : INService
{
    private readonly DiscordSocketClient client;
    private readonly GamblingConfigService configService;
    private readonly ICurrencyService cs;
    private readonly EventHandler eventHandler;

    private readonly ConcurrentDictionary<ulong, ICurrencyEvent> events =
        new();

    public CurrencyEventsService(DiscordSocketClient client, ICurrencyService cs, GamblingConfigService configService,
        EventHandler eventHandler)
    {
        this.client = client;
        this.cs = cs;
        this.configService = configService;
        this.eventHandler = eventHandler;
    }

    public async Task<bool> TryCreateEventAsync(ulong guildId, ulong channelId, CurrencyEvent.Type type,
        EventOptions opts, Func<CurrencyEvent.Type, EventOptions, long, EmbedBuilder> embed)
    {
        var g = client.GetGuild(guildId);
        if (g?.GetChannel(channelId) is not SocketTextChannel ch)
            return false;

        ICurrencyEvent ce;

        switch (type)
        {
            case CurrencyEvent.Type.Reaction:
                ce = new ReactionEvent(cs, g, ch, opts, configService.Data, embed, eventHandler);
                break;
            case CurrencyEvent.Type.GameStatus:
                ce = new GameStatusEvent(client, cs, g, ch, opts, embed, eventHandler);
                break;
            default:
                return false;
        }

        var added = events.TryAdd(guildId, ce);
        if (!added) return added;
        try
        {
            ce.OnEnded += OnEventEnded;
            await ce.StartEvent().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error starting event");
            events.TryRemove(guildId, out ce);
            return false;
        }

        return added;
    }

    private Task OnEventEnded(ulong gid)
    {
        events.TryRemove(gid, out _);
        return Task.CompletedTask;
    }

    public class VoteModel
    {
        public ulong User { get; set; }
        public long Date { get; set; }
    }
}