using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

/// <summary>
///     Represents the request data for a Trovo API call.
/// </summary>
public class TrovoRequestData
{
    /// <summary>
    ///     The username of the channel to fetch data for.
    /// </summary>
    [JsonPropertyName("username")]
    public string Username { get; set; }
}