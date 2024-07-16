namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents the server recovery store.
    /// </summary>
    public class ServerRecoveryStore : DbEntity
    {
        /// <summary>
        /// Gets or sets the guild ID.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        /// Gets or sets the recovery key.
        /// </summary>
        public string? RecoveryKey { get; set; }

        /// <summary>
        /// Gets or sets the two-factor key.
        /// </summary>
        public string? TwoFactorKey { get; set; }
    }
}