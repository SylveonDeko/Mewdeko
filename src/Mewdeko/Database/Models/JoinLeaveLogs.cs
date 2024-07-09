namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents the logs for user join and leave events in a guild.
    /// </summary>
    public class JoinLeaveLogs : DbEntity
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
        /// Gets or sets a value indicating whether the event is a join event.
        /// </summary>
        public bool IsJoin { get; set; }
    }
}