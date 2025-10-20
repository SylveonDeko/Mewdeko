using DataModel;
using LinqToDB;
using LinqToDB.Async;

namespace Mewdeko.Modules.Currency.Services.Impl;

/// <summary>
///     Implementation of the currency dbContext for managing global user balances and transactions.
/// </summary>
public class GlobalCurrencyService : ICurrencyService
{
    private readonly IDataConnectionFactory dbFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GlobalCurrencyService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database dbContext.</param>
    public GlobalCurrencyService(IDataConnectionFactory dbFactory)
    {
        this.dbFactory = dbFactory;
    }

    /// <inheritdoc />
    public async Task AddUserBalanceAsync(ulong userId, long amount, ulong? guildId = null)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Check if the user already has a balance entry
        var existingBalance = await dbContext.GlobalUserBalances
            .FirstOrDefaultAsync(g => g.UserId == userId);

        if (existingBalance != null)
        {
            // Update the existing balance
            existingBalance.Balance += amount;
            await dbContext.UpdateAsync(existingBalance);
        }
        else
        {
            // Create a new balance entry
            var globalBalance = new GlobalUserBalance
            {
                UserId = userId, Balance = amount
            };
            await dbContext.InsertAsync(globalBalance);
        }
    }

    /// <inheritdoc />
    public async Task<long> GetUserBalanceAsync(ulong userId, ulong? guildId = null)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        // Retrieve user balance from the database
        return await dbContext.GlobalUserBalances
            .Where(x => x.UserId == userId)
            .Select(x => x.Balance)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task AddTransactionAsync(ulong userId, long amount, string description, ulong? guildId = null)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        // Create a new transaction entry
        var transaction = new TransactionHistory
        {
            UserId = userId, Amount = amount, Description = description, DateAdded = DateTime.UtcNow
        };

        // Add transaction to the database
        await dbContext.InsertAsync(transaction);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TransactionHistory>?> GetTransactionsAsync(ulong userId, ulong? guildId = null)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        // Retrieve user transactions from the database
        return await dbContext.TransactionHistories
            .Where(x => x.UserId == userId && x.GuildId == 0)?
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<string> GetCurrencyEmote(ulong? guildId = null)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        // Retrieve currency emote from the database
        return await dbContext.OwnerOnlies
            .Select(x => x.CurrencyEmote)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<LbCurrency>> GetAllUserBalancesAsync(ulong? _)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        // Retrieve all user balances from the database
        return dbContext.GlobalUserBalances
            .Select(x => new LbCurrency
            {
                UserId = x.UserId, Balance = x.Balance
            }).ToList();
    }

    /// <inheritdoc />
    public async Task SetReward(int amount, int seconds, ulong? _)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        // Update reward configuration in the database
        var config = await dbContext.OwnerOnlies.FirstOrDefaultAsync();
        config.RewardAmount = amount;
        config.RewardTimeoutSeconds = seconds;
        await dbContext.UpdateAsync(config);
    }

    /// <inheritdoc />
    public async Task<(int, int)> GetReward(ulong? _)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();
        // Retrieve reward configuration from the database
        var config = await dbContext.OwnerOnlies.FirstOrDefaultAsync();
        return (config.RewardAmount, config.RewardTimeoutSeconds);
    }
}