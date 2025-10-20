using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

/// <summary>
///     Represents the API response when fetching channel data from Kick.
/// </summary>
public class KickChannelResponse
{
    /// <summary>
    ///     The list of channel data returned by the API.
    /// </summary>
    [JsonPropertyName("data")]
    public List<KickChannelData> Data { get; set; } = [];

    /// <summary>
    ///     Optional message from the API.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
///     Represents channel data from Kick API.
/// </summary>
public class KickChannelData
{
    /// <summary>
    ///     The broadcaster's user ID.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public long BroadcasterUserId { get; set; }

    /// <summary>
    ///     URL of the channel's banner picture.
    /// </summary>
    [JsonPropertyName("banner_picture")]
    public string? BannerPicture { get; set; }

    /// <summary>
    ///     The channel's slug (username).
    /// </summary>
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    ///     The channel description.
    /// </summary>
    [JsonPropertyName("channel_description")]
    public string? ChannelDescription { get; set; }

    /// <summary>
    ///     The current stream title.
    /// </summary>
    [JsonPropertyName("stream_title")]
    public string? StreamTitle { get; set; }

    /// <summary>
    ///     The category/game being streamed.
    /// </summary>
    [JsonPropertyName("category")]
    public KickCategory? Category { get; set; }

    /// <summary>
    ///     The stream data if the channel is live.
    /// </summary>
    [JsonPropertyName("stream")]
    public KickStream? Stream { get; set; }
}

/// <summary>
///     Represents a category (game) on Kick.
/// </summary>
public class KickCategory
{
    /// <summary>
    ///     The category ID.
    /// </summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    ///     The category name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     URL to the category thumbnail.
    /// </summary>
    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }
}

/// <summary>
///     Represents stream data from Kick.
/// </summary>
public class KickStream
{
    /// <summary>
    ///     Indicates whether the stream is currently live.
    /// </summary>
    [JsonPropertyName("is_live")]
    public bool IsLive { get; set; }

    /// <summary>
    ///     Indicates whether the stream is marked as mature content.
    /// </summary>
    [JsonPropertyName("is_mature")]
    public bool IsMature { get; set; }

    /// <summary>
    ///     The stream key.
    /// </summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>
    ///     The language of the stream.
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>
    ///     The time the stream started.
    /// </summary>
    [JsonPropertyName("start_time")]
    public string? StartTime { get; set; }

    /// <summary>
    ///     URL to the stream thumbnail/preview.
    /// </summary>
    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    /// <summary>
    ///     The stream URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    ///     The current viewer count.
    /// </summary>
    [JsonPropertyName("viewer_count")]
    public int ViewerCount { get; set; }
}