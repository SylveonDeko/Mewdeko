using System.Threading;
using DataModel;
using Discord.Net;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Services.Analytics;
using Serilog;

namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     Manages the repeating execution of a message in a specified Discord channel.
/// </summary>
public class RepeatRunner : IDisposable
{
    private readonly DiscordShardedClient client;
    private readonly IAnalyticsCollector? collector;
    private readonly StickyConditionService? conditionService;
    private readonly GuildTimezoneService? guildTimezoneService;
    private readonly MessageCountService? messageCountService;
    private readonly MessageRepeaterService mrs;
    private readonly SemaphoreSlim triggerLock = new(1, 1);
    private bool disposed;
    private TimeSpan initialInterval;
    private DateTime lastActivityCheck = DateTime.UtcNow;
    private Timer? timer;
    private Timer? burstTimer;
    private int pendingMessages;

    /// <summary>
    ///     Initializes a new instance of the RepeatRunner class with the specified parameters.
    /// </summary>
    /// <param name="client">The Discord client for sending messages.</param>
    /// <param name="guild">The guild where messages will be sent.</param>
    /// <param name="repeater">The repeater configuration.</param>
    /// <param name="mrs">The message repeater service.</param>
    /// <param name="conditionService">Service for evaluating sticky conditions.</param>
    /// <param name="messageCountService">Service for activity detection.</param>
    /// <param name="guildTimezoneService">Service for timezone handling.</param>
    /// <param name="collector">The analytics collector.</param>
    public RepeatRunner(DiscordShardedClient client, IGuild guild, GuildRepeater repeater,
        MessageRepeaterService mrs, StickyConditionService? conditionService = null,
        MessageCountService? messageCountService = null, GuildTimezoneService? guildTimezoneService = null,
        IAnalyticsCollector? collector = null)
    {
        this.collector = collector;
        Repeater = repeater ?? throw new ArgumentNullException(nameof(repeater));
        Guild = guild ?? throw new ArgumentNullException(nameof(guild));
        this.mrs = mrs ?? throw new ArgumentNullException(nameof(mrs));
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.conditionService = conditionService;
        this.messageCountService = messageCountService;
        this.guildTimezoneService = guildTimezoneService;

        InitialInterval = TimeSpan.Parse(Repeater.Interval);
        Run();
    }

    /// <summary>
    ///     Gets the repeater configuration for this runner.
    /// </summary>
    public GuildRepeater Repeater { get; }

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

    /// <summary>
    ///     Disposes the repeater resources.
    /// </summary>
    public void Dispose()
    {
        if (disposed) return;

        disposed = true;
        timer?.Dispose();
        burstTimer?.Dispose();
        triggerLock.Dispose();

        GC.SuppressFinalize(this);
    }

    private void Run()
    {
        var triggerMode = (StickyTriggerMode)Repeater.TriggerMode;

        switch (triggerMode)
        {
            case StickyTriggerMode.Immediate:
                // Trigger immediately, then only respond to message events (no timer)
                _ = TriggerInternal();
                // Don't set up timer - immediate mode only responds to MessageReceived events
                break;
            case StickyTriggerMode.OnActivity:
            case StickyTriggerMode.OnNoActivity:
                // These modes use activity detection instead of timers
                SetupActivityBasedTrigger();
                break;
            case StickyTriggerMode.AfterMessages:
                // Driven by NoteMessage, which posts after N messages or T of new chat, whichever comes first
                if (Repeater.LastMessageId == null)
                    _ = TriggerInternal(true);
                break;
            case StickyTriggerMode.TimeInterval:
            default:
                // Original time-based logic
                SetupTimeBasedTrigger();
                break;
        }
    }

    private void SetupTimeBasedTrigger()
    {
        if (!string.IsNullOrEmpty(Repeater.StartTimeOfDay))
        {
            if (Repeater.DateAdded != null)
            {
                var added = Repeater.DateAdded.Value;
                var initialTriggerTimeOfDay = TimeSpan.Parse(Repeater.StartTimeOfDay);
                DateTime initialDateTime;

                if (added.TimeOfDay <= initialTriggerTimeOfDay)
                {
                    initialDateTime = added + (initialTriggerTimeOfDay - added.TimeOfDay);
                }
                else
                {
                    initialDateTime = added + TimeSpan.FromDays(1) - (added.TimeOfDay - initialTriggerTimeOfDay);
                }

                CalculateInitialInterval(initialDateTime);
            }
        }
        else
        {
            if (Repeater.DateAdded != null)
                CalculateInitialInterval(Repeater.DateAdded.Value + TimeSpan.Parse(Repeater.Interval));
        }

        SetupTimer();
    }

