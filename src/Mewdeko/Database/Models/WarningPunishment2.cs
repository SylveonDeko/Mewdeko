using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a warning punishment given by the second warning system in a guild.
    /// </summary>
    [Table("WarningPunishment2")]
    public class WarningPunishment2 : DbEntity
    {
        /// <summary>
        /// Gets or sets the guild configuration ID.
        /// </summary>
        [ForeignKey("GuildConfigId")]
        public int GuildConfigId { get; set; }

        /// <summary>
        /// Gets or sets the warning count.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Gets or sets the punishment action.
        /// </summary>
        public PunishmentAction Punishment { get; set; }

        /// <summary>
        /// Gets or sets the time for the punishment.
        /// </summary>
        public int Time { get; set; }

        /// <summary>
        /// Gets or sets the role ID for the punishment.
        /// </summary>
        public ulong? RoleId { get; set; }
    }
}