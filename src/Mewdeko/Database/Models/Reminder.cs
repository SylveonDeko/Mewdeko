namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a reminder in a guild.
    /// </summary>
    public class Reminder : DbEntity
    {
        /// <summary>
        /// Gets or sets the date and time for the reminder.
        /// </summary>
        public DateTime When { get; set; }

        /// <summary>
        /// Gets or sets the channel ID.
        /// </summary>
        public ulong ChannelId { get; set; }

        /// <summary>
        /// Gets or sets the server ID.
        /// </summary>
        public ulong ServerId { get; set; }

        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        /// Gets or sets the reminder message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the reminder is private.
        /// </summary>
        public bool IsPrivate { get; set; } = false;
    }
}