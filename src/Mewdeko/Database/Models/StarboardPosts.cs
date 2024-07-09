namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a starboard post in a guild.
    /// </summary>
    public class StarboardPosts : DbEntity
    {
        /// <summary>
        /// Gets or sets the message ID.
        /// </summary>
        public ulong MessageId { get; set; }

        /// <summary>
        /// Gets or sets the post ID.
        /// </summary>
        public ulong PostId { get; set; }
    }
}