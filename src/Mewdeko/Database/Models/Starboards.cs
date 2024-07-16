namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a starboard in a guild.
    /// </summary>
    public class Starboards : DbEntity
    {
        /// <summary>
        /// Gets or sets the star emote.
        /// </summary>
        public string? Star { get; set; } = "⭐";

        /// <summary>
        /// Gets or sets the guild ID.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        /// Gets or sets the starboard channel ID.
        /// </summary>
        public ulong StarboardChannel { get; set; }

        /// <summary>
        /// Gets or sets the starboard threshold.
        /// </summary>
        public int StarboardThreshold { get; set; } = 3;

        /// <summary>
        /// Gets or sets the repost threshold.
        /// </summary>
        public int RepostThreshold { get; set; } = 5;

        /// <summary>
        /// Gets or sets a value indicating whether to allow bots on the starboard.
        /// </summary>
        public bool StarboardAllowBots { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to remove posts from the starboard when deleted.
        /// </summary>
        public bool StarboardRemoveOnDelete { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to remove posts from the starboard when reactions are cleared.
        /// </summary>
        public bool StarboardRemoveOnReactionsClear { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to remove posts from the starboard when below threshold.
        /// </summary>
        public bool StarboardRemoveOnBelowThreshold { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to use a starboard blacklist.
        /// </summary>
        public bool UseStarboardBlacklist { get; set; } = true;

        /// <summary>
        /// Gets or sets the channels to check for the starboard.
        /// </summary>
        public string? StarboardCheckChannels { get; set; } = "0";
    }
}
