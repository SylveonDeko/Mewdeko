using System.Threading;
using System.Threading.Tasks;
using Discord.Net;
using Mewdeko.Modules.Utility.Services;
using Serilog;

namespace Mewdeko.Modules.Utility.Common;

public class RepeatRunner
{
    private readonly DiscordSocketClient client;

    private readonly MessageRepeaterService mrs;

    private TimeSpan initialInterval;

    private Timer t;

    public RepeatRunner(DiscordSocketClient client, SocketGuild guild, Repeater repeater,
        MessageRepeaterService mrs)
    {
        Repeater = repeater;
        Guild = guild;
        this.mrs = mrs;
        this.client = client;

        InitialInterval = Repeater.Interval;

        Run();
    }

    public Repeater Repeater { get; }
    public SocketGuild Guild { get; }

    public ITextChannel? Channel { get; private set; }

    public TimeSpan InitialInterval
    {
        get => initialInterval;
        private set
        {
            initialInterval = value;
            NextDateTime = DateTime.UtcNow + value;
        }
    }

    /// <summary>
    ///     When's the next time the repeater will run.
    ///     On bot startup, it will be InitialInterval + StartupDateTime.
    ///     After first execution, it will be Interval + ExecutionDateTime
    /// </summary>
    public DateTime NextDateTime { get; set; }

    private void Run()
    {
        if (Repeater.StartTimeOfDay != null)
        {
            // if there was a start time of day
            // calculate whats the next time of day repeat should trigger at
            // based on teh dateadded

            // i know this is not null because of the .Where in the repeat service
            if (Repeater.DateAdded != null)
            {
                var added = Repeater.DateAdded.Value;

                // initial trigger was the time of day specified by the command.
                var initialTriggerTimeOfDay = Repeater.StartTimeOfDay.Value;

                DateTime initialDateTime;

                // if added timeofday is less than specified timeofday for initial trigger
                // that means the repeater first ran that same day at that exact specified time
                if (added.TimeOfDay <= initialTriggerTimeOfDay)
                {
                    // in that case, just add the difference to make sure the timeofday is the same
                    initialDateTime = added + (initialTriggerTimeOfDay - added.TimeOfDay);
                }
                else
                {
                    // if not, then it ran at that time the following day
                    // in other words; Add one day, and subtract how much time passed since that time of day
                    initialDateTime = added + TimeSpan.FromDays(1) - (added.TimeOfDay - initialTriggerTimeOfDay);
                }

                CalculateInitialInterval(initialDateTime);
            }
        }
        else
        {
            // if repeater is not running daily, it's initial time is the time it was Added at, plus the interval
            if (Repeater.DateAdded != null) CalculateInitialInterval(Repeater.DateAdded.Value + Repeater.Interval);
        }

        // wait at least a minute for the bot to have all data needed in the cache
        if (InitialInterval < TimeSpan.FromMinutes(1))
            InitialInterval = TimeSpan.FromMinutes(1);

        t = new Timer(Callback, null, InitialInterval, Repeater.Interval);
    }

    private async void Callback(object _)
    {
        try
        {
            await Trigger().ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>
    ///     Calculate when is the proper time to run the repeater again based on initial time repeater ran.
    /// </summary>
    /// <param name="initialDateTime">Initial time repeater ran at (or should run at).</param>
    private void CalculateInitialInterval(DateTime initialDateTime)
    {
        // if the initial time is greater than now, that means the repeater didn't still execute a single time.
        // just schedule it
        if (initialDateTime > DateTime.UtcNow)
        {
            InitialInterval = initialDateTime - DateTime.UtcNow;
        }
        else
        {
            // else calculate based on minutes difference

            // get the difference
            var diff = DateTime.UtcNow - initialDateTime;

            // see how many times the repeater theoretically ran already
            var triggerCount = diff / Repeater.Interval;

            // ok lets say repeater was scheduled to run 10h ago.
            // we have an interval of 2.4h
            // repeater should've ran 4 times- that's 9.6h
            // next time should be in 2h from now exactly
            // 10/2.4 is 4.166
            // 4.166 - Math.Truncate(4.166) is 0.166
            // initial interval multiplier is 1 - 0.166 = 0.834
            // interval (2.4h) * 0.834 is 2.0016 and that is the initial interval

            var initialIntervalMultiplier = 1 - (triggerCount - Math.Truncate(triggerCount));
            InitialInterval = Repeater.Interval * initialIntervalMultiplier;
        }
    }

    public async Task Trigger()
    {
        async Task ChannelMissingError()
        {
            Log.Warning("Channel not found or insufficient permissions. Repeater stopped. ChannelId : {0}",
                Channel?.Id);
            Stop();
            await mrs.RemoveRepeater(Repeater).ConfigureAwait(false);
        }

        // next execution is interval amount of time after now
        NextDateTime = DateTime.UtcNow + Repeater.Interval;

        var toSend = Repeater.Message;
        try
        {
            Channel ??= Guild.GetTextChannel(Repeater.ChannelId);

            if (Channel == null)
            {
                await ChannelMissingError().ConfigureAwait(false);
                return;
            }

            if (Repeater.NoRedundant)
            {
                var lastMsgInChannel = (await Channel.GetMessagesAsync(2).FlattenAsync().ConfigureAwait(false))
                    .FirstOrDefault();
                if (lastMsgInChannel != null && lastMsgInChannel.Id == Repeater.LastMessageId
                   ) //don't send if it's the same message in the channel
                    return;
            }

            // if the message needs to be send
            // delete previous message if it exists
            try
            {
                if (Repeater.LastMessageId != null)
                {
                    var oldMsg = await Channel.GetMessageAsync(Repeater.LastMessageId.Value).ConfigureAwait(false);
                    if (oldMsg != null) await oldMsg.DeleteAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                // ignored
            }

            var rep = new ReplacementBuilder()
                .WithDefault(Guild.CurrentUser, Channel, Guild, client)
                .Build();

            IMessage newMsg;
            if (SmartEmbed.TryParse(rep.Replace(toSend), Channel.GuildId, out var embed, out var plainText, out var components))
            {
                newMsg = await Channel.SendMessageAsync(plainText, embeds: embed, components: components.Build()).ConfigureAwait(false);
            }
            else
            {
                newMsg = await Channel.SendMessageAsync(rep.Replace(toSend)).ConfigureAwait(false);
            }

            if (Repeater.NoRedundant)
            {
                mrs.SetRepeaterLastMessage(Repeater.Id, newMsg.Id);
                Repeater.LastMessageId = newMsg.Id;
            }
        }
        catch (HttpException ex)
        {
            Log.Warning(ex, "Http Exception in repeat trigger");
            await ChannelMissingError().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Exception in repeat trigger");
            Stop();
            await mrs.RemoveRepeater(Repeater).ConfigureAwait(false);
        }
    }

    public void Reset()
    {
        Stop();
        Run();
    }

    public void Stop() => t.Change(Timeout.Infinite, Timeout.Infinite);

    public override string ToString() =>
        $"{Channel?.Mention ?? $"⚠<#{Repeater.ChannelId}>"} {(Repeater.NoRedundant ? "| ✍" : "")}| {(int)Repeater.Interval.TotalHours}:{Repeater.Interval:mm} | {Repeater.Message.TrimTo(33)}";
}