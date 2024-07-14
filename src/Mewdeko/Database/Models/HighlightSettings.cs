namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents the settings for highlights in a guild.
    /// </summary>
    public class HighlightSettings : DbEntity
    {
        /// <summary>
        /// Gets or sets the guild ID.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        /// Gets or sets the ignored channels for highlights.
        /// </summary>
        public string? IgnoredChannels { get; set; }

        /// <summary>
        /// Gets or sets the ignored users for highlights.
        /// </summary>
        public string? IgnoredUsers { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether highlights are enabled.
        /// </summary>
        public bool HighlightsOn { get; set; } = false;
    }
}