using Mewdeko.Modules.Currency.Services.Impl;

namespace Mewdeko.Modules.Currency.Services;

public interface ICurrencyService
{
    Task AddUserBalanceAsync(ulong userId, long amount, ulong? guildId = null);
    Task<long> GetUserBalanceAsync(ulong userId, ulong? guildId = null);

    Task AddTransactionAsync(ulong userId, long amount, string description, ulong? guildId = null);
    Task<IEnumerable<TransactionHistory>?> GetTransactionsAsync(ulong userId, ulong? guildId = null);
    Task<string> GetCurrencyEmote(ulong? guildId);

    Task<IEnumerable<LbCurrency>> GetAllUserBalancesAsync(ulong? guildId = null);
    Task SetReward(int amount, int seconds, ulong? guildId);
    Task<(int, int)> GetReward(ulong? guildId);
}