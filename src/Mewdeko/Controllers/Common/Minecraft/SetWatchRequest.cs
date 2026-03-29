namespace Mewdeko.Controllers.Common.Minecraft;

/// <summary>
///     Request model for configuring server watch/monitoring.
/// </summary>
public class SetWatchRequest
{
    /// <summary>
    ///     The channel ID to post status updates in. Null to disable watching.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     The interval in minutes between status updates. Minimum 1.
    /// </summary>
    public int? Interval { get; set; }

    /// <summary>
    ///     The watch mode. 0 = Embed, 1 = ChannelTopic, 2 = Both.
    /// </summary>
    public int? WatchMode { get; set; }
}