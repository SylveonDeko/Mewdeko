using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Controllers.Common.Repeaters;

/// <summary>
///     Response model for a repeater configuration
/// </summary>
public class RepeaterResponse
{
    /// <summary>
    ///     The unique ID of the repeater
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The channel ID where messages are sent
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The message content to repeat
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    ///     The interval between messages as a TimeSpan string
    /// </summary>
    public string Interval { get; set; } = string.Empty;

    /// <summary>
    ///     Optional start time of day for daily repeaters
    /// </summary>
    public string? StartTimeOfDay { get; set; }

    /// <summary>
    ///     Whether redundant message checking is enabled
    /// </summary>
    public bool NoRedundant { get; set; }

    /// <summary>
    ///     Whether the repeater is currently enabled
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    ///     The trigger mode for the repeater
    /// </summary>
    public StickyTriggerMode TriggerMode { get; set; }

    /// <summary>
    ///     Number of messages needed to trigger activity-based modes
    /// </summary>
    public int ActivityThreshold { get; set; }

    /// <summary>
    ///     Time window for activity detection
    /// </summary>
    public string ActivityTimeWindow { get; set; } = "00:05:00";

    /// <summary>
    ///     Whether conversation detection is enabled
    /// </summary>
    public bool ConversationDetection { get; set; }

    /// <summary>
    ///     Messages per minute threshold for active conversation
    /// </summary>
    public int ConversationThreshold { get; set; }

    /// <summary>
    ///     Display priority (0-100, higher = more important)
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    ///     Queue position for rotation within same priority
    /// </summary>
    public int QueuePosition { get; set; }

    /// <summary>
    ///     JSON string of time conditions for scheduling
    /// </summary>
    public string? TimeConditions { get; set; }

    /// <summary>
    ///     Maximum age before auto-expiry
    /// </summary>
    public string? MaxAge { get; set; }

    /// <summary>
    ///     Maximum number of displays before auto-expiry
    /// </summary>
    public int? MaxTriggers { get; set; }

    /// <summary>
    ///     Whether to auto-create in new threads
    /// </summary>
    public bool ThreadAutoSticky { get; set; }

    /// <summary>
    ///     Whether this repeater only posts in threads, not parent channels
    /// </summary>
    public bool ThreadOnlyMode { get; set; }

    /// <summary>
    ///     Whether notifications from this repeater are suppressed
    /// </summary>
    public bool SuppressNotifications { get; set; }

    /// <summary>
    ///     JSON string of forum tag conditions
    /// </summary>
    public string? ForumTagConditions { get; set; }

    /// <summary>
    ///     Number of times the repeater has been displayed
    /// </summary>
    public int DisplayCount { get; set; }

    /// <summary>
    ///     When the repeater was last displayed
    /// </summary>
    public DateTime? LastDisplayed { get; set; }

    /// <summary>
    ///     When the repeater was created
    /// </summary>
    public DateTime? DateAdded { get; set; }

    /// <summary>
    ///     Next scheduled execution time
    /// </summary>
    public DateTime? NextExecution { get; set; }

    /// <summary>
    ///     The guild's timezone ID used for time-based scheduling
    /// </summary>
    public string? GuildTimezone { get; set; }

    /// <summary>
    ///     Whether time-based scheduling requires timezone setup
    /// </summary>
    public bool RequiresTimezone { get; set; }

    /// <summary>
    ///     JSON tracking of thread sticky message IDs
    /// </summary>
    public string? ThreadStickyMessages { get; set; }
}