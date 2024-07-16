using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents the global balance of a user.
    /// </summary>
    [Table("GlobalUserBalance")]
    public class GlobalUserBalance : DbEntity
    {
        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        /// Gets or sets the balance.
        /// </summary>
        public long Balance { get; set; }
    }

    /// <summary>
    /// Represents the balance of a user in a guild.
    /// </summary>
    [Table("GuildUserBalance")]
    public class GuildUserBalance : DbEntity
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
        /// Gets or sets the balance.
        /// </summary>
        public long Balance { get; set; }
    }

    /// <summary>
    /// Represents the transaction history of a user in a guild.
    /// </summary>
    [Table("TransactionHistory")]
    public class TransactionHistory : DbEntity
    {
        /// <summary>
        /// Gets or sets the guild ID.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public ulong? UserId { get; set; } = 0;

        /// <summary>
        /// Gets or sets the transaction amount.
        /// </summary>
        public long Amount { get; set; }

        /// <summary>
        /// Gets or sets the transaction description.
        /// </summary>
        public string? Description { get; set; }
    }
}
