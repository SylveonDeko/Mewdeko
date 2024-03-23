using Mewdeko.Modules.Currency.Services.Impl;

namespace Mewdeko.Modules.Currency.Services
{
    /// <summary>
    /// Service interface for managing user currency balances and transactions.
    /// </summary>
    public interface ICurrencyService
    {
        /// <summary>
        /// Adds the specified amount to the balance of the user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="amount">The amount to add to the balance.</param>
        /// <param name="guildId">The ID of the guild (optional).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AddUserBalanceAsync(ulong userId, long amount, ulong? guildId = null);

        /// <summary>
        /// Gets the balance of the user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="guildId">The ID of the guild (optional).</param>
        /// <returns>The balance of the user.</returns>
        Task<long> GetUserBalanceAsync(ulong userId, ulong? guildId = null);

        /// <summary>
        /// Adds a transaction for the user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="amount">The transaction amount.</param>
        /// <param name="description">The description of the transaction.</param>
        /// <param name="guildId">The ID of the guild (optional).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AddTransactionAsync(ulong userId, long amount, string description, ulong? guildId = null);

        /// <summary>
        /// Gets the transaction history of the user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="guildId">The ID of the guild (optional).</param>
        /// <returns>The transaction history of the user.</returns>
        Task<IEnumerable<TransactionHistory>?> GetTransactionsAsync(ulong userId, ulong? guildId = null);

        /// <summary>
        /// Gets the currency emote of the guild.
        /// </summary>
        /// <param name="guildId">The ID of the guild (optional).</param>
        /// <returns>The currency emote of the guild.</returns>
        Task<string> GetCurrencyEmote(ulong? guildId);

        /// <summary>
        /// Gets the balances of all users in the guild.
        /// </summary>
        /// <param name="guildId">The ID of the guild (optional).</param>
        /// <returns>The balances of all users in the guild.</returns>
        Task<IEnumerable<LbCurrency>> GetAllUserBalancesAsync(ulong? guildId = null);

        /// <summary>
        /// Sets the reward for currency gain.
        /// </summary>
        /// <param name="amount">The amount of currency to reward.</param>
        /// <param name="seconds">The cooldown duration for the reward.</param>
        /// <param name="guildId">The ID of the guild (optional).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetReward(int amount, int seconds, ulong? guildId);

        /// <summary>
        /// Gets the reward for currency gain.
        /// </summary>
        /// <param name="guildId">The ID of the guild (optional).</param>
        /// <returns>The reward amount and cooldown duration.</returns>
        Task<(int, int)> GetReward(ulong? guildId);
    }
}