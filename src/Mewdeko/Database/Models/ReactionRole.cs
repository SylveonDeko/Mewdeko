using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a message for reaction roles in a guild.
    /// </summary>
    public class ReactionRoleMessage : DbEntity, IIndexed
    {
        /// <summary>
        /// Gets or sets the guild configuration ID.
        /// </summary>
        [ForeignKey("GuildConfigId")]
        public int GuildConfigId { get; set; }

        /// <summary>
        /// Gets or sets the channel ID.
        /// </summary>
        public ulong ChannelId { get; set; }

        /// <summary>
        /// Gets or sets the message ID.
        /// </summary>
        public ulong MessageId { get; set; }

        /// <summary>
        /// Gets or sets the reaction roles.
        /// </summary>
        public List<ReactionRole> ReactionRoles { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the roles are exclusive.
        /// </summary>
        public bool Exclusive { get; set; }

        /// <summary>
        /// Gets or sets the index of the reaction role message.
        /// </summary>
        public int Index { get; set; }
    }

    /// <summary>
    /// Represents a reaction role in a guild.
    /// </summary>
    public class ReactionRole : DbEntity
    {
        /// <summary>
        /// Gets or sets the name of the emote.
        /// </summary>
        public string? EmoteName { get; set; }

        /// <summary>
        /// Gets or sets the role ID.
        /// </summary>
        public ulong RoleId { get; set; }

        /// <summary>
        /// Gets or sets the reaction role message ID.
        /// </summary>
        [ForeignKey("ReactionRoleMessageId")]
        public int ReactionRoleMessageId { get; set; }
    }
}