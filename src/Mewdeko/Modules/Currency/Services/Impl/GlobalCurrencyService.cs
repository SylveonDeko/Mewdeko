using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Currency.Services.Impl;

public class GlobalCurrencyService : ICurrencyService
{
    private readonly DbService dbService;

    public GlobalCurrencyService(DbService dbService)
    {
        this.dbService = dbService;
    }

    public async Task AddUserBalanceAsync(ulong userId, long amount, ulong? guildId = null)
    {
        await using var uow = dbService.GetDbContext();

        var existingBalance = await uow.GlobalUserBalances
            .FirstOrDefaultAsync(g => g.UserId == userId);

        if (existingBalance != null)
        {
            existingBalance.Balance += amount;
            uow.GlobalUserBalances.Update(existingBalance);
        }
        else
        {
            var globalBalance = new GlobalUserBalance
            {
                UserId = userId, Balance = amount
            };
            uow.GlobalUserBalances.Add(globalBalance);
        }

        await uow.SaveChangesAsync();
    }

    public async Task<long> GetUserBalanceAsync(ulong userId, ulong? guildId = null)
    {
        await using var uow = dbService.GetDbContext();
        return await uow.GlobalUserBalances
            .Where(x => x.UserId == userId)
            .Select(x => x.Balance)
            .FirstOrDefaultAsync();
    }

    public async Task AddTransactionAsync(ulong userId, long amount, string description, ulong? guildId = null)
    {
        await using var uow = dbService.GetDbContext();

        var transaction = new TransactionHistory
        {
            UserId = userId, Amount = amount, Description = description
        };

        uow.TransactionHistories.Add(transaction);
        await uow.SaveChangesAsync();
    }

    public async Task<IEnumerable<TransactionHistory>?> GetTransactionsAsync(ulong userId, ulong? guildId = null)
    {
        await using var uow = dbService.GetDbContext();

        return await uow.TransactionHistories
            .Where(x => x.UserId == userId && x.GuildId == 0)?
            .ToListAsync();
    }

    public async Task<string> GetCurrencyEmote(ulong? guildId = null)
    {
        await using var uow = dbService.GetDbContext();

        return await uow.OwnerOnly
            .Select(x => x.CurrencyEmote)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<LbCurrency>> GetAllUserBalancesAsync(ulong? guildId = null)
    {
        await using var uow = dbService.GetDbContext();

        return uow.GlobalUserBalances
            .Select(x => new LbCurrency
            {
                UserId = x.UserId, Balance = x.Balance
            }).ToList();
    }
}