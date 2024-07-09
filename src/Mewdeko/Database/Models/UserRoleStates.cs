namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents the saved role states of a user in a guild.
    /// </summary>
    public class UserRoleStates : DbEntity
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
        /// Gets or sets the username.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the saved roles.
        /// </summary>
        public string SavedRoles { get; set; }
    }
}