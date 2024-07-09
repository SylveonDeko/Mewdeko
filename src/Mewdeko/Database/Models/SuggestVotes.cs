namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a vote on a suggestion in a guild.
    /// </summary>
    public class SuggestVotes : DbEntity
    {
        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        /// Gets or sets the message ID.
        /// </summary>
        public ulong MessageId { get; set; }

        /// <summary>
        /// Gets or sets the emote picked by the user.
        /// </summary>
        public int EmotePicked { get; set; }
    }
}
