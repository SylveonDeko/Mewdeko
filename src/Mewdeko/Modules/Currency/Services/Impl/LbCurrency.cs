namespace Mewdeko.Modules.Currency.Services.Impl
{
    /// <summary>
    /// Represents a user's balance in the currency system.
    /// </summary>
    public class LbCurrency
    {
        /// <summary>
        /// Gets or sets the balance amount.
        /// </summary>
        public long Balance { get; set; }

        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public ulong UserId { get; set; }
    }
}