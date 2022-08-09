using Mewdeko.Common.Collections;
using Serilog;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Gambling.Common.Events;

public class GameStatusEvent : ICurrencyEvent
{
    private readonly long _amount;
    private readonly ConcurrentHashSet<ulong> _awardedUsers = new();
    private readonly ITextChannel _channel;
    private readonly DiscordSocketClient _client;

    private readonly string _code;
    private readonly ICurrencyService _cs;

    private readonly Func<CurrencyEvent.Type, EventOptions, long, EmbedBuilder> _embedFunc;
    private readonly IGuild _guild;
    private readonly bool _isPotLimited;
    private readonly EventOptions _opts;
    private readonly EventHandler _eventHandler;

    private readonly char[] _sneakyGameStatusChars = Enumerable.Range(48, 10)
        .Concat(Enumerable.Range(65, 26))
        .Concat(Enumerable.Range(97, 26))
        .Select(x => (char)x)
        .ToArray();

    private readonly Timer _t;
    private readonly Timer _timeout;
    private readonly ConcurrentQueue<ulong> _toAward = new();

    private readonly object _potLock = new();

    private readonly object _stopLock = new();
    private IUserMessage? msg;

    public GameStatusEvent(DiscordSocketClient client, ICurrencyService cs, SocketGuild g, ITextChannel ch,
        EventOptions opt, Func<CurrencyEvent.Type, EventOptions, long, EmbedBuilder> embedFunc,
        EventHandler eventHandler)
    {
        _client = client;
        _guild = g;
        _cs = cs;
        _amount = opt.Amount;
        PotSize = opt.PotSize;
        _embedFunc = embedFunc;
        _eventHandler = eventHandler;
        _isPotLimited = PotSize > 0;
        _channel = ch;
        _opts = opt;
        // generate code
        _code = new string(_sneakyGameStatusChars.Shuffle().Take(5).ToArray());

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
        msg = await _channel.EmbedAsync(GetEmbed(_opts.PotSize)).ConfigureAwait(false);
        await _client.SetGameAsync(_code).ConfigureAwait(false);
        _eventHandler.MessageDeleted += OnMessageDeleted;
        _eventHandler.MessageReceived += HandleMessage;
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
            _eventHandler.MessageReceived -= HandleMessage;
#pragma warning disable CS4014
            _client.SetGameAsync(null);
#pragma warning restore CS4014
            _t.Change(Timeout.Infinite, Timeout.Infinite);
            _timeout.Change(Timeout.Infinite, Timeout.Infinite);
            try
            {
                _ = msg.DeleteAsync();
            }
            catch
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
                toAward.Select(_ => "GameStatus Event"),
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
            Log.Warning(ex, "Error in OnTimerTick in gamestatusevent");
        }
    }

    private EmbedBuilder GetEmbed(long pot) => _embedFunc(CurrencyEvent.Type.GameStatus, _opts, pot);

    private async Task OnMessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> _)
    {
        if (message.Id == msg.Id) await StopEvent().ConfigureAwait(false);
    }

    private Task HandleMessage(IMessage message)
    {
        _ = Task.Run(async () =>
        {
            if (message.Author is not IGuildUser gu // no unknown users, as they could be bots, or alts
                || gu.IsBot // no bots
                || message.Content != _code // code has to be the same
                || (DateTime.UtcNow - gu.CreatedAt).TotalDays <= 5) // no recently created accounts
            {
                return;
            }
            // there has to be money left in the pot
            // and the user wasn't rewarded
            if (_awardedUsers.Add(message.Author.Id) && TryTakeFromPot())
            {
                _toAward.Enqueue(message.Author.Id);
                if (_isPotLimited && PotSize < _amount)
                    PotEmptied = true;
            }

            try
            {
                await message.DeleteAsync(new RequestOptions
                {
                    RetryMode = RetryMode.AlwaysFail
                });
            }
            catch
            {
                // ignored
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