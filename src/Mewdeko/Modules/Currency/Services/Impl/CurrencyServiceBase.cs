using System.Linq.Expressions;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Database.Enums;

namespace Mewdeko.Modules.Currency.Services.Impl;

/// <summary>
///     Shared implementation of the currency balance operations, written once against whichever balance
///     table the bot is configured to use.
/// </summary>
/// <remarks>
///     Every mutation here compiles to a single conditional <c>UPDATE</c>, so concurrent commands from
///     the same user serialize at the row rather than racing in application memory. The previous
///     implementation loaded the row, adjusted it in C# and saved it back, which lost updates under
///     concurrency and let a user wager the same funds in several channels simultaneously.
/// </remarks>
/// <typeparam name="TBalance">The balance row type for the configured currency scope.</typeparam>
public abstract class CurrencyServiceBase<TBalance> : ICurrencyService
    where TBalance : class, IUserBalanceEntity, new()
{
    /// <summary>
    ///     The database connection factory.
    /// </summary>
    protected readonly IDataConnectionFactory DbFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CurrencyServiceBase{TBalance}" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    protected CurrencyServiceBase(IDataConnectionFactory dbFactory)
    {
        DbFactory = dbFactory;
    }

    /// <inheritdoc />
    public async Task AddUserBalanceAsync(ulong userId, long amount, ulong? guildId = null)
    {
        if (amount == 0)
            return;

        await using var db = await DbFactory.CreateConnectionAsync();

        var updated = await db.GetTable<TBalance>()
            .Where(UserKey(userId, guildId))
            .Set(x => x.Balance, x => x.Balance + amount < 0 ? 0 : x.Balance + amount)
            .UpdateAsync();

        if (updated != 0)
            return;

        await InsertBalanceRowAsync(db, userId, guildId, Math.Max(0, amount));
    }

    /// <inheritdoc />
    public async Task<bool> TryDebitAsync(ulong userId, long amount, ulong? guildId = null)
    {
        if (amount <= 0)
            return false;

        await using var db = await DbFactory.CreateConnectionAsync();

        return await db.GetTable<TBalance>()
            .Where(UserKey(userId, guildId))
            .Where(x => x.Balance >= amount)
            .Set(x => x.Balance, x => x.Balance - amount)
            .UpdateAsync() > 0;
    }

    /// <inheritdoc />
    public async Task<bool> TryDebitAsync(ulong userId, long amount, string description, CurrencyCategory category,
        ulong? guildId = null, string? source = null)
    {
        if (!await TryDebitAsync(userId, amount, guildId))
            return false;

        await AddTransactionAsync(userId, -amount, description, guildId, category, source);
        return true;
    }

    /// <inheritdoc />
    public async Task CreditAsync(ulong userId, long amount, string description, CurrencyCategory category,
        ulong? guildId = null, string? source = null)
    {
        await AddUserBalanceAsync(userId, amount, guildId);
        await AddTransactionAsync(userId, amount, description, guildId, category, source);
    }

    /// <inheritdoc />
    public async Task<long> GetUserBalanceAsync(ulong userId, ulong? guildId = null)
    {
        await using var db = await DbFactory.CreateConnectionAsync();

        return await db.GetTable<TBalance>()
            .Where(UserKey(userId, guildId))
            .Select(x => x.Balance)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<(long Wallet, long Bank)> GetBalancesAsync(ulong userId, ulong? guildId = null)
    {
        await using var db = await DbFactory.CreateConnectionAsync();

        var row = await db.GetTable<TBalance>()
            .Where(UserKey(userId, guildId))
            .Select(x => new
            {
                x.Balance, x.Bank
            })
            .FirstOrDefaultAsync();

        return row is null ? (0, 0) : (row.Balance, row.Bank);
    }

    /// <inheritdoc />
    public async Task<bool> TryDepositAsync(ulong userId, long amount, long capacity, ulong? guildId = null)
    {
        if (amount <= 0)
            return false;

        await using var db = await DbFactory.CreateConnectionAsync();

        return await db.GetTable<TBalance>()
            .Where(UserKey(userId, guildId))
            .Where(x => x.Balance >= amount)
            .Where(x => capacity <= 0 || x.Bank + amount <= capacity)
            .Set(x => x.Balance, x => x.Balance - amount)
            .Set(x => x.Bank, x => x.Bank + amount)
            .UpdateAsync() > 0;
    }

    /// <inheritdoc />
    public async Task<bool> TryWithdrawAsync(ulong userId, long amount, ulong? guildId = null)
    {
        if (amount <= 0)
            return false;

        await using var db = await DbFactory.CreateConnectionAsync();

        return await db.GetTable<TBalance>()
            .Where(UserKey(userId, guildId))
            .Where(x => x.Bank >= amount)
            .Set(x => x.Bank, x => x.Bank - amount)
            .Set(x => x.Balance, x => x.Balance + amount)
            .UpdateAsync() > 0;
    }

    /// <inheritdoc />
    public async Task<bool> TryTransferAsync(ulong fromUserId, ulong toUserId, long amount, long amountReceived,
        string description, CurrencyCategory category, ulong? guildId = null, string? source = null)
    {
        if (amount <= 0 || amountReceived < 0 || amountReceived > amount)
            return false;

        await using var db = await DbFactory.CreateConnectionAsync();
        await using var transaction = await db.BeginTransactionAsync();

        var debited = await db.GetTable<TBalance>()
            .Where(UserKey(fromUserId, guildId))
            .Where(x => x.Balance >= amount)
            .Set(x => x.Balance, x => x.Balance - amount)
            .UpdateAsync();

        if (debited == 0)
        {
            await transaction.RollbackAsync();
            return false;
        }

        if (amountReceived > 0)
        {
            var credited = await db.GetTable<TBalance>()
                .Where(UserKey(toUserId, guildId))
                .Set(x => x.Balance, x => x.Balance + amountReceived)
                .UpdateAsync();

            if (credited == 0)
                await InsertBalanceRowAsync(db, toUserId, guildId, amountReceived);
        }

        await db.InsertAsync(BuildTransaction(fromUserId, -amount, description, guildId, category, source));
        await db.InsertAsync(BuildTransaction(toUserId, amountReceived, description, guildId,
            CounterpartCategory(category), source));

        await transaction.CommitAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task AddTransactionAsync(ulong userId, long amount, string description, ulong? guildId = null,
        CurrencyCategory category = CurrencyCategory.Legacy, string? source = null)
    {
        await using var db = await DbFactory.CreateConnectionAsync();
        await db.InsertAsync(BuildTransaction(userId, amount, description, guildId, category, source));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TransactionHistory>> GetTransactionsAsync(ulong userId, ulong? guildId = null,
        int limit = 100)
    {
        await using var db = await DbFactory.CreateConnectionAsync();

        var scopedGuildId = TransactionGuildId(guildId);

        return await db.TransactionHistories
            .Where(x => x.UserId == userId && x.GuildId == scopedGuildId)
            .OrderByDescending(x => x.DateAdded)
            .Take(limit)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<long> GetNetGameLossAsync(ulong userId, DateTime since, ulong? guildId = null)
    {
        await using var db = await DbFactory.CreateConnectionAsync();

        var scopedGuildId = TransactionGuildId(guildId);
        var bet = nameof(CurrencyCategory.GameBet);
        var payout = nameof(CurrencyCategory.GamePayout);

        var net = await db.TransactionHistories
            .Where(x => x.UserId == userId
                        && x.GuildId == scopedGuildId
                        && x.DateAdded >= since
                        && (x.Category == bet || x.Category == payout))
            .SumAsync(x => (long?)x.Amount) ?? 0;

        return net < 0 ? -net : 0;
    }

    /// <inheritdoc />
    public ulong ResolveLedgerGuildId(ulong? guildId)
    {
        return TransactionGuildId(guildId);
    }

    /// <inheritdoc />
    public abstract Task<string> GetCurrencyEmote(ulong? guildId);

    /// <inheritdoc />
    public abstract Task<IEnumerable<LbCurrency>> GetAllUserBalancesAsync(ulong? guildId = null);

    /// <inheritdoc />
    public abstract Task SetReward(int amount, int seconds, ulong? guildId);

    /// <inheritdoc />
    public abstract Task<(int, int)> GetReward(ulong? guildId);

    /// <summary>
    ///     Builds the predicate identifying a single user's balance row in the configured scope.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="guildId">The ID of the guild, where the scope uses one.</param>
    /// <returns>A predicate matching exactly one balance row.</returns>
    protected abstract Expression<Func<TBalance, bool>> UserKey(ulong userId, ulong? guildId);

    /// <summary>
    ///     Creates a new balance row for a user who does not have one yet.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="guildId">The ID of the guild, where the scope uses one.</param>
    /// <param name="balance">The starting wallet balance.</param>
    /// <returns>The row to insert.</returns>
    protected abstract TBalance NewBalanceRow(ulong userId, ulong? guildId, long balance);

    /// <summary>
    ///     Resolves the guild ID stored on ledger entries. Global-scope balances record 0 so their
    ///     history stays separable from guild-scoped history.
    /// </summary>
    /// <param name="guildId">The ID of the guild, where the scope uses one.</param>
    /// <returns>The guild ID to store on ledger rows.</returns>
    protected abstract ulong TransactionGuildId(ulong? guildId);

    /// <summary>
    ///     Inserts a balance row, falling back to an increment if a concurrent caller created the row
    ///     first. The unique index on the balance tables is what makes the losing insert fail rather
    ///     than silently duplicate the user.
    /// </summary>
    private async Task InsertBalanceRowAsync(MewdekoDb db, ulong userId, ulong? guildId, long amount)
    {
        try
        {
            await db.InsertAsync(NewBalanceRow(userId, guildId, amount));
        }
        catch (Exception)
        {
            await db.GetTable<TBalance>()
                .Where(UserKey(userId, guildId))
                .Set(x => x.Balance, x => x.Balance + amount < 0 ? 0 : x.Balance + amount)
                .UpdateAsync();
        }
    }

    private TransactionHistory BuildTransaction(ulong userId, long amount, string description, ulong? guildId,
        CurrencyCategory category, string? source)
    {
        return new TransactionHistory
        {
            UserId = userId,
            GuildId = TransactionGuildId(guildId),
            Amount = amount,
            Description = description,
            Category = category.ToString(),
            Source = source,
            DateAdded = DateTime.UtcNow
        };
    }

    /// <summary>
    ///     Maps the sender's side of a transfer to the category recorded against the recipient, so a
    ///     single transfer does not read as two outflows in the analytics breakdown.
    /// </summary>
    private static CurrencyCategory CounterpartCategory(CurrencyCategory category)
    {
        return category switch
        {
            CurrencyCategory.PaySent => CurrencyCategory.PayReceived,
            _ => category
        };
    }
}