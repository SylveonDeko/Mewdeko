using Mewdeko.Database.Common;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

/// <summary>
///     Represents data related to a streamed broadcast from various platforms.
/// </summary>
public record StreamData
{
    /// <summary>
    ///     Gets or sets the type of the stream based on the platform.
    /// </summary>
    public FType StreamType { get; set; }

    /// <summary>
    ///     Gets or sets the display name of the streamer.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Gets or sets the unique identifier or username of the streamer.
    /// </summary>
    public string UniqueName { get; set; }

    /// <summary>
    ///     Gets or sets the current viewer count of the stream.
    /// </summary>
    public int Viewers { get; set; }

    /// <summary>
    ///     Gets or sets the title of the stream.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    ///     Gets or sets the name of the game or category being broadcast.
    /// </summary>
    public string Game { get; set; }

    /// <summary>
    ///     Gets or sets the URL of the stream's preview image.
    /// </summary>
    public string Preview { get; set; }

    /// <summary>
    ///     Indicates whether the stream is currently live.
    /// </summary>
    public bool IsLive { get; set; }

    /// <summary>
    ///     Gets or sets the URL to the stream's page on the platform.
    /// </summary>
    public string StreamUrl { get; set; }

    /// <summary>
    ///     Gets or sets the URL of the streamer's avatar image.
    /// </summary>
    public string AvatarUrl { get; set; }

    /// <summary>
    ///     The id of the users channel
    /// </summary>
    public string ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the identifier of the account's most recent video, for platforms that report new uploads.
    /// </summary>
    public string? LatestVideoId { get; set; }

    /// <summary>
    ///     Gets or sets the URL of the account's most recent video.
    /// </summary>
    public string? LatestVideoUrl { get; set; }

    /// <summary>
    ///     Gets or sets the title or caption of the account's most recent video.
    /// </summary>
    public string? LatestVideoTitle { get; set; }

    /// <summary>
    ///     Gets or sets the thumbnail URL of the account's most recent video.
    /// </summary>
    public string? LatestVideoThumbnail { get; set; }

    /// <summary>
    ///     Gets or sets the description, post text or show notes of the account's most recent video, post or episode.
    /// </summary>
    public string? LatestVideoDescription { get; set; }

    /// <summary>
    ///     Gets or sets when the account's most recent video, post or episode was published.
    /// </summary>
    public DateTimeOffset? LatestVideoPublished { get; set; }

    /// <summary>
    ///     Gets or sets the media URL of the most recent item, such as a podcast episode's audio file.
    /// </summary>
    public string? LatestVideoMedia { get; set; }

    /// <summary>
    ///     Gets or sets the items published since the previous check, oldest first, capped per check.
    /// </summary>
    public IReadOnlyList<StreamItem>? NewItems { get; set; }

    /// <summary>
    ///     Gets or sets how many new items were left out of <see cref="NewItems" /> by the cap.
    /// </summary>
    public int SkippedItems { get; set; }

    /// <summary>
    ///     Gets or sets the feed URL this source is read from, for sources whose key cannot hold it.
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    ///     Generates a key for identifying the stream data uniquely.
    /// </summary>
    /// <returns>A <see cref="StreamDataKey" /> composed of the stream type and lowercased unique name.</returns>
    public StreamDataKey CreateKey()
    {
        return new StreamDataKey(StreamType, UniqueName.ToLower());
    }
}