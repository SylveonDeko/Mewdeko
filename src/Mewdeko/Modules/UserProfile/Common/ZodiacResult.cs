using System.Text.Json.Serialization;

namespace Mewdeko.Modules.UserProfile.Common
{
    /// <summary>
    /// Represents the result of a Zodiac query.
    ///
    /// # WARNING: ZODIACS ARE FAKE
    /// </summary>
    public class ZodiacResult
    {
        /// <summary>
        /// Gets or sets the date range for the Zodiac sign.
        /// </summary>
        [JsonPropertyName("date_range")]
        public string DateRange { get; set; }

        /// <summary>
        /// Gets or sets the current date.
        /// </summary>
        [JsonPropertyName("current_date")]
        public string CurrentDate { get; set; }

        /// <summary>
        /// Gets or sets the description of the Zodiac sign.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the compatibility of the Zodiac sign.
        /// </summary>
        [JsonPropertyName("compatibility")]
        public string Compatibility { get; set; }

        /// <summary>
        /// Gets or sets the mood associated with the Zodiac sign.
        /// </summary>
        [JsonPropertyName("mood")]
        public string Mood { get; set; }

        /// <summary>
        /// Gets or sets the color associated with the Zodiac sign.
        /// </summary>
        [JsonPropertyName("color")]
        public string Color { get; set; }

        /// <summary>
        /// Gets or sets the lucky number associated with the Zodiac sign.
        /// </summary>
        [JsonPropertyName("lucky_number")]
        public string LuckyNumber { get; set; }

        /// <summary>
        /// Gets or sets the lucky time associated with the Zodiac sign.
        /// </summary>
        [JsonPropertyName("lucky_time")]
        public string LuckyTime { get; set; }
    }
}