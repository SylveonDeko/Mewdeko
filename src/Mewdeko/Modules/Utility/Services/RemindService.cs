using System.Text.RegularExpressions;
using System.Threading;
using Mewdeko.Database.DbContextStuff;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
/// Manages and executes reminders for users at specified times.
/// </summary>
public partial class RemindService : INService
{
    private readonly DiscordShardedClient client;
    private readonly IBotCredentials creds;
    private readonly DbContextProvider dbProvider;
    private readonly ConcurrentDictionary<int, Timer> reminderTimers;

    private readonly Regex regex = MyRegex();

    /// <summary>
    /// Initializes the reminder service, starting the background task to check for and execute reminders.
    /// </summary>
    /// <param name="client">The Discord client used for sending reminder notifications.</param>
    /// <param name="db">The database service for managing reminders.</param>
    /// <param name="creds">The bot's credentials, used for shard management and distribution of tasks.</param>
    public RemindService(DiscordShardedClient client, DbContextProvider dbProvider, IBotCredentials creds)
    {
        this.client = client;
        this.dbProvider = dbProvider;
        this.creds = creds;
        this.reminderTimers = new ConcurrentDictionary<int, Timer>();
        _ = InitializeRemindersAsync();
    }

    /// <summary>
    /// Initializes the reminders by loading them from the database and setting timers.
    /// </summary>
    private async Task InitializeRemindersAsync()
    {
        var now = DateTime.UtcNow;
        var reminders = await GetRemindersBeforeAsync(now);

        foreach (var reminder in reminders)
        {
            ScheduleReminder(reminder);
        }
    }

    /// <summary>
    /// Schedules a reminder by setting a timer.
    /// </summary>
    /// <param name="reminder">The reminder to be scheduled.</param>
    private void ScheduleReminder(Reminder reminder)
    {
        var timeToGo = reminder.When - DateTime.UtcNow;
        if (timeToGo <= TimeSpan.Zero)
        {
            timeToGo = TimeSpan.Zero;
        }

        var timer = new Timer(async _ => await ExecuteReminderAsync(reminder), null, timeToGo, Timeout.InfiniteTimeSpan);
        reminderTimers[reminder.Id] = timer;
    }

    /// <summary>
    /// Executes the reminder action.
    /// </summary>
    /// <param name="reminder">The reminder to be executed.</param>
    private async Task ExecuteReminderAsync(Reminder reminder)
    {
        try
        {
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

            await ch.EmbedAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle("Reminder")
                    .AddField("Created At", reminder.DateAdded.HasValue ? reminder.DateAdded.Value.ToLongDateString() : "?")
                    .AddField("By",
                        (await ch.GetUserAsync(reminder.UserId).ConfigureAwait(false))?.ToString() ?? reminder.UserId.ToString()),
                reminder.Message).ConfigureAwait(false);

            // Remove the executed reminder from the database and timer
            await RemoveReminder(reminder);
        }
        catch (Exception ex)
        {
            Log.Information(ex.Message + $"({reminder.Id})");
        }
    }

    /// <summary>
    /// Removes the reminder from the database and disposes of its timer.
    /// </summary>
    /// <param name="reminder">The reminder to be removed.</param>
    private async Task RemoveReminder(Reminder reminder)
    {
        if (reminderTimers.TryRemove(reminder.Id, out var timer))
        {
            timer.Dispose();
        }

        await using var dbContext = await dbProvider.GetContextAsync();

        dbContext.Set<Reminder>().Remove(reminder);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves reminders that are scheduled to be executed before the specified time.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <returns>A list of reminders scheduled before the specified time.</returns>
    private async Task<List<Reminder>> GetRemindersBeforeAsync(DateTime now)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var reminders = await dbContext.Reminders
            .Where(x => x.When < now)
            .ToListAsync();
        return reminders;
    }

    /// <summary>
    /// Parses a remind command input and extracts the reminder details.
    /// </summary>
    /// <param name="input">The input string containing the remind command and its parameters.</param>
    /// <param name="obj">When this method returns, contains the reminder object created from the input.</param>
    /// <returns>true if the input could be parsed; otherwise, false.</returns>
    /// <remarks>
    /// The method uses a regular expression to parse the input and extract reminder details like time and message.
    /// </remarks>
    public bool TryParseRemindMessage(string input, out RemindObject obj)
    {
        var m = regex.Match(input);

        obj = default;
        if (m.Length == 0) return false;

        var values = new Dictionary<string, int>();

        var what = m.Groups["what"].Value;

        if (string.IsNullOrWhiteSpace(what))
        {
            Log.Warning("No message provided for the reminder");
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
                Log.Warning($"Reminder regex group {groupName} has invalid value.");
                return false;
            }

            if (value < 1)
            {
                Log.Warning("Reminder time value has to be an integer greater than 0");
                return false;
            }

            values[groupName] = value;
        }

        var ts = new TimeSpan
        (
            30 * values["mo"] + 7 * values["w"] + values["d"],
            values["h"],
            values["m"],
            0
        );

        obj = new RemindObject
        {
            Time = ts, What = what
        };

        return true;
    }

    [GeneratedRegex(
        @"^(?:in\s?)?\s*(?:(?<mo>\d+)(?:\s?(?:months?|mos?),?))?(?:(?:\sand\s|\s*)?(?<w>\d+)(?:\s?(?:weeks?|w),?))?(?:(?:\sand\s|\s*)?(?<d>\d+)(?:\s?(?:days?|d),?))?(?:(?:\sand\s|\s*)?(?<h>\d+)(?:\s?(?:hours?|h),?))?(?:(?:\sand\s|\s*)?(?<m>\d+)(?:\s?(?:minutes?|mins?|m),?))?\s+(?:to:?\s+)?(?<what>(?:\r\n|[\r\n]|.)+)",
        RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex MyRegex();

    /// <summary>
    /// Represents the details of a reminder, including what the reminder is for and the time until the reminder should occur.
    /// </summary>
    public struct RemindObject
    {
        /// <summary>
        /// Gets or sets the message or content of the reminder.
        /// </summary>
        /// <value>
        /// The content of the reminder, describing what the reminder is for.
        /// </value>
        public string? What { get; set; }

        /// <summary>
        /// Gets or sets the duration of time until the reminder should be triggered.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> representing the amount of time until the reminder occurs.
        /// </value>
        /// <remarks>
        /// This value is used to calculate the specific datetime when the reminder will be triggered, based on the current time plus the TimeSpan.
        /// </remarks>
        public TimeSpan Time { get; set; }
    }
}
