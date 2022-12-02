using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

public class TrovoRequestData
{
    [JsonPropertyName("username")]
    public string Username { get; set; }
}