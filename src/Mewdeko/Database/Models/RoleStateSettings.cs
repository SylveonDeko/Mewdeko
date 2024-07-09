namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents the settings for role state in a guild.
    /// </summary>
    public class RoleStateSettings : DbEntity
    {
        /// <summary>
        /// Gets or sets the guild ID.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether role state is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to clear roles on ban.
        /// </summary>
        public bool ClearOnBan { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to ignore bots.
        /// </summary>
        public bool IgnoreBots { get; set; } = true;

        /// <summary>
        /// Gets or sets the denied roles.
        /// </summary>
        public string DeniedRoles { get; set; } = "";

        /// <summary>
        /// Gets or sets the denied users.
        /// </summary>
        public string DeniedUsers { get; set; } = "";
    }
}