using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

/// <summary>
///     Represents a social link for a Trovo user.
/// </summary>
public class TrovoSocialLink
{
    /// <summary>
    ///     The type of social link.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    ///     The URL of the social link.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; }
}