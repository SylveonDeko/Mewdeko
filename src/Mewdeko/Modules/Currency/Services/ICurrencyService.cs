using DataModel;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Currency.Services.Impl;

namespace Mewdeko.Modules.Currency.Services;

/// <summary>
///     Service interface for managing user currency balances and transactions.
/// </summary>
/// <remarks>
///     Every method that moves currency resolves to a single conditional SQL statement. Callers must
///     never read a balance, decide, and then write it back: that pattern allowed the same funds to be
///     spent from several channels at once.
/// </remarks>
public interface ICurrencyService
{
    /// <summary>
    ///     Adds the specified amount to the wallet of the user, creating the balance row if needed.
    ///     Negative amounts are clamped so a wallet can never go below zero.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="amount">The amount to add to the balance.</param>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task AddUserBalanceAsync(ulong userId, long amount, ulong? guildId = null);

    /// <summary>
    ///     Atomically removes the specified amount from the user's wallet, but only if the wallet
    ///     currently holds at least that much. This is the only correct way to take a wager, a purchase
    ///     price or any other debit.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="amount">The amount to remove. Must be positive.</param>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <returns><see langword="true" /> if the funds were taken; otherwise <see langword="false" />.</returns>
    public Task<bool> TryDebitAsync(ulong userId, long amount, ulong? guildId = null);

    /// <summary>
    ///     Atomically debits the user and writes a categorized ledger entry in one call.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="amount">The amount to remove. Must be positive.</param>
    /// <param name="description">The description recorded on the ledger entry.</param>
    /// <param name="category">The ledger classification for analytics.</param>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <param name="source">
    ///     Stable machine-readable originator, such as a game key. Unlike the description this is never
    ///     localized, so analytics can group by it.
    /// </param>
    /// <returns><see langword="true" /> if the funds were taken; otherwise <see langword="false" />.</returns>
    public Task<bool> TryDebitAsync(ulong userId, long amount, string description, CurrencyCategory category,
        ulong? guildId = null, string? source = null);

    /// <summary>
    ///     Credits the user and writes a categorized ledger entry in one call.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="amount">The amount to add.</param>
    /// <param name="description">The description recorded on the ledger entry.</param>
    /// <param name="category">The ledger classification for analytics.</param>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <param name="source">Stable machine-readable originator, such as a game key.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task CreditAsync(ulong userId, long amount, string description, CurrencyCategory category,
        ulong? guildId = null, string? source = null);

    /// <summary>
    ///     Gets the wallet balance of the user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <returns>The balance of the user.</returns>
    public Task<long> GetUserBalanceAsync(ulong userId, ulong? guildId = null);

    /// <summary>
    ///     Gets the wallet and bank balances of the user in a single query.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <returns>The user's wallet and bank amounts.</returns>
    public Task<(long Wallet, long Bank)> GetBalancesAsync(ulong userId, ulong? guildId = null);

    /// <summary>
    ///     Atomically moves currency from the user's wallet into their bank.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="amount">The amount to deposit. Must be positive.</param>
    /// <param name="capacity">Maximum permitted bank balance, or 0 for unlimited.</param>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <returns>
    ///     <see langword="true" /> if the deposit happened; <see langword="false" /> if the wallet was
    ///     short or the deposit would exceed <paramref name="capacity" />.
    /// </returns>
    public Task<bool> TryDepositAsync(ulong userId, long amount, long capacity, ulong? guildId = null);

    /// <summary>
    ///     Atomically moves currency from the user's bank into their wallet.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="amount">The amount to withdraw. Must be positive.</param>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <returns><see langword="true" /> if the withdrawal happened; otherwise <see langword="false" />.</returns>
    public Task<bool> TryWithdrawAsync(ulong userId, long amount, ulong? guildId = null);

    /// <summary>
    ///     Atomically takes currency from one user's wallet and gives part or all of it to another.
    ///     The debit is conditional, so a sender can never move funds they no longer hold.
    /// </summary>
    /// <param name="fromUserId">The user losing currency.</param>
    /// <param name="toUserId">The user receiving currency.</param>
    /// <param name="amount">The amount taken from the sender. Must be positive.</param>
    /// <param name="amountReceived">
    ///     The amount actually credited to the recipient. Anything below <paramref name="amount" /> is
    ///     destroyed, which is how transfer tax removes currency from circulation.
    /// </param>
    /// <param name="description">The description recorded on both ledger entries.</param>
    /// <param name="category">The ledger classification for analytics.</param>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <param name="source">Stable machine-readable originator.</param>
    /// <returns><see langword="true" /> if the transfer happened; otherwise <see langword="false" />.</returns>
    public Task<bool> TryTransferAsync(ulong fromUserId, ulong toUserId, long amount, long amountReceived,
        string description, CurrencyCategory category, ulong? guildId = null, string? source = null);

    /// <summary>
    ///     Adds a transaction for the user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="amount">The transaction amount.</param>
    /// <param name="description">The description of the transaction.</param>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <param name="category">The ledger classification for analytics.</param>
    /// <param name="source">Stable machine-readable originator, such as a game key.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task AddTransactionAsync(ulong userId, long amount, string description, ulong? guildId = null,
        CurrencyCategory category = CurrencyCategory.Legacy, string? source = null);

    /// <summary>
    ///     Gets the most recent transactions for the user, newest first.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <param name="limit">Maximum number of entries to return.</param>
    /// <returns>The transaction history of the user.</returns>
    public Task<IReadOnlyList<TransactionHistory>> GetTransactionsAsync(ulong userId, ulong? guildId = null,
        int limit = 100);

    /// <summary>
    ///     Sums the amounts a user has lost to a given ledger category since a point in time. Used to
    ///     enforce the daily loss limit.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="since">The earliest transaction time to include.</param>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <returns>The net amount lost, as a positive number.</returns>
    public Task<long> GetNetGameLossAsync(ulong userId, DateTime since, ulong? guildId = null);

    /// <summary>
    ///     Resolves the guild ID that ledger rows are recorded under for the configured currency scope.
    ///     Global currency records everything under 0, so analytics needs this to scope its queries the
    ///     same way the write path does.
    /// </summary>
    /// <param name="guildId">The guild a command was used in.</param>
    /// <returns>The guild ID ledger rows are stored under.</returns>
    public ulong ResolveLedgerGuildId(ulong? guildId);

    /// <summary>
    ///     Gets the currency emote of the guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <returns>The currency emote of the guild.</returns>
    public Task<string> GetCurrencyEmote(ulong? guildId);

    /// <summary>
    ///     Gets the balances of all users in the guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <returns>The balances of all users in the guild.</returns>
    public Task<IEnumerable<LbCurrency>> GetAllUserBalancesAsync(ulong? guildId = null);

    /// <summary>
    ///     Sets the reward for currency gain.
    /// </summary>
    /// <param name="amount">The amount of currency to reward.</param>
    /// <param name="seconds">The cooldown duration for the reward.</param>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task SetReward(int amount, int seconds, ulong? guildId);

    /// <summary>
    ///     Gets the reward for currency gain.
    /// </summary>
    /// <param name="guildId">The ID of the guild (optional).</param>
    /// <returns>The reward amount and cooldown duration.</returns>
    public Task<(int, int)> GetReward(ulong? guildId);
}