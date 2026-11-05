namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

/// <summary>
///     Specifies the type of the stream.
/// </summary>
public enum FType
{
    /// <summary>
    ///     Twitch stream.
    /// </summary>
    Twitch = 0,

    /// <summary>
    ///     Picarto stream.
    /// </summary>
    Picarto = 3,

    /// <summary>
    ///     YouTube stream.
    /// </summary>
    Youtube = 4,

    /// <summary>
    ///     Facebook stream.
    /// </summary>
    Facebook = 5,

    /// <summary>
    ///     Trovo stream.
    /// </summary>
    Trovo = 6,

    /// <summary>
    ///     Kick stream.
    /// </summary>
    Kick = 7,

    /// <summary>
    ///     TikTok account, covering live streams and new videos.
    /// </summary>
    TikTok = 8,

    /// <summary>
    ///     Bluesky account, covering new posts.
    /// </summary>
    Bluesky = 9,

    /// <summary>
    ///     Subreddit, covering new posts.
    /// </summary>
    Reddit = 10,

    /// <summary>
    ///     Podcast, covering new episodes.
    /// </summary>
    Podcast = 11,

    /// <summary>
    ///     YouTube channel, covering new uploads.
    /// </summary>
    YoutubeUploads = 12
}