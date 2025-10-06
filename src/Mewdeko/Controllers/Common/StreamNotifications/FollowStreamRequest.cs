namespace Mewdeko.Controllers.Common.StreamNotifications;

/// <summary>
///     Request model for following a stream
/// </summary>
public class FollowStreamRequest
{
    /// <summary>
    ///     The channel ID where stream notifications should be posted
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The URL of the stream (Twitch, YouTube, etc.)
    /// </summary>
    public string Url { get; set; }
}