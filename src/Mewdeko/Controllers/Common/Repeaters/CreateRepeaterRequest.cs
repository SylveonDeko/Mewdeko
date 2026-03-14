using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Controllers.Common.Repeaters;

/// <summary>
///     Request model for creating a new repeater
/// </summary>
public class CreateRepeaterRequest
{
    /// <summary>
    ///     The channel ID where messages will be sent
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The message content to repeat
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    ///     The interval between messages (e.g., "00:05:00" for 5 minutes)
    /// </summary>
    public string Interval { get; set; } = "00:05:00";

    /// <summary>
    ///     Optional start time of day for daily repeaters (e.g., "09:00")
    /// </summary>
    public string? StartTimeOfDay { get; set; }

    /// <summary>
    ///     Whether to enable redundant message checking
    /// </summary>
    public bool NoRedundant { get; set; } = false;

    /// <summary>
    ///     Whether to allow mentions in the message
    /// </summary>
    public bool AllowMentions { get; set; } = false;

    /// <summary>
    ///     The trigger mode for the repeater
    /// </summary>
    public StickyTriggerMode TriggerMode { get; set; } = StickyTriggerMode.TimeInterval;

    /// <summary>
    ///     Number of messages needed to trigger activity-based modes
    /// </summary>
    public int ActivityThreshold { get; set; } = 5;

    /// <summary>
    ///     Time window for activity detection
    /// </summary>
    public string ActivityTimeWindow { get; set; } = "00:05:00";

    /// <summary>
    ///     Whether to enable conversation detection
    /// </summary>
    public bool ConversationDetection { get; set; } = false;

    /// <summary>
    ///     Messages per minute threshold for active conversation
    /// </summary>
    public int ConversationThreshold { get; set; } = 3;

    /// <summary>
    ///     Display priority (0-100, higher = more important)
    /// </summary>
    public int Priority { get; set; } = 50;

    /// <summary>
    ///     Time-based scheduling preset (business, evening, weekend, or custom JSON)
    /// </summary>
    public string? TimeSchedulePreset { get; set; }

    /// <summary>
    ///     Custom time conditions as JSON string
    /// </summary>
    public string? TimeConditions { get; set; }

    /// <summary>
    ///     Maximum age before auto-expiry (e.g., "7.00:00:00" for 7 days)
    /// </summary>
    public string? MaxAge { get; set; }

    /// <summary>
    ///     Maximum number of displays before auto-expiry
    /// </summary>
    public int? MaxTriggers { get; set; }

    /// <summary>
    ///     Whether to auto-create in new threads
    /// </summary>
    public bool ThreadAutoSticky { get; set; } = false;

    /// <summary>
    ///     Whether this repeater only posts in threads, not parent channels
    /// </summary>
    public bool ThreadOnlyMode { get; set; } = false;

    /// <summary>
    ///     JSON string of forum tag conditions
    /// </summary>
    public string? ForumTagConditions { get; set; }

    /// <summary>
    ///     Whether to suppress notifications when sending messages
    /// </summary>
    public bool SuppressNotifications { get; set; } = false;
}