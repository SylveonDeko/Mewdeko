namespace Mewdeko.Modules.Currency.Services;

public interface ICurrencyService
{
    Task AddUserBalanceAsync(ulong userId, long amount, ulong? guildId = null);
    Task<long> GetUserBalanceAsync(ulong userId, ulong? guildId = null);

    Task AddTransactionAsync(ulong userId, int amount, string description, ulong? guildId = null);
    Task<IEnumerable<TransactionHistory>> GetTransactionsAsync(ulong userId, ulong? guildId = null);
}