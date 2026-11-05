using System.Text.Json.Serialization;
using DataModel;

namespace Mewdeko.Controllers.Common.ChannelTools;

/// <summary>
///     A channel's auto-purge settings, as read and as written.
/// </summary>
public class AutoPurgeDto
{
    /// <summary>
    ///     The channel that is purged.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     How old a message must be, in minutes, before it is deleted, from 1 to 525600.
    /// </summary>
    public int MaxAgeMinutes { get; set; } = 1440;

    /// <summary>
    ///     How often the channel is purged, in minutes, from 5 to 1440.
    /// </summary>
    public int IntervalMinutes { get; set; } = 10;

    /// <summary>
    ///     All, Humans, Bots, Links or Attachments.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AutoPurgeFilter Filter { get; set; }

    /// <summary>
    ///     Whether pinned messages are kept.
    /// </summary>
    public bool KeepPinned { get; set; } = true;

    /// <summary>
    ///     When the channel was last purged, or null if it has not been.
    /// </summary>
    public DateTime? LastRunAt { get; set; }
}

/// <summary>
///     A voice channel and the text channel it is linked to.
/// </summary>
public class VoiceTextLinkDto
{
    /// <summary>
    ///     The voice channel.
    /// </summary>
    public ulong VoiceChannelId { get; set; }

    /// <summary>
    ///     The text channel members get access to while they are in the voice channel.
    /// </summary>
    public ulong TextChannelId { get; set; }
}

/// <summary>
///     A channel that is locked or hidden.
/// </summary>
public class ChannelTimedActionDto
{
    /// <summary>
    ///     The channel.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Lock or Hide.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ChannelTimedActionKind Kind { get; set; }

    /// <summary>
    ///     When it is undone, or null if it lasts until undone by hand.
    /// </summary>
    public DateTime? EndsAt { get; set; }

    /// <summary>
    ///     The user who started it.
    /// </summary>
    public ulong CreatedBy { get; set; }
}
