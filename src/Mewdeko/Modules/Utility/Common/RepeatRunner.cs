using System.Threading;
using Discord.Net;
using Mewdeko.Modules.Utility.Services;
using Serilog;

namespace Mewdeko.Modules.Utility.Common;

/// <summary>
/// Manages the repeating execution of a message in a specified Discord channel.
/// </summary>
public class RepeatRunner
{
    private readonly DiscordSocketClient client;

    private readonly MessageRepeaterService mrs;

    private TimeSpan initialInterval;

    private Timer t;

    /// <summary>
    /// Initializes a new instance of the <see cref="RepeatRunner"/> with the specified parameters.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="guild">The guild in which the message will be repeated.</param>
    /// <param name="repeater">The repeater configuration.</param>
    /// <param name="mrs">The message repeater service.</param>
    /// <remarks>
    /// The runner calculates the initial and subsequent intervals for message repetition,
    /// handling daily repetitions or at specific intervals.
    /// </remarks>
    public RepeatRunner(DiscordSocketClient client, SocketGuild guild, Repeater repeater,
        MessageRepeaterService mrs)
    {
        Repeater = repeater;
        Guild = guild;
        this.mrs = mrs;
        this.client = client;

        InitialInterval = TimeSpan.Parse(Repeater.Interval);

        Run();
    }

    /// <summary>
    /// Gets the repeater configuration associated with this instance.
    /// </summary>
    public Repeater Repeater { get; }

    /// <summary>
    /// Gets the guild (server) where the message will be repeated.
    /// </summary>
    public SocketGuild Guild { get; }

    /// <summary>
    /// Gets the text channel where the repeated message is sent.
    /// This property is set after the first execution of the repeater.
    /// </summary>
    public ITextChannel? Channel { get; private set; }

    /// <summary>
    /// Gets or sets the initial interval before the first message repetition.
    /// Subsequent intervals are based on the configured <see cref="Repeater.Interval"/>.
    /// </summary>
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
        if (!string.IsNullOrEmpty(Repeater.StartTimeOfDay))
        {
            // if there was a start time of day
            // calculate whats the next time of day repeat should trigger at
            // based on teh dateadded

            // i know this is not null because of the .Where in the repeat service
            if (Repeater.DateAdded != null)
            {
                var added = Repeater.DateAdded.Value;

                // initial trigger was the time of day specified by the command.
                var initialTriggerTimeOfDay = TimeSpan.Parse(Repeater.StartTimeOfDay);

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
            if (Repeater.DateAdded != null)
                CalculateInitialInterval(Repeater.DateAdded.Value + TimeSpan.Parse(Repeater.Interval));
        }

        // wait at least a minute for the bot to have all data needed in the cache
        if (InitialInterval < TimeSpan.FromMinutes(1))
            InitialInterval = TimeSpan.FromMinutes(1);

        t = new Timer(Callback, null, InitialInterval, TimeSpan.Parse(Repeater.Interval));
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
            var triggerCount = diff / TimeSpan.Parse(Repeater.Interval);

            // ok lets say repeater was scheduled to run 10h ago.
            // we have an interval of 2.4h
            // repeater should've ran 4 times- that's 9.6h
            // next time should be in 2h from now exactly
            // 10/2.4 is 4.166
            // 4.166 - Math.Truncate(4.166) is 0.166
            // initial interval multiplier is 1 - 0.166 = 0.834
            // interval (2.4h) * 0.834 is 2.0016 and that is the initial interval

            var initialIntervalMultiplier = 1 - (triggerCount - Math.Truncate(triggerCount));
            InitialInterval = TimeSpan.Parse(Repeater.Interval) * initialIntervalMultiplier;
        }
    }


    /// <summary>
    /// Executes the repeater's action, sending the configured message to the specified channel.
    /// </summary>
    public async Task Trigger()
    {
        Task ChannelMissingError()
        {
            Log.Warning("Channel not found or insufficient permissions. Repeater stopped. ChannelId : {0}",
                Channel?.Id);
            Stop();
            return mrs.RemoveRepeater(Repeater);
        }

        // next execution is interval amount of time after now
        NextDateTime = DateTime.UtcNow + TimeSpan.Parse(Repeater.Interval);

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
            if (SmartEmbed.TryParse(rep.Replace(toSend), Channel.GuildId, out var embed, out var plainText,
                    out var components))
            {
                newMsg = await Channel.SendMessageAsync(plainText ?? "", embeds: embed, components: components?.Build())
                    .ConfigureAwait(false);
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

    /// <summary>
    /// Stops and then restarts the repeater, recalculating the initial interval based on current settings.
    /// </summary>
    public void Reset()
    {
        Stop();
        Run();
    }

    /// <summary>
    /// Stops the repeater, preventing any further executions until reset or restarted.
    /// </summary>
    public void Stop() => t.Change(Timeout.Infinite, Timeout.Infinite);

    /// <summary>
    /// Provides a string representation of the repeater's current state, including its channel, interval, and message.
    /// </summary>
    /// <returns>A string detailing the repeater's configuration and status.</returns>
    public override string ToString()
    {
        TimeSpan.TryParse(Repeater.Interval, out var interval);
        return
            $"{Channel?.Mention ?? $"⚠<#{Repeater.ChannelId}>"} {(Repeater.NoRedundant ? "| ✍" : "")}| {interval.TotalHours}:{interval:mm} | {Repeater.Message.TrimTo(33)}";
    }
}