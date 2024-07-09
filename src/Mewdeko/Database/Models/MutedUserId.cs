#nullable enable
using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a muted user ID.
    /// </summary>
    [Table("MutedUserId")]
    public class MutedUserId : DbEntity
    {
        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        /// Gets or sets the roles of the muted user.
        /// </summary>
        public string? roles { get; set; }

        /// <summary>
        /// Gets or sets the guild configuration ID.
        /// </summary>
        [ForeignKey("GuildConfigId")]
        public int GuildConfigId { get; set; }
    }
}