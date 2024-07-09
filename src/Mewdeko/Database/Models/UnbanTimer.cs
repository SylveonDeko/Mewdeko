using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents an unban timer for a user in a guild.
    /// </summary>
    [Table("UnbanTimer")]
    public class UnbanTimer : DbEntity
    {
        /// <summary>
        /// Gets or sets the guild configuration ID.
        /// </summary>
        [ForeignKey("GuildConfigId")]
        public int GuildConfigId { get; set; }

        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        /// Gets or sets the unban date and time.
        /// </summary>
        public DateTime UnbanAt { get; set; }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode() => UserId.GetHashCode();

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj) =>
            obj is UnbanTimer ut && ut.UserId == UserId;
    }
}