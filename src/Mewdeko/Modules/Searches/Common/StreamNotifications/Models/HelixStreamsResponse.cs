#nullable disable
using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

/// <summary>
///     Represents the response from the Twitch Helix API for stream data.
/// </summary>
public class HelixStreamsResponse
{
    /// <summary>
    ///     The list of streams returned in the response.
    /// </summary>
    [JsonPropertyName("data")]
    public List<StreamData> Data { get; set; }

    /// <summary>
    ///     Pagination data to retrieve the next set of streams.
    /// </summary>
    [JsonPropertyName("pagination")]
    public PaginationData Pagination { get; set; }

    /// <summary>
    ///     Contains pagination data for navigating through stream results.
    /// </summary>
    public class PaginationData
    {
        /// <summary>
        ///     The cursor used to paginate through the list of streams.
        /// </summary>
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }
    }

    /// <summary>
    ///     Represents individual stream information returned by the Helix API.
    /// </summary>
    public class StreamData
    {
        /// <summary>
        ///     The unique identifier of the stream.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        ///     The unique identifier of the user broadcasting the stream.
        /// </summary>
        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        /// <summary>
        ///     The login name of the user broadcasting the stream.
        /// </summary>
        [JsonPropertyName("user_login")]
        public string UserLogin { get; set; }

        /// <summary>
        ///     The display name of the user broadcasting the stream.
        /// </summary>
        [JsonPropertyName("user_name")]
        public string UserName { get; set; }

        /// <summary>
        ///     The unique identifier of the game being played on the stream.
        /// </summary>
        [JsonPropertyName("game_id")]
        public string GameId { get; set; }

        /// <summary>
        ///     The name of the game being played on the stream.
        /// </summary>
        [JsonPropertyName("game_name")]
        public string GameName { get; set; }

        /// <summary>
        ///     The type of the stream (e.g., "live").
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        ///     The title of the stream.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; }

        /// <summary>
        ///     The number of viewers currently watching the stream.
        /// </summary>
        [JsonPropertyName("viewer_count")]
        public int ViewerCount { get; set; }

        /// <summary>
        ///     The UTC timestamp when the stream started.
        /// </summary>
        [JsonPropertyName("started_at")]
        public DateTime StartedAt { get; set; }

        /// <summary>
        ///     The language of the stream.
        /// </summary>
        [JsonPropertyName("language")]
        public string Language { get; set; }

        /// <summary>
        ///     The URL of the stream's thumbnail image.
        /// </summary>
        [JsonPropertyName("thumbnail_url")]
        public string ThumbnailUrl { get; set; }

        /// <summary>
        ///     The list of tag IDs associated with the stream.
        /// </summary>
        [JsonPropertyName("tag_ids")]
        public List<string> TagIds { get; set; }

        /// <summary>
        ///     Indicates whether the stream is marked as mature content.
        /// </summary>
        [JsonPropertyName("is_mature")]
        public bool IsMature { get; set; }
    }
}