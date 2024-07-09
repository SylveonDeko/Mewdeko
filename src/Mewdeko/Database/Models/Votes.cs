namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a vote in a guild.
    /// </summary>
    public class Votes : DbEntity
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
        /// Gets or sets the bot ID.
        /// </summary>
        public ulong BotId { get; set; }
    }
}