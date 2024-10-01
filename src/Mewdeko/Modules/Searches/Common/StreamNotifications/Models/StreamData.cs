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
    public FollowedStream.FType StreamType { get; set; }

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
    ///     Generates a key for identifying the stream data uniquely.
    /// </summary>
    /// <returns>A <see cref="StreamDataKey" /> composed of the stream type and lowercased unique name.</returns>
    public StreamDataKey CreateKey()
    {
        return new StreamDataKey(StreamType, UniqueName.ToLower());
    }
}