    private void SetupTimer()
    {
        timer = new Timer(Callback, null, InitialInterval, TimeSpan.Parse(Repeater.Interval));
    }

    private void SetupActivityBasedTrigger()
    {
        // For activity-based triggers, we check periodically but don't send on timer
        var checkInterval = TimeSpan.FromMinutes(1); // Check every minute
        timer = new Timer(ActivityCheckCallback, null, checkInterval, checkInterval);
    }

    private async void ActivityCheckCallback(object? _)
    {
        try
        {
            var triggerMode = (StickyTriggerMode)Repeater.TriggerMode;
            var shouldTrigger = false;

            switch (triggerMode)
            {
                case StickyTriggerMode.OnActivity:
                    shouldTrigger = await HasSufficientActivity();
                    break;
                case StickyTriggerMode.OnNoActivity:
                    shouldTrigger = await IsChannelQuiet();
                    break;
            }

            if (shouldTrigger)
            {
                await TriggerInternal(true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in activity check callback for repeater {RepeaterId}", Repeater.Id);
        }
    }

    private async void Callback(object? _)
    {
        try
        {
            await TriggerInternal().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in repeater callback for channel {ChannelId}", Repeater.ChannelId);
            await DisableRepeater(DescribeFailure(ex)).ConfigureAwait(false);
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
        await TriggerInternal();
    }

    /// <summary>
    ///     Triggers the repeater to send its message with activity context.
    /// </summary>
    /// <param name="isActivityTriggered">Whether this trigger was caused by channel activity.</param>
    public async Task TriggerInternal(bool isActivityTriggered = false)
    {
        if (disposed || !Repeater.IsEnabled)
            return;

        // Skip immediate posting for immediate mode with thread-only (forum channels)
        if (Repeater.TriggerMode == (int)StickyTriggerMode.Immediate && Repeater.ThreadOnlyMode)
        {
            Log.Debug(
                "Skipping immediate posting for immediate thread-only mode repeater {RepeaterId} - will post when threads are created",
                Repeater.Id);
            return;
        }

        // Skip if this is a thread-only repeater and we're trying to post in the parent channel
        if (Repeater.ThreadOnlyMode)
        {
            Log.Debug("Repeater {RepeaterId} is in thread-only mode, skipping parent channel posting", Repeater.Id);
            return;
        }

        await triggerLock.WaitAsync();
        try
        {
            // Check if sticky has expired
            if (conditionService?.HasExpired(Repeater) == true)
            {
                Log.Information("Repeater {RepeaterId} has expired, removing", Repeater.Id);
                await RemoveRepeater();
                return;
            }

            // Check time conditions
            if (conditionService?.ShouldDisplayAtCurrentTime(Repeater, Guild.Id) == false)
            {
                Log.Debug("Repeater {RepeaterId} not active at current time, skipping", Repeater.Id);
                ScheduleNextCheck();
                return;
            }

            if (Channel == null)
            {
                Channel = await Guild.GetTextChannelAsync(Repeater.ChannelId);
            }

            if (Channel == null)
            {
                Log.Warning("Channel {ChannelId} not found. Disabling repeater.", Repeater.ChannelId);
                await DisableRepeater("The channel no longer exists or I can't see it.");
                return;
            }

            // Check conversation detection
            if (Repeater.ConversationDetection && await IsConversationActive())
            {
                Log.Debug("Active conversation detected in {ChannelId}, delaying sticky", Repeater.ChannelId);
                ScheduleConversationRecheck();
                return;
            }

            if (Repeater.NoRedundant)
            {
                var lastMessage = await GetLastMessageAsync();
                if (lastMessage?.Id == Repeater.LastMessageId)
                {
                    ScheduleNextCheck();
                    return;
                }
            }

            await DeletePreviousMessageAsync();
            var newMsg = await SendNewMessageAsync();

            if (newMsg != null)
            {
                // Update tracking info
                Repeater.DisplayCount++;
                Repeater.LastDisplayed = DateTime.UtcNow;
                await mrs.UpdateRepeaterStatsAsync(Repeater.Id, Repeater.DisplayCount, Repeater.LastDisplayed.Value);

                // Always track the last message ID for all modes
                ResetPendingMessages();
                await mrs.SetRepeaterLastMessage(Repeater.Id, newMsg.Id);
                Repeater.LastMessageId = newMsg.Id;
                collector?.Feature("repeater", Guild.Id);
            }

            ScheduleNextCheck();
        }
        catch (HttpException ex)
        {
            collector?.Feature("repeater", Guild.Id, false, ex.GetType().Name);
            Log.Warning(ex, "HTTP error in repeater for channel {ChannelId}", Repeater.ChannelId);
            await DisableRepeater(DescribeFailure(ex));
        }
        catch (Exception ex)
        {
            collector?.Feature("repeater", Guild.Id, false, ex.GetType().Name);
            Log.Error(ex, "Error in repeater for channel {ChannelId}", Repeater.ChannelId);
            await DisableRepeater(DescribeFailure(ex));
        }
        finally
        {
            triggerLock.Release();
        }
    }

    private async Task<bool> HasSufficientActivity()
    {
        if (messageCountService == null) return false;

        var timeWindow = TimeSpan.Parse(Repeater.ActivityTimeWindow ?? "00:05:00");
        var threshold = Repeater.ActivityThreshold;

        try
        {
            // Get recent message activity for this channel
            var activityWindow = DateTime.UtcNow - timeWindow;
            if (Repeater.ActivityBasedLastCheck.HasValue && Repeater.ActivityBasedLastCheck.Value > activityWindow)
            {
                // Use the last check time if it's more recent than our window
                activityWindow = Repeater.ActivityBasedLastCheck.Value;
            }

            // Count messages since last check
            // Note: This is a simplified check - in reality we'd need to track recent message activity
            // For now, we'll use a basic heuristic
            var timeSinceLastCheck =
                DateTime.UtcNow - (Repeater.ActivityBasedLastCheck ?? DateTime.UtcNow.AddMinutes(-5));
            var estimatedMessages = (int)(timeSinceLastCheck.TotalMinutes * 2); // Rough estimate

            Repeater.ActivityBasedLastCheck = DateTime.UtcNow;
            return estimatedMessages >= threshold;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error checking activity for repeater {RepeaterId}", Repeater.Id);
            return false;
        }
    }

    private async Task<bool> IsChannelQuiet()
    {
        if (Channel == null) return false;

        try
        {
            var timeWindow = TimeSpan.Parse(Repeater.ActivityTimeWindow ?? "00:05:00");
            var messages = await Channel.GetMessagesAsync(50).FlattenAsync();

            var recentMessages = messages.Where(m =>
                DateTime.UtcNow - m.Timestamp.UtcDateTime < timeWindow).ToArray();

            return recentMessages.Length == 0;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error checking if channel is quiet for repeater {RepeaterId}", Repeater.Id);
            return false;
        }
    }

    /// <summary>
    ///     Deletes the last message this repeater posted.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteLastMessageAsync()
    {
        Channel ??= await Guild.GetTextChannelAsync(Repeater.ChannelId).ConfigureAwait(false);
        await DeletePreviousMessageAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Counts a new message in the channel for the after-messages mode. The repeater posts once the count reaches the
    ///     threshold, or once the time window has passed since the first new message, whichever comes first.
    /// </summary>
    public void NoteMessage()
    {
        if (disposed || !Repeater.IsEnabled || Repeater.TriggerMode != (int)StickyTriggerMode.AfterMessages)
            return;

        var count = Interlocked.Increment(ref pendingMessages);
        if (count >= Math.Max(1, Repeater.ActivityThreshold))
        {
            _ = TriggerInternal(true);
            return;
        }

        if (count != 1)
            return;

        var window = TimeSpan.TryParse(Repeater.ActivityTimeWindow, out var parsed) && parsed > TimeSpan.Zero
            ? parsed
            : TimeSpan.FromSeconds(15);
        burstTimer?.Dispose();
        burstTimer = new Timer(_ => _ = TriggerInternal(true), null, window, Timeout.InfiniteTimeSpan);
    }

    private void ResetPendingMessages()
    {
        Interlocked.Exchange(ref pendingMessages, 0);
        burstTimer?.Dispose();
        burstTimer = null;
    }

    private async Task<bool> IsConversationActive()
    {
        if (Channel == null || messageCountService == null) return false;

        try
        {
            // Check if there's been rapid message activity indicating active conversation
            var messages = await Channel.GetMessagesAsync(20).FlattenAsync();
            var recentMessages = messages.Where(m =>
                DateTime.UtcNow - m.Timestamp.UtcDateTime < TimeSpan.FromMinutes(1)).ToArray();

            return recentMessages.Length >= Repeater.ConversationThreshold;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error checking conversation activity for repeater {RepeaterId}", Repeater.Id);
            return false;
        }
    }

    private void ScheduleNextCheck()
    {
        var triggerMode = (StickyTriggerMode)Repeater.TriggerMode;

        switch (triggerMode)
        {
            case StickyTriggerMode.TimeInterval:
                NextDateTime = DateTime.UtcNow + TimeSpan.Parse(Repeater.Interval);
                break;
            case StickyTriggerMode.OnActivity:
            case StickyTriggerMode.OnNoActivity:
            case StickyTriggerMode.AfterMessages:
                // Activity-based modes check more frequently
                NextDateTime = DateTime.UtcNow + TimeSpan.FromMinutes(1);
                break;
            case StickyTriggerMode.Immediate:
                // Immediate mode uses normal interval after first trigger
                NextDateTime = DateTime.UtcNow + TimeSpan.Parse(Repeater.Interval);
                break;
        }
    }

    private void ScheduleConversationRecheck()
    {
        // Recheck in 30 seconds if conversation is still active
        NextDateTime = DateTime.UtcNow + TimeSpan.FromSeconds(30);
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

        // Set message flags - suppress notifications if enabled
        var flags = Repeater.SuppressNotifications ? MessageFlags.SuppressNotification : MessageFlags.None;

        var role = Repeater.PingRoleId is { } roleId ? Guild.GetRole(roleId) : null;
        if (role is null)
            return await SendCoreAsync(message, flags, null, null).ConfigureAwait(false);

        return await RolePinger.SendAsync(Guild, Channel, role,
            () => SendCoreAsync(message, flags, role.Mention, RolePinger.MentionsFor(role.Id))).ConfigureAwait(false);
    }

    private async Task<IMessage?> SendCoreAsync(string message, MessageFlags flags, string? mention,
        AllowedMentions? mentions)
    {
        if (SmartEmbed.TryParse(message, Channel!.GuildId, out var embed, out var plainText, out var components))
        {
            var embedContent = mention is null ? plainText ?? "" : string.Join(' ', mention, plainText).Trim();
            return await Channel
                .SendMessageAsync(embedContent, embeds: embed, components: components?.Build(), flags: flags,
                    allowedMentions: mentions)
                .ConfigureAwait(false);
        }

        var content = mention is null ? message : string.Join(' ', mention, message);
        return await Channel.SendMessageAsync(content, flags: flags, allowedMentions: mentions)
            .ConfigureAwait(false);
    }

    private async Task RemoveRepeater()
    {
        Stop();
        await mrs.RemoveRepeater(Repeater).ConfigureAwait(false);
    }

    private async Task DisableRepeater(string reason)
    {
        try
        {
            Stop();
            await mrs.DisableRepeaterAsync(Repeater, reason).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error disabling failed repeater {RepeaterId}", Repeater.Id);
        }
    }

    private static string DescribeFailure(Exception ex)
    {
        var reason = ex switch
        {
            HttpException { DiscordCode: DiscordErrorCode.MissingPermissions or DiscordErrorCode.InsufficientPermissions } =>
                "I'm missing permissions to send messages there.",
            HttpException { DiscordCode: DiscordErrorCode.UnknownChannel } => "The channel was deleted.",
            HttpException { DiscordCode: DiscordErrorCode.UnknownMessage } => "A message it needed was deleted.",
            HttpException { DiscordCode: { } code } => $"Discord refused the message ({code}).",
            HttpException http => $"Discord refused the message ({(int)http.HttpCode}).",
            _ => ex.Message
        };

        return reason.TrimTo(200);
    }

    /// <summary>
    ///     Resets the repeater with new settings.
    /// </summary>
    public void Reset()
    {
        Stop(); // This clears any existing timer
        lastActivityCheck = DateTime.UtcNow;
        Run(); // Set up new timer/mode based on current TriggerMode
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

        ResetPendingMessages();
    }

    /// <summary>
    ///     Returns a string representation of the repeater.
    /// </summary>
    public override string ToString()
    {
        TimeSpan.TryParse(Repeater.Interval, out var interval);
        var triggerMode = (StickyTriggerMode)Repeater.TriggerMode;
        var modeIndicator = triggerMode switch
        {
            StickyTriggerMode.OnActivity => "📈",
            StickyTriggerMode.OnNoActivity => "📉",
            StickyTriggerMode.Immediate => "⚡",
            StickyTriggerMode.AfterMessages => "💬",
            _ => ""
        };

        var priorityIndicator = Repeater.Priority != 50 ? $"[P{Repeater.Priority}]" : "";
        var redundantIndicator = Repeater.NoRedundant ? "| ✍" : "";
        var enabledIndicator = Repeater.IsEnabled ? "" :
            string.IsNullOrWhiteSpace(Repeater.DisabledReason) ? "[❌]" : $"[❌ {Repeater.DisabledReason.TrimTo(60)}]";

        return
            $"{Channel?.Mention ?? $"⚠<#{Repeater.ChannelId}>"} {modeIndicator}{priorityIndicator}{redundantIndicator}{enabledIndicator}| {interval.TotalHours}:{interval:mm} | {Repeater.Message.TrimTo(33)}";
    }
}