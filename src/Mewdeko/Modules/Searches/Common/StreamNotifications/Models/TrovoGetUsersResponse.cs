using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

/// <summary>
///     Represents the response data from a Trovo API call to fetch user details, including live stream information.
/// </summary>
public class TrovoGetUsersResponse
{
    /// <summary>
    ///     Indicates whether the user is currently live streaming.
    /// </summary>
    [JsonPropertyName("is_live")]
    public bool IsLive { get; set; }

    /// <summary>
    ///     The ID of the category under which the stream is listed.
    /// </summary>
    [JsonPropertyName("category_id")]
    public string CategoryId { get; set; }

    /// <summary>
    ///     The name of the category under which the stream is listed.
    /// </summary>
    [JsonPropertyName("category_name")]
    public string CategoryName { get; set; }

    /// <summary>
    ///     The title of the live stream.
    /// </summary>
    [JsonPropertyName("live_title")]
    public string LiveTitle { get; set; }

    /// <summary>
    ///     The audience type of the stream.
    /// </summary>
    [JsonPropertyName("audi_type")]
    public string AudiType { get; set; }

    /// <summary>
    ///     The language code of the stream.
    /// </summary>
    [JsonPropertyName("language_code")]
    public string LanguageCode { get; set; }

    /// <summary>
    ///     The URL of the stream's thumbnail image.
    /// </summary>
    [JsonPropertyName("thumbnail")]
    public string Thumbnail { get; set; }

    /// <summary>
    ///     The current number of viewers watching the live stream.
    /// </summary>
    [JsonPropertyName("current_viewers")]
    public int CurrentViewers { get; set; }

    /// <summary>
    ///     The total number of followers the streamer has.
    /// </summary>
    [JsonPropertyName("followers")]
    public int Followers { get; set; }

    /// <summary>
    ///     Additional information about the streamer.
    /// </summary>
    [JsonPropertyName("streamer_info")]
    public string StreamerInfo { get; set; }

    /// <summary>
    ///     The URL of the streamer's profile picture.
    /// </summary>
    [JsonPropertyName("profile_pic")]
    public string ProfilePic { get; set; }

    /// <summary>
    ///     The URL to the streamer's channel on Trovo.
    /// </summary>
    [JsonPropertyName("channel_url")]
    public string ChannelUrl { get; set; }

    /// <summary>
    ///     The creation date of the streamer's account.
    /// </summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; }

    /// <summary>
    ///     The number of subscribers the streamer has.
    /// </summary>
    [JsonPropertyName("subscriber_num")]
    public int SubscriberNum { get; set; }

    /// <summary>
    ///     The username of the streamer.
    /// </summary>
    [JsonPropertyName("username")]
    public string Username { get; set; }

    /// <summary>
    ///     A list of social links associated with the streamer.
    /// </summary>
    [JsonPropertyName("social_links")]
    public List<TrovoSocialLink> SocialLinks { get; set; }

    /// <summary>
    ///     The start time of the current or most recent stream.
    /// </summary>
    [JsonPropertyName("started_at")]
    public string StartedAt { get; set; }

    /// <summary>
    ///     The end time of the most recent stream.
    /// </summary>
    [JsonPropertyName("ended_at")]
    public string EndedAt { get; set; }
}