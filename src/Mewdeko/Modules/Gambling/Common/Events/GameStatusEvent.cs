using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Mewdeko.Common.Collections;
using Serilog;

namespace Mewdeko.Modules.Gambling.Common.Events;

public class GameStatusEvent : ICurrencyEvent
{
    private readonly long amount;
    private readonly ConcurrentHashSet<ulong> awardedUsers = new();
    private readonly ITextChannel channel;
    private readonly DiscordSocketClient client;

    private readonly string code;
    private readonly ICurrencyService cs;

    private readonly Func<CurrencyEvent.Type, EventOptions, long, EmbedBuilder> embedFunc;
    private readonly IGuild guild;
    private readonly bool isPotLimited;
    private readonly EventOptions opts;
    private readonly EventHandler eventHandler;

    private readonly char[] sneakyGameStatusChars = Enumerable.Range(48, 10)
        .Concat(Enumerable.Range(65, 26))
        .Concat(Enumerable.Range(97, 26))
        .Select(x => (char)x)
        .ToArray();

    private readonly Timer t;
    private readonly Timer timeout;
    private readonly ConcurrentQueue<ulong> toAward = new();

    private readonly object potLock = new();

    private readonly object stopLock = new();
    private IUserMessage? msg;

    public GameStatusEvent(DiscordSocketClient client, ICurrencyService cs, SocketGuild g, ITextChannel ch,
        EventOptions opt, Func<CurrencyEvent.Type, EventOptions, long, EmbedBuilder> embedFunc,
        EventHandler eventHandler)
    {
        this.client = client;
        guild = g;
        this.cs = cs;
        amount = opt.Amount;
        PotSize = opt.PotSize;
        this.embedFunc = embedFunc;
        this.eventHandler = eventHandler;
        isPotLimited = PotSize > 0;
        channel = ch;
        opts = opt;
        // generate code
        code = new string(sneakyGameStatusChars.Shuffle().Take(5).ToArray());

        t = new Timer(OnTimerTick, null, Timeout.InfiniteTimeSpan, TimeSpan.FromSeconds(2));
        if (opts.Hours > 0)
            timeout = new Timer(EventTimeout, null, TimeSpan.FromHours(opts.Hours), Timeout.InfiniteTimeSpan);
    }

    private long PotSize { get; set; }
    public bool Stopped { get; private set; }
    public bool PotEmptied { get; private set; }

    public event Func<ulong, Task> OnEnded;

    public async Task StartEvent()
    {
        msg = await channel.EmbedAsync(GetEmbed(opts.PotSize)).ConfigureAwait(false);
        await client.SetGameAsync(code).ConfigureAwait(false);
        eventHandler.MessageDeleted += OnMessageDeleted;
        eventHandler.MessageReceived += HandleMessage;
        t.Change(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    public async Task StopEvent()
    {
        await Task.Yield();
        lock (stopLock)
        {
            if (Stopped)
                return;
            Stopped = true;
            eventHandler.MessageDeleted -= OnMessageDeleted;
            eventHandler.MessageReceived -= HandleMessage;
#pragma warning disable CS4014
            client.SetGameAsync(null);
#pragma warning restore CS4014
            t.Change(Timeout.Infinite, Timeout.Infinite);
            timeout.Change(Timeout.Infinite, Timeout.Infinite);
            try
            {
                _ = msg.DeleteAsync();
            }
            catch
            {
                // ignored
            }

#pragma warning disable CS4014
            OnEnded(guild.Id);
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
        var award = new List<ulong>();
        while (this.toAward.TryDequeue(out var x)) award.Add(x);

        if (award.Count == 0)
            return;

        try
        {
            await cs.AddBulkAsync(award,
                award.Select(_ => "GameStatus Event"),
                award.Select(_ => amount),
                true).ConfigureAwait(false);

            if (isPotLimited)
            {
                await msg.ModifyAsync(m => m.Embed = GetEmbed(PotSize).Build(),
                    new RequestOptions
                    {
                        RetryMode = RetryMode.AlwaysRetry
                    }).ConfigureAwait(false);
            }

            Log.Information("Awarded {0} users {1} currency.{2}",
                award.Count,
                amount,
                isPotLimited ? $" {PotSize} left." : "");

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

    private EmbedBuilder GetEmbed(long pot) => embedFunc(CurrencyEvent.Type.GameStatus, opts, pot);

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
                || message.Content != code // code has to be the same
                || (DateTime.UtcNow - gu.CreatedAt).TotalDays <= 5) // no recently created accounts
            {
                return;
            }

            // there has to be money left in the pot
            // and the user wasn't rewarded
            if (awardedUsers.Add(message.Author.Id) && TryTakeFromPot())
            {
                toAward.Enqueue(message.Author.Id);
                if (isPotLimited && PotSize < amount)
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
        if (isPotLimited)
        {
            lock (potLock)
            {
                if (PotSize < amount)
                    return false;

                PotSize -= amount;
                return true;
            }
        }

        return true;
    }
}