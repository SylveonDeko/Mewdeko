using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

/// <summary>
/// Represents the response from the Twitch Helix API for user data.
/// </summary>
public class HelixUsersResponse
{
    /// <summary>
    /// Represents individual user information returned by the Helix API.
    /// </summary>
    public class User
    {
        /// <summary>
        /// The unique identifier of the user.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// The login name of the user.
        /// </summary>
        [JsonPropertyName("login")]
        public string Login { get; set; }

        /// <summary>
        /// The display name of the user.
        /// </summary>
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        /// <summary>
        /// The type of the user.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// The broadcaster type of the user.
        /// </summary>
        [JsonPropertyName("broadcaster_type")]
        public string BroadcasterType { get; set; }

        /// <summary>
        /// The description of the user's profile.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// The URL of the user's profile image.
        /// </summary>
        [JsonPropertyName("profile_image_url")]
        public string ProfileImageUrl { get; set; }

        /// <summary>
        /// The URL of the user's offline image.
        /// </summary>
        [JsonPropertyName("offline_image_url")]
        public string OfflineImageUrl { get; set; }

        /// <summary>
        /// The total number of views of the user's channel.
        /// </summary>
        [JsonPropertyName("view_count")]
        public int ViewCount { get; set; }

        /// <summary>
        /// The email of the user.
        /// </summary>
        [JsonPropertyName("email")]
        public string Email { get; set; }

        /// <summary>
        /// The date and time when the user's account was created.
        /// </summary>
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// The list of users returned in the response.
    /// </summary>
    [JsonPropertyName("data")]
    public List<User> Data { get; set; }
}