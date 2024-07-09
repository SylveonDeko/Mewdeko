namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents settings that are only accessible by the owner.
    /// </summary>
    public class OwnerOnly : DbEntity
    {
        /// <summary>
        /// Gets or sets the owners.
        /// </summary>
        public string Owners { get; set; } = "";

        /// <summary>
        /// Gets or sets the number of GPT tokens used.
        /// </summary>
        public int GptTokensUsed { get; set; }

        /// <summary>
        /// Gets or sets the emoji used for currency.
        /// </summary>
        public string CurrencyEmote { get; set; } = "💰";

        /// <summary>
        /// Gets or sets the reward amount.
        /// </summary>
        public int RewardAmount { get; set; } = 200;

        /// <summary>
        /// Gets or sets the reward timeout in seconds.
        /// </summary>
        public int RewardTimeoutSeconds { get; set; } = 86400;
    }
}