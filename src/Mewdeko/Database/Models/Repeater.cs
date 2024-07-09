using System.ComponentModel.DataAnnotations.Schema;

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a message repeater in a guild.
    /// </summary>
    [Table("GuildRepeater")]
    public class Repeater : DbEntity
    {
        /// <summary>
        /// Gets or sets the guild configuration ID.
        /// </summary>
        [ForeignKey("GuildConfigId")]
        public int GuildConfigId { get; set; }

        /// <summary>
        /// Gets or sets the guild ID.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        /// Gets or sets the channel ID.
        /// </summary>
        public ulong ChannelId { get; set; }

        /// <summary>
        /// Gets or sets the last message ID.
        /// </summary>
        public ulong? LastMessageId { get; set; }

        /// <summary>
        /// Gets or sets the message to repeat.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the interval for repeating the message.
        /// </summary>
        public string Interval { get; set; }

        /// <summary>
        /// Gets or sets the start time of day for repeating the message.
        /// </summary>
        public string? StartTimeOfDay { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether redundant messages are allowed.
        /// </summary>
        public bool NoRedundant { get; set; } = false;
    }
}