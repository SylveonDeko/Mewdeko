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

    /// <summary>
    ///     Set to youtube, bluesky, reddit or podcast to follow uploads, posts or episodes instead of a live stream.
    ///     The URL then holds a URL, handle or name, or a feed URL for a podcast.
    /// </summary>
    public string? Platform { get; set; }

    /// <summary>
    ///     Whether Bluesky reposts count as new posts.
    /// </summary>
    public bool IncludeReposts { get; set; }
}