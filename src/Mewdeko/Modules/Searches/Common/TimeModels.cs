using Newtonsoft.Json;

namespace Mewdeko.Modules.Searches.Common
{
    /// <summary>
    /// Represents the result of a time zone query, including the time zone abbreviation and timestamp.
    /// </summary>
    public class TimeZoneResult
    {
        /// <summary>
        /// Gets or sets the time zone abbreviation.
        /// </summary>
        [JsonProperty("abbreviation")]
        public string TimezoneName { get; set; }

        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        [JsonProperty("timestamp")]
        public int Timestamp { get; set; }
    }

    /// <summary>
    /// Represents the response from the LocationIQ API, including latitude, longitude, and display name.
    /// </summary>
    public class LocationIqResponse
    {
        /// <summary>
        /// Gets or sets the latitude.
        /// </summary>
        public float Lat { get; set; }

        /// <summary>
        /// Gets or sets the longitude.
        /// </summary>
        public float Lon { get; set; }

        /// <summary>
        /// Gets or sets the display name associated with the location.
        /// </summary>
        [JsonProperty("display_name")]
        public string DisplayName { get; set; }
    }
}