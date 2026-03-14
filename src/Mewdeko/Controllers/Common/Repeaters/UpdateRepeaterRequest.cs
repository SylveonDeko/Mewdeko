using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Controllers.Common.Repeaters;

/// <summary>
///     Request model for updating an existing repeater
/// </summary>
public class UpdateRepeaterRequest
{
    /// <summary>
    ///     The new message content (null to keep existing)
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    ///     The new channel ID (null to keep existing)
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     The new interval (null to keep existing)
    /// </summary>
    public string? Interval { get; set; }

    /// <summary>
    ///     Whether to allow mentions in the message (null to keep existing)
    /// </summary>
    public bool? AllowMentions { get; set; }

    /// <summary>
    ///     The new trigger mode (null to keep existing)
    /// </summary>
    public StickyTriggerMode? TriggerMode { get; set; }

    /// <summary>
    ///     The new activity threshold (null to keep existing)
    /// </summary>
    public int? ActivityThreshold { get; set; }

    /// <summary>
    ///     The new activity time window (null to keep existing)
    /// </summary>
    public string? ActivityTimeWindow { get; set; }

    /// <summary>
    ///     Whether to enable conversation detection (null to keep existing)
    /// </summary>
    public bool? ConversationDetection { get; set; }

    /// <summary>
    ///     The new conversation threshold (null to keep existing)
    /// </summary>
    public int? ConversationThreshold { get; set; }

    /// <summary>
    ///     The new priority (null to keep existing)
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    ///     The new queue position (null to keep existing)
    /// </summary>
    public int? QueuePosition { get; set; }

    /// <summary>
    ///     Whether redundancy checking is enabled (null to keep existing)
    /// </summary>
    public bool? NoRedundant { get; set; }

    /// <summary>
    ///     Whether the repeater is enabled (null to keep existing)
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    ///     Time-based scheduling preset or custom JSON (null to keep existing)
    /// </summary>
    public string? TimeConditions { get; set; }

    /// <summary>
    ///     Maximum age before auto-expiry (null to keep existing)
    /// </summary>
    public string? MaxAge { get; set; }

    /// <summary>
    ///     Maximum displays before auto-expiry (null to keep existing)
    /// </summary>
    public int? MaxTriggers { get; set; }

    /// <summary>
    ///     Whether to auto-create in threads (null to keep existing)
    /// </summary>
    public bool? ThreadAutoSticky { get; set; }

    /// <summary>
    ///     Whether to only post in threads, not parent channels (null to keep existing)
    /// </summary>
    public bool? ThreadOnlyMode { get; set; }

    /// <summary>
    ///     Forum tag conditions JSON (null to keep existing)
    /// </summary>
    public string? ForumTagConditions { get; set; }

    /// <summary>
    ///     Whether to suppress notifications (null to keep existing)
    /// </summary>
    public bool? SuppressNotifications { get; set; }
}