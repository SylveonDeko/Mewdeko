using System.Linq.Expressions;
using DataModel;
using LinqToDB;
using LinqToDB.Async;

namespace Mewdeko.Modules.Currency.Services.Impl;

/// <summary>
///     Implementation of the currency service for managing global user balances and transactions shared
///     across every guild the bot is in.
/// </summary>
public class GlobalCurrencyService : CurrencyServiceBase<GlobalUserBalance>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="GlobalCurrencyService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database service.</param>
    public GlobalCurrencyService(IDataConnectionFactory dbFactory) : base(dbFactory)
    {
    }

    /// <inheritdoc />
    protected override Expression<Func<GlobalUserBalance, bool>> UserKey(ulong userId, ulong? guildId)
    {
        return x => x.UserId == userId;
    }

    /// <inheritdoc />
    protected override GlobalUserBalance NewBalanceRow(ulong userId, ulong? guildId, long balance)
    {
        return new GlobalUserBalance
        {
            UserId = userId, Balance = balance, DateAdded = DateTime.UtcNow
        };
    }

    /// <inheritdoc />
    protected override ulong TransactionGuildId(ulong? guildId)
    {
        return 0;
    }

    /// <inheritdoc />
    public override async Task<string> GetCurrencyEmote(ulong? guildId = null)
    {
        await using var db = await DbFactory.CreateConnectionAsync();

        return await db.OwnerOnlies
            .Select(x => x.CurrencyEmote)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<LbCurrency>> GetAllUserBalancesAsync(ulong? guildId = null)
    {
        await using var db = await DbFactory.CreateConnectionAsync();

        return await db.GlobalUserBalances
            .Select(x => new LbCurrency
            {
                UserId = x.UserId, Balance = x.Balance, Bank = x.Bank
            })
            .ToListAsync();
    }

    /// <inheritdoc />
    public override async Task SetReward(int amount, int seconds, ulong? guildId = null)
    {
        await using var db = await DbFactory.CreateConnectionAsync();

        var config = await db.OwnerOnlies.FirstOrDefaultAsync();
        if (config is null)
            return;

        config.RewardAmount = amount;
        config.RewardTimeoutSeconds = seconds;
        await db.UpdateAsync(config);
    }

    /// <inheritdoc />
    public override async Task<(int, int)> GetReward(ulong? guildId = null)
    {
        await using var db = await DbFactory.CreateConnectionAsync();

        var config = await db.OwnerOnlies.FirstOrDefaultAsync();
        return config is null ? (0, 0) : (config.RewardAmount, config.RewardTimeoutSeconds);
    }
}