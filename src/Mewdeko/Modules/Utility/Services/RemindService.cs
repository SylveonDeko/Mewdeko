using System.Text.RegularExpressions;
using System.Threading;
using DataModel;
using Discord.Net;
using Humanizer;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Services.Strings;
using Swan;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Manages and executes reminders for users at specified times.
/// </summary>
public partial class RemindService : INService
{
    /// <summary>
    ///     The shortest interval a reminder can repeat at.
    /// </summary>
    public static readonly TimeSpan MinRepeat = TimeSpan.FromHours(1);

    /// <summary>
    ///     The possible outcomes of subscribing to a reminder.
    /// </summary>
    public enum SubscribeResult
    {
        /// <summary>
        ///     The user is now subscribed.
        /// </summary>
        Subscribed,

        /// <summary>
        ///     The reminder does not exist.
        /// </summary>
        NotFound,

        /// <summary>
        ///     The user already subscribed.
        /// </summary>
        AlreadySubscribed,

        /// <summary>
        ///     The user owns the reminder.
        /// </summary>
        Owner
    }

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<RemindService> logger;

    private readonly Regex regex = MyRegex();
    private readonly ConcurrentDictionary<int, Timer> reminderTimers;
    private readonly GeneratedBotStrings strings;
    private readonly GuildTimezoneService tz;

    /// <summary>
    ///     Initializes the reminder service, starting the background task to check for and execute reminders.
    /// </summary>
    /// <param name="client">The Discord client used for sending reminder notifications.</param>
    /// <param name="dbFactory">The database service for managing reminders.</param>
    /// <param name="tz">The timezone service for guild timezones.</param>
    /// <param name="strings">The bot strings service for localized messages.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public RemindService(DiscordShardedClient client, IDataConnectionFactory dbFactory, GuildTimezoneService tz,
        GeneratedBotStrings strings, ILogger<RemindService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.tz = tz;
        this.strings = strings;
        this.logger = logger;
        reminderTimers = new ConcurrentDictionary<int, Timer>();
        _ = InitializeRemindersAsync();
    }

    /// <summary>
    ///     Initializes the reminders by loading them from the database and setting timers.
    /// </summary>
    private async Task InitializeRemindersAsync()
    {
        var reminders = await GetRemindersAsync();
        foreach (var reminder in reminders)
        {
            // Only schedule reminders that haven't occurred yet
            if (reminder.When > DateTime.UtcNow)
            {
                _ = ScheduleReminder(reminder);
            }
            else
            {
                // For past reminders, either execute them immediately or clean them up
                _ = ExecuteReminderAsync(reminder);
            }
        }
    }

    /// <summary>
    ///     Schedules a reminder by setting a timer.
    /// </summary>
    /// <param name="reminder">The reminder to be scheduled.</param>
    public Task ScheduleReminder(Reminder reminder)
    {
        var timeToGo = reminder.When - DateTime.UtcNow;
        if (timeToGo <= TimeSpan.Zero)
        {
            timeToGo = TimeSpan.Zero;
        }

        var timer = new Timer(async _ => await ExecuteReminderAsync(reminder), null, timeToGo,
            Timeout.InfiniteTimeSpan);
        reminderTimers[reminder.Id] = timer;
        return Task.CompletedTask;
    }

    private async Task<List<Reminder>> GetRemindersAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.Reminders.ToListAsync();
    }

    /// <summary>
    ///     Executes the reminder action.
    /// </summary>
    /// <param name="reminder">The reminder to be executed.</param>
    private async Task ExecuteReminderAsync(Reminder reminder)
    {
        try
        {
            await using (var db = await dbFactory.CreateConnectionAsync())
            {
                var current = await db.Reminders.FirstOrDefaultAsync(r => r.Id == reminder.Id);
                if (current == null)
                {
                    if (reminderTimers.TryRemove(reminder.Id, out var stale))
                        await stale.DisposeAsync();
                    return;
                }

                reminder = current;
            }

            var subscribers = ParseSubscribers(reminder.Subscribers);

            IMessageChannel ch;
            if (reminder.IsPrivate)
            {
                var user = client.GetUser(reminder.ChannelId);
                if (user == null)
                    return;
                ch = await user.CreateDMChannelAsync().ConfigureAwait(false);
            }
            else
                ch = client.GetGuild(reminder.ServerId)?.GetTextChannel(reminder.ChannelId);

            if (ch == null)
                return;

            var embed = new EmbedBuilder()
                .WithOkColor(reminder.ServerId)
                .WithTitle(strings.ReminderTitle(reminder.ServerId))
                .AddField("Created At",
                    reminder.DateAdded.HasValue ? reminder.DateAdded.Value.ToLongDateString() : "?")
                .AddField("By",
                    (await ch.GetUserAsync(reminder.UserId).ConfigureAwait(false))?.ToString() ??
                    reminder.UserId.ToString());

            var content = reminder.Message;
            if (!reminder.IsPrivate && subscribers.Count > 0)
                content = $"{string.Join(" ", subscribers.Select(MentionUtils.MentionUser))}\n{content}";

            await ch.EmbedAsync(embed, content).ConfigureAwait(false);

            if (reminder.IsPrivate)
            {
                foreach (var subscriber in subscribers)
                {
                    try
                    {
                        var subscriberUser = client.GetUser(subscriber);
                        if (subscriberUser == null)
                            continue;

                        var dm = await subscriberUser.CreateDMChannelAsync().ConfigureAwait(false);
                        await dm.EmbedAsync(embed, reminder.Message).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Could not DM reminder {ReminderId} to subscriber {UserId}",
                            reminder.Id, subscriber);
                    }
                }
            }

            if (reminder.RepeatMinutes < MinRepeat.TotalMinutes)
            {
                await RemoveReminder(reminder);
                return;
            }

            var step = TimeSpan.FromMinutes(reminder.RepeatMinutes);
            var next = reminder.When;
            do
            {
                next += step;
            } while (next <= DateTime.UtcNow);

            reminder.When = next;
            await using (var db = await dbFactory.CreateConnectionAsync())
            {
                await db.Reminders
                    .Where(r => r.Id == reminder.Id)
                    .Set(r => r.When, next)
                    .UpdateAsync();
            }

            await ScheduleReminder(reminder);
        }
        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.CannotSendMessageToUser)
        {
            logger.LogWarning("Cannot send reminder to user {UserId}, removing reminder {ReminderId}",
                reminder.UserId, reminder.Id);
            await RemoveReminder(reminder);
        }
        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.UnknownChannel)
        {
            logger.LogWarning("Channel {ChannelId} no longer exists, removing reminder {ReminderId}",
                reminder.ChannelId, reminder.Id);
            await RemoveReminder(reminder);
        }
        catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.MissingPermissions)
        {
            logger.LogWarning(
                "Missing permissions to send reminder in channel {ChannelId}, removing reminder {ReminderId}",
                reminder.ChannelId, reminder.Id);
            await RemoveReminder(reminder);
        }
        catch (HttpException ex)
        {
            logger.LogWarning(ex, "Discord API error executing reminder {ReminderId}: {ErrorCode} - {ErrorMessage}",
                reminder.Id, ex.DiscordCode, ex.Message);
            await RemoveReminder(reminder);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error executing reminder {ReminderId}", reminder.Id);
            await RemoveReminder(reminder);
        }
    }

    /// <summary>
    ///     Removes the reminder from the database and disposes of its timer.
    /// </summary>
    /// <param name="reminder">The reminder to be removed.</param>
    private async Task RemoveReminder(Reminder reminder)
    {
        if (reminderTimers.TryRemove(reminder.Id, out var timer))
        {
            await timer.DisposeAsync();
        }

        await using var db = await dbFactory.CreateConnectionAsync();

        // Use LinqToDB to delete the reminder
        await db.Reminders
            .Where(r => r.Id == reminder.Id)
            .DeleteAsync();
    }

    /// <summary>
    ///     Creates a new reminder and schedules it for execution.
    /// </summary>
    /// <param name="targetId">The ID of the target channel or user.</param>
    /// <param name="isPrivate">Whether the reminder should be sent as a private message.</param>
    /// <param name="ts">The time span until the reminder should be triggered.</param>
    /// <param name="message">The reminder message content.</param>
    /// <param name="userId">The ID of the user who created the reminder.</param>
    /// <param name="serverId">The ID of the server where the reminder was created, or null for private reminders.</param>
    /// <param name="sanitizeMentions">Whether to sanitize mentions in the message.</param>
    /// <param name="repeat">How often the reminder repeats, or null to fire once.</param>
    /// <returns>A tuple containing success status, response message and the reminder id.</returns>
    public async Task<(bool success, string message, int id)> CreateReminderAsync(
        ulong targetId,
        bool isPrivate,
        TimeSpan ts,
        string message,
        ulong userId,
        ulong? serverId,
        bool sanitizeMentions,
        TimeSpan? repeat = null)
    {
        if (ts > TimeSpan.FromDays(60))
            return (false, strings.RemindTooLong(serverId ?? 0), 0);

        if (repeat.HasValue && (repeat.Value < MinRepeat || repeat.Value > TimeSpan.FromDays(60)))
            return (false, strings.RemindRepeatRange(serverId ?? 0), 0);

        var time = DateTime.UtcNow + ts;

        if (sanitizeMentions)
        {
            message = message.SanitizeAllMentions();
        }

        var rem = new Reminder
        {
            ChannelId = targetId,
            IsPrivate = isPrivate,
            When = time,
            Message = message,
            UserId = userId,
            ServerId = serverId ?? 0,
            DateAdded = DateTime.UtcNow,
            RepeatMinutes = repeat.HasValue ? (int)repeat.Value.TotalMinutes : 0
        };

        await using var db = await dbFactory.CreateConnectionAsync();

        // Insert using LinqToDB
        rem.Id = await db.InsertWithInt32IdentityAsync(rem);

        await ScheduleReminder(rem);

        var gTime = serverId == null
            ? time
            : TimeZoneInfo.ConvertTime(time, tz.GetTimeZoneOrUtc(serverId.Value));

        var unixTime = gTime.ToUnixEpochDate();
        var response = $"⏰ {strings.Remind(serverId ?? 0,
            Format.Bold(!isPrivate ? $"<#{targetId}>" : userId.ToString()),
            Format.Bold(message),
            $"<t:{unixTime}:R>")}";

        if (rem.RepeatMinutes > 0)
            response += $"\n{strings.RemindRepeats(serverId ?? 0, TimeSpan.FromMinutes(rem.RepeatMinutes).Humanize())}";

        return (true, response, rem.Id);
    }

    /// <summary>
    ///     Builds the button that lets other members subscribe to a reminder.
    /// </summary>
    /// <param name="reminderId">The id of the reminder.</param>
    /// <param name="guildId">The guild the reminder was created in.</param>
    /// <returns>A component builder holding the subscribe button.</returns>
    public ComponentBuilder SubscribeButton(int reminderId, ulong guildId)
    {
        return new ComponentBuilder().WithButton(strings.RemindSubscribeButton(guildId), $"remindsub:{reminderId}");
    }

    /// <summary>
    ///     Sets or clears how often a user's reminder repeats.
    /// </summary>
    /// <param name="userId">The owner of the reminder.</param>
    /// <param name="index">The zero based position of the reminder in the user's reminder list.</param>
    /// <param name="repeat">The repeat interval, or null to stop repeating.</param>
    /// <returns>True if the reminder was found and updated.</returns>
    public async Task<bool> SetRepeatAsync(ulong userId, int index, TimeSpan? repeat)
    {
        var rems = await GetUserRemindersAsync(userId);
        if (index < 0 || index >= rems.Count)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.Reminders
            .Where(r => r.Id == rems[index].Id)
            .Set(r => r.RepeatMinutes, repeat.HasValue ? (int)repeat.Value.TotalMinutes : 0)
            .UpdateAsync();

        return true;
    }

    /// <summary>
    ///     Subscribes a user to a reminder so they get pinged along with the owner.
    /// </summary>
    /// <param name="userId">The user subscribing.</param>
    /// <param name="reminderId">The id of the reminder.</param>
    /// <returns>The outcome of the subscription.</returns>
    public async Task<SubscribeResult> SubscribeAsync(ulong userId, int reminderId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var rem = await db.Reminders.FirstOrDefaultAsync(r => r.Id == reminderId);
        if (rem == null)
            return SubscribeResult.NotFound;

        if (rem.UserId == userId)
            return SubscribeResult.Owner;

        var subscribers = ParseSubscribers(rem.Subscribers);
        if (subscribers.Contains(userId))
            return SubscribeResult.AlreadySubscribed;

        subscribers.Add(userId);
        await db.Reminders
            .Where(r => r.Id == reminderId)
            .Set(r => r.Subscribers, string.Join(",", subscribers))
            .UpdateAsync();

        return SubscribeResult.Subscribed;
    }

    /// <summary>
    ///     Removes a user's subscription from a reminder.
    /// </summary>
    /// <param name="userId">The user unsubscribing.</param>
    /// <param name="reminderId">The id of the reminder.</param>
    /// <returns>True if the user was subscribed and is now removed.</returns>
    public async Task<bool> UnsubscribeAsync(ulong userId, int reminderId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var rem = await db.Reminders.FirstOrDefaultAsync(r => r.Id == reminderId);
        if (rem == null)
            return false;

        var subscribers = ParseSubscribers(rem.Subscribers);
        if (!subscribers.Remove(userId))
            return false;

        await db.Reminders
            .Where(r => r.Id == reminderId)
            .Set(r => r.Subscribers, string.Join(",", subscribers))
            .UpdateAsync();

        return true;
    }

    private static List<ulong> ParseSubscribers(string? subscribers)
    {
        return (subscribers ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => ulong.TryParse(x, out var id) ? id : 0)
            .Where(x => x != 0)
            .ToList();
    }

    /// <summary>
    ///     Retrieves all reminders for a specific user.
    /// </summary>
    /// <param name="userId">The ID of the user whose reminders to retrieve.</param>
    /// <returns>A list of all reminders for the specified user.</returns>
    public async Task<List<Reminder>> GetUserRemindersAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.Reminders
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.When)
            .ToListAsync();
    }

    /// <summary>
    ///     Deletes a specific reminder for a user.
    /// </summary>
    /// <param name="userId">The ID of the user who owns the reminder.</param>
    /// <param name="index">The index of the reminder to delete.</param>
    /// <returns>True if the reminder was deleted successfully, false if it wasn't found.</returns>
    public async Task<bool> DeleteReminderAsync(ulong userId, int index)
    {
        if (index < 0)
            return false;

        var rems = await GetUserRemindersAsync(userId);

        if (rems.Count <= index)
            return false;

        var rem = rems[index];

        await using var db = await dbFactory.CreateConnectionAsync();

        // Delete using LinqToDB
        await db.Reminders
            .Where(r => r.Id == rem.Id)
            .DeleteAsync();

        if (reminderTimers.TryRemove(rem.Id, out var timer))
        {
            await timer.DisposeAsync();
        }

        return true;
    }

    /// <summary>
    ///     Parses a remind command input and extracts the reminder details.
    /// </summary>
    /// <param name="input">The input string containing the remind command and its parameters.</param>
    /// <param name="obj">When this method returns, contains the reminder object created from the input.</param>
    /// <returns>true if the input could be parsed; otherwise, false.</returns>
    /// <remarks>
    ///     The method uses a regular expression to parse the input and extract reminder details like time and message.
    /// </remarks>
    public bool TryParseRemindMessage(string input, out RemindObject obj)
    {
        TimeSpan? repeat = null;
        var every = EveryRegex().Match(input);
        if (every.Success
            && TryParseTimeAndMessage(every.Groups["head"].Value + " x", out var headObj) && headObj.What == "x"
            && TryParseTimeAndMessage(every.Groups["iv"].Value + " x", out var intervalObj) && intervalObj.What == "x")
        {
            repeat = intervalObj.Time;
            input = $"{every.Groups["head"].Value} {every.Groups["tail"].Value}";
        }

        var parsed = TryParseTimeAndMessage(input, out obj);
        if (parsed)
            obj.Repeat = repeat;
        return parsed;
    }

    private bool TryParseTimeAndMessage(string input, out RemindObject obj)
    {
        var m = MyRegex().Match(input);

        obj = default;
        if (m.Length == 0) return false;

        var values = new Dictionary<string, int>();

        var what = m.Groups["what"].Value;

        if (string.IsNullOrWhiteSpace(what))
        {
            logger.LogWarning("No message provided for the reminder");
            return false;
        }

        foreach (var groupName in regex.GetGroupNames())
        {
            if (groupName is "0" or "what") continue;
            if (string.IsNullOrWhiteSpace(m.Groups[groupName].Value))
            {
                values[groupName] = 0;
                continue;
            }

            if (!int.TryParse(m.Groups[groupName].Value, out var value))
            {
                logger.LogWarning($"Reminder regex group {groupName} has invalid value.");
                return false;
            }

            if (value < 1)
            {
                logger.LogWarning("Reminder time value has to be an integer greater than 0");
                return false;
            }

            values[groupName] = value;
        }

        var ts = new TimeSpan
        (
            30 * values["mo"] + 7 * values["w"] + values["d"],
            values["h"],
            values["m"],
            values["s"]
        );

        obj = new RemindObject
        {
            Time = ts, What = what
        };

        return true;
    }

    [GeneratedRegex(
        @"^(?:in\s?)?\s*(?:(?<mo>\d+)(?:\s?(?:months?|mos?),?))?(?:(?:\sand\s|\s*)?(?<w>\d+)(?:\s?(?:weeks?|w),?))?(?:(?:\sand\s|\s*)?(?<d>\d+)(?:\s?(?:days?|d),?))?(?:(?:\sand\s|\s*)?(?<h>\d+)(?:\s?(?:hours?|h),?))?(?:(?:\sand\s|\s*)?(?<m>\d+)(?:\s?(?:minutes?|mins?|m),?))?(?:(?:\sand\s|\s*)?(?<s>\d+)(?:\s?(?:seconds?|secs?|s),?))?\s+(?:to:?\s+)?(?<what>(?:\r\n|[\r\n]|.)+)",
        RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex MyRegex();

    [GeneratedRegex(
        @"^(?<head>(?:(?!\bto\b)[^\r\n])+?)\s+every\s+(?<iv>\d+\s?(?:months?|mos?|weeks?|w|days?|d|hours?|h|minutes?|mins?|m|seconds?|secs?|s)(?:(?:,?\s?(?:and\s)?)\d+\s?(?:months?|mos?|weeks?|w|days?|d|hours?|h|minutes?|mins?|m|seconds?|secs?|s))*)\s+(?<tail>(?:\r\n|[\r\n]|.)+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EveryRegex();

    /// <summary>
    ///     Represents the details of a reminder, including what the reminder is for and the time until the reminder should
    ///     occur.
    /// </summary>
    public struct RemindObject
    {
        /// <summary>
        ///     Gets or sets the message or content of the reminder.
        /// </summary>
        /// <value>
        ///     The content of the reminder, describing what the reminder is for.
        /// </value>
        public string? What { get; set; }

        /// <summary>
        ///     Gets or sets the duration of time until the reminder should be triggered.
        /// </summary>
        /// <value>
        ///     A <see cref="TimeSpan" /> representing the amount of time until the reminder occurs.
        /// </value>
        /// <remarks>
        ///     This value is used to calculate the specific datetime when the reminder will be triggered, based on the current
        ///     time plus the TimeSpan.
        /// </remarks>
        public TimeSpan Time { get; set; }

        /// <summary>
        ///     Gets or sets how often the reminder repeats, or null to fire once.
        /// </summary>
        public TimeSpan? Repeat { get; set; }
    }
}