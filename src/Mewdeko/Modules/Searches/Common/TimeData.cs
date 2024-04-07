namespace Mewdeko.Modules.Searches.Common
{
    /// <summary>
    /// Represents data related to time, including the address, time, and time zone name.
    /// </summary>
    public class TimeData
    {
        /// <summary>
        /// Gets or sets the address associated with the time data.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Gets or sets the date and time.
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// Gets or sets the name of the time zone.
        /// </summary>
        public string TimeZoneName { get; set; }
    }
}