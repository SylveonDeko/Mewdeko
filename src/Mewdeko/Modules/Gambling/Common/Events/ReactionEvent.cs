using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Mewdeko.Common.Collections;
using Serilog;

namespace Mewdeko.Modules.Gambling.Common.Events;

public class ReactionEvent : ICurrencyEvent
{
    private readonly long amount;
    private readonly ConcurrentHashSet<ulong> awardedUsers = new();
    private readonly ITextChannel channel;
    private readonly GamblingConfig config;
    private readonly ICurrencyService cs;

    private readonly Func<CurrencyEvent.Type, EventOptions, long, EmbedBuilder> embedFunc;
    private readonly IGuild guild;
    private readonly bool isPotLimited;
    private readonly bool noRecentlyJoinedServer;
    private readonly EventHandler eventHandler;
    private readonly EventOptions opts;
    private readonly Timer t;
    private readonly Timer timeout;
    private readonly ConcurrentQueue<ulong> toAward = new();

    private readonly object potLock = new();

    private readonly object stopLock = new();
    private IEmote emote;
    private IUserMessage? msg;

    public ReactionEvent(
        ICurrencyService cs,
        SocketGuild g, ITextChannel ch, EventOptions opt, GamblingConfig config,
        Func<CurrencyEvent.Type, EventOptions, long, EmbedBuilder> embedFunc,
        EventHandler eventHandler)
    {
        guild = g;
        this.cs = cs;
        amount = opt.Amount;
        PotSize = opt.PotSize;
        this.embedFunc = embedFunc;
        this.eventHandler = eventHandler;
        isPotLimited = PotSize > 0;
        channel = ch;
        noRecentlyJoinedServer = false;
        opts = opt;
        this.config = config;

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
        if (Emote.TryParse(config.Currency.Sign, out var result))
            emote = result;
        else
            emote = new Emoji(config.Currency.Sign);
        msg = await channel.EmbedAsync(GetEmbed(opts.PotSize)).ConfigureAwait(false);
        await msg.AddReactionAsync(emote).ConfigureAwait(false);
        eventHandler.MessageDeleted += OnMessageDeleted;
        eventHandler.ReactionAdded += HandleReaction;
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
            eventHandler.ReactionAdded -= HandleReaction;
            t.Change(Timeout.Infinite, Timeout.Infinite);
            timeout.Change(Timeout.Infinite, Timeout.Infinite);
            try
            {
                var _ = msg.DeleteAsync();
            }
            catch (Exception)
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
                award.Select(_ => "Reaction Event"),
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
            Log.Warning(ex, "Error adding bulk currency to users");
        }
    }

    private EmbedBuilder GetEmbed(long pot) => embedFunc(CurrencyEvent.Type.Reaction, opts, pot);

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
                || (noRecentlyJoinedServer && // if specified, no users who joined the server in the last 24h
                    (gu.JoinedAt == null ||
                     (DateTime.UtcNow - gu.JoinedAt.Value).TotalDays <
                     1))) // and no users for who we don't know when they joined
            {
                return;
            }

            // there has to be money left in the pot
            // and the user wasn't rewarded
            if (awardedUsers.Add(r.UserId) && TryTakeFromPot())
            {
                toAward.Enqueue(r.UserId);
                if (isPotLimited && PotSize < amount)
                    PotEmptied = true;
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