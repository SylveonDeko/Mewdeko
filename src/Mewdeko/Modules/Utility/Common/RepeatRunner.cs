using System.Threading;
using Discord.Net;
using Mewdeko.Modules.Utility.Services;
using Serilog;

namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     Manages the repeating execution of a message in a specified Discord channel.
/// </summary>
public class RepeatRunner : IDisposable
{
    private readonly DiscordShardedClient client;
    private readonly MessageRepeaterService mrs;
    private readonly SemaphoreSlim triggerLock = new(1, 1);
    private TimeSpan initialInterval;
    private Timer? timer;
    private bool disposed;

    /// <summary>
    ///     Initializes a new instance of the RepeatRunner class with the specified parameters.
    /// </summary>
    /// <param name="client">The Discord client for sending messages.</param>
    /// <param name="guild">The guild where messages will be sent.</param>
    /// <param name="repeater">The repeater configuration.</param>
    /// <param name="mrs">The message repeater service.</param>
    public RepeatRunner(DiscordShardedClient client, IGuild guild, Repeater repeater,
        MessageRepeaterService mrs)
    {
        Repeater = repeater ?? throw new ArgumentNullException(nameof(repeater));
        Guild = guild ?? throw new ArgumentNullException(nameof(guild));
        this.mrs = mrs ?? throw new ArgumentNullException(nameof(mrs));
        this.client = client ?? throw new ArgumentNullException(nameof(client));

        InitialInterval = TimeSpan.Parse(Repeater.Interval);
        Run();
    }

    /// <summary>
    ///     Gets the repeater configuration for this runner.
    /// </summary>
    public Repeater Repeater { get; }

    /// <summary>
    ///     Gets the guild where the repeater operates.
    /// </summary>
    public IGuild Guild { get; }

    /// <summary>
    ///     Gets the channel where messages are sent.
    /// </summary>
    public ITextChannel? Channel { get; private set; }

    /// <summary>
    ///     Gets or sets the initial interval for the repeater.
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
    ///     Gets the next scheduled execution time.
    /// </summary>
    public DateTime NextDateTime { get; private set; }

    private void Run()
    {
        if (!string.IsNullOrEmpty(Repeater.StartTimeOfDay))
        {
            // if repeater is not running daily, it's initial time is the time it was Added at, plus the interval
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

        timer = new Timer(Callback, null, InitialInterval, TimeSpan.Parse(Repeater.Interval));
    }

    private async void Callback(object? _)
    {
        try
        {
            await Trigger().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in repeater callback for channel {ChannelId}", Repeater.ChannelId);
            try
            {
                Stop();
                await mrs.RemoveRepeater(Repeater).ConfigureAwait(false);
            }
            catch (Exception innerEx)
            {
                Log.Error(innerEx, "Error removing failed repeater");
            }
        }
    }

    private void CalculateInitialInterval(DateTime initialDateTime)
    {
        if (initialDateTime > DateTime.UtcNow)
        {
            InitialInterval = initialDateTime - DateTime.UtcNow;
            return;
        }

        var diff = DateTime.UtcNow - initialDateTime;
        var interval = TimeSpan.Parse(Repeater.Interval);
        var triggerCount = diff / interval;
        var initialIntervalMultiplier = 1 - (triggerCount - Math.Truncate(triggerCount));
        InitialInterval = interval * initialIntervalMultiplier;
    }

    /// <summary>
    ///     Triggers the repeater to send its message.
    /// </summary>
    public async Task Trigger()
    {
        if (disposed)
            return;

        await triggerLock.WaitAsync();
        try
        {
            NextDateTime = DateTime.UtcNow + TimeSpan.Parse(Repeater.Interval);

            Channel ??= await Guild.GetTextChannelAsync(Repeater.ChannelId);
            if (Channel == null)
            {
                Log.Warning("Channel {ChannelId} not found. Removing repeater.", Repeater.ChannelId);
                await RemoveRepeater();
                return;
            }

            if (Repeater.NoRedundant)
            {
                var lastMessage = await GetLastMessageAsync();
                if (lastMessage?.Id == Repeater.LastMessageId)
                    return;
            }

            await DeletePreviousMessageAsync();
            var newMsg = await SendNewMessageAsync();

            if (Repeater.NoRedundant && newMsg != null)
            {
                await mrs.SetRepeaterLastMessage(Repeater.Id, newMsg.Id);
                Repeater.LastMessageId = newMsg.Id;
            }
        }
        catch (HttpException ex)
        {
            Log.Warning(ex, "HTTP error in repeater for channel {ChannelId}", Repeater.ChannelId);
            await RemoveRepeater();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in repeater for channel {ChannelId}", Repeater.ChannelId);
            await RemoveRepeater();
        }
        finally
        {
            triggerLock.Release();
        }
    }

    private async Task<IMessage?> GetLastMessageAsync()
    {
        if (Channel == null) return null;
        var messages = await Channel.GetMessagesAsync(2).FlattenAsync().ConfigureAwait(false);
        return messages.FirstOrDefault();
    }

    private async Task DeletePreviousMessageAsync()
    {
        if (Channel == null || Repeater.LastMessageId == null) return;

        try
        {
            var oldMsg = await Channel.GetMessageAsync(Repeater.LastMessageId.Value).ConfigureAwait(false);
            if (oldMsg != null)
                await oldMsg.DeleteAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error deleting previous repeater message");
        }
    }

    private async Task<IMessage?> SendNewMessageAsync()
    {
        if (Channel == null) return null;

        var rep = new ReplacementBuilder()
            .WithDefault(await Guild.GetCurrentUserAsync(), Channel, Guild as SocketGuild, client)
            .Build();

        var message = rep.Replace(Repeater.Message);

        if (SmartEmbed.TryParse(message, Channel.GuildId, out var embed, out var plainText, out var components))
        {
            return await Channel.SendMessageAsync(plainText ?? "", embeds: embed, components: components)
                .ConfigureAwait(false);
        }

        return await Channel.SendMessageAsync(message).ConfigureAwait(false);
    }

    private async Task RemoveRepeater()
    {
        Stop();
        await mrs.RemoveRepeater(Repeater).ConfigureAwait(false);
    }

    /// <summary>
    ///     Resets the repeater with new settings.
    /// </summary>
    public void Reset()
    {
        Stop();
        Run();
    }

    /// <summary>
    ///     Stops the repeater.
    /// </summary>
    public void Stop()
    {
        if (timer != null)
        {
            timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <summary>
    ///     Returns a string representation of the repeater.
    /// </summary>
    public override string ToString()
    {
        TimeSpan.TryParse(Repeater.Interval, out var interval);
        return $"{Channel?.Mention ?? $"⚠<#{Repeater.ChannelId}>"} {(Repeater.NoRedundant ? "| ✍" : "")}| {interval.TotalHours}:{interval:mm} | {Repeater.Message.TrimTo(33)}";
    }

    /// <summary>
    ///     Disposes the repeater resources.
    /// </summary>
    public void Dispose()
    {
        if (disposed) return;

        disposed = true;
        timer?.Dispose();
        triggerLock.Dispose();

        GC.SuppressFinalize(this);
    }
}