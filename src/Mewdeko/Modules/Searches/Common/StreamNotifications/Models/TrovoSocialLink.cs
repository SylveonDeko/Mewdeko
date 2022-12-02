using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

public class TrovoSocialLink
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}