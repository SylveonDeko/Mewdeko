using Mewdeko.Common.Collections;
using Serilog;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Gambling.Common.Events;

public class ReactionEvent : ICurrencyEvent
{
    private readonly long _amount;
    private readonly ConcurrentHashSet<ulong> _awardedUsers = new();
    private readonly ITextChannel _channel;
    private readonly GamblingConfig _config;
    private readonly ICurrencyService _cs;

    private readonly Func<CurrencyEvent.Type, EventOptions, long, EmbedBuilder> _embedFunc;
    private readonly IGuild _guild;
    private readonly bool _isPotLimited;
    private readonly bool _noRecentlyJoinedServer;
    private readonly EventHandler _eventHandler;
    private readonly EventOptions _opts;
    private readonly Timer _t;
    private readonly Timer _timeout;
    private readonly ConcurrentQueue<ulong> _toAward = new();

    private readonly object _potLock = new();

    private readonly object _stopLock = new();
    private IEmote emote;
    private IUserMessage? msg;

    public ReactionEvent(DiscordSocketClient client, ICurrencyService cs,
        SocketGuild g, ITextChannel ch, EventOptions opt, GamblingConfig config,
        Func<CurrencyEvent.Type, EventOptions, long, EmbedBuilder> embedFunc,
        EventHandler eventHandler)
    {
        _guild = g;
        _cs = cs;
        _amount = opt.Amount;
        PotSize = opt.PotSize;
        _embedFunc = embedFunc;
        _eventHandler = eventHandler;
        _isPotLimited = PotSize > 0;
        _channel = ch;
        _noRecentlyJoinedServer = false;
        _opts = opt;
        _config = config;

        _t = new Timer(OnTimerTick, null, Timeout.InfiniteTimeSpan, TimeSpan.FromSeconds(2));
        if (_opts.Hours > 0)
            _timeout = new Timer(EventTimeout, null, TimeSpan.FromHours(_opts.Hours), Timeout.InfiniteTimeSpan);
    }

    private long PotSize { get; set; }
    public bool Stopped { get; private set; }
    public bool PotEmptied { get; private set; }

    public event Func<ulong, Task> OnEnded;

    public async Task StartEvent()
    {
        if (Emote.TryParse(_config.Currency.Sign, out var result))
            emote = result;
        else
            emote = new Emoji(_config.Currency.Sign);
        msg = await _channel.EmbedAsync(GetEmbed(_opts.PotSize)).ConfigureAwait(false);
        await msg.AddReactionAsync(emote).ConfigureAwait(false);
        _eventHandler.MessageDeleted += OnMessageDeleted;
        _eventHandler.ReactionAdded += HandleReaction;
        _t.Change(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    public async Task StopEvent()
    {
        await Task.Yield();
        lock (_stopLock)
        {
            if (Stopped)
                return;
            Stopped = true;
            _eventHandler.MessageDeleted -= OnMessageDeleted;
            _eventHandler.ReactionAdded -= HandleReaction;
            _t.Change(Timeout.Infinite, Timeout.Infinite);
            _timeout.Change(Timeout.Infinite, Timeout.Infinite);
            try
            {
                var _ = msg.DeleteAsync();
            }
            catch (Exception)
            {
                // ignored
            }

#pragma warning disable CS4014
            OnEnded(_guild.Id);
#pragma warning restore CS4014
        }
    }

    private void EventTimeout(object state)
    {
        var _ = StopEvent();
    }

    private async void OnTimerTick(object state)
    {
        var potEmpty = PotEmptied;
        var toAward = new List<ulong>();
        while (_toAward.TryDequeue(out var x)) toAward.Add(x);

        if (toAward.Count == 0)
            return;

        try
        {
            await _cs.AddBulkAsync(toAward,
                toAward.Select(_ => "Reaction Event"),
                toAward.Select(_ => _amount),
                true).ConfigureAwait(false);

            if (_isPotLimited)
            {
                await msg.ModifyAsync(m => m.Embed = GetEmbed(PotSize).Build(),
                    new RequestOptions { RetryMode = RetryMode.AlwaysRetry }).ConfigureAwait(false);
            }

            Log.Information("Awarded {0} users {1} currency.{2}",
                toAward.Count,
                _amount,
                _isPotLimited ? $" {PotSize} left." : "");

            if (potEmpty)
            {
                var _ = StopEvent();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error adding bulk currency to users");
        }
    }

    private EmbedBuilder GetEmbed(long pot) => _embedFunc(CurrencyEvent.Type.Reaction, _opts, pot);

    private async Task OnMessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> _)
    {
        if (message.Id == msg.Id) await StopEvent().ConfigureAwait(false);
    }

    private Task HandleReaction(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> ch, SocketReaction r)
    {
        _ = Task.Run(() =>
        {
            if (emote.Name != r.Emote.Name)
                return;
            if ((r.User.IsSpecified
                    ? r.User.Value
                    : null) is not IGuildUser gu // no unknown users, as they could be bots, or alts
                || message.Id != msg.Id // same message
                || gu.IsBot // no bots
                || (DateTime.UtcNow - gu.CreatedAt).TotalDays <= 5 // no recently created accounts
                || (_noRecentlyJoinedServer && // if specified, no users who joined the server in the last 24h
                    (gu.JoinedAt == null ||
                     (DateTime.UtcNow - gu.JoinedAt.Value).TotalDays <
                     1))) // and no users for who we don't know when they joined
            {
                return;
            }
            // there has to be money left in the pot
            // and the user wasn't rewarded
            if (_awardedUsers.Add(r.UserId) && TryTakeFromPot())
            {
                _toAward.Enqueue(r.UserId);
                if (_isPotLimited && PotSize < _amount)
                    PotEmptied = true;
            }
        });
        return Task.CompletedTask;
    }

    private bool TryTakeFromPot()
    {
        if (_isPotLimited)
        {
            lock (_potLock)
            {
                if (PotSize < _amount)
                    return false;

                PotSize -= _amount;
                return true;
            }
        }

        return true;
    }
}