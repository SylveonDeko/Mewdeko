using System.Text.Json.Serialization;

namespace Mewdeko.Modules.UserProfile.Common;

public class ZodiacResult
{
    [JsonPropertyName("date_range")]
    public string DateRange { get; set; }

    [JsonPropertyName("current_date")]
    public string CurrentDate { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("compatibility")]
    public string Compatibility { get; set; }

    [JsonPropertyName("mood")]
    public string Mood { get; set; }

    [JsonPropertyName("color")]
    public string Color { get; set; }

    [JsonPropertyName("lucky_number")]
    public string LuckyNumber { get; set; }

    [JsonPropertyName("lucky_time")]
    public string LuckyTime { get; set; }
}