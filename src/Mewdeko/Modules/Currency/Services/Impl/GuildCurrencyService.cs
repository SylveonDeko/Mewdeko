using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Currency.Services.Impl;

public class GuildCurrencyService : ICurrencyService
{
    private readonly DbService dbService;
    private readonly GuildSettingsService guildSettingsService;

    public GuildCurrencyService(DbService dbService, GuildSettingsService guildSettingsService)
    {
        this.dbService = dbService;
        this.guildSettingsService = guildSettingsService;
    }

    public async Task AddUserBalanceAsync(ulong userId, long amount, ulong? guildId)
    {
        await using var uow = dbService.GetDbContext();
        if (!guildId.HasValue) throw new ArgumentException("Guild ID must be provided.");

        var existingBalance = await uow.GuildUserBalances
            .FirstOrDefaultAsync(g => g.UserId == userId && g.GuildId == guildId.Value);

        if (existingBalance != null)
        {
            existingBalance.Balance += amount;
            uow.GuildUserBalances.Update(existingBalance);
        }
        else
        {
            var guildBalance = new GuildUserBalance
            {
                UserId = userId, GuildId = guildId.Value, Balance = amount
            };
            uow.GuildUserBalances.Add(guildBalance);
        }

        await uow.SaveChangesAsync();
    }


    public async Task<long> GetUserBalanceAsync(ulong userId, ulong? guildId)
    {
        if (!guildId.HasValue) throw new ArgumentException("Guild ID must be provided.");

        await using var uow = dbService.GetDbContext();
        return await uow.GuildUserBalances
            .Where(x => x.UserId == userId && x.GuildId == guildId.Value)
            .Select(x => x.Balance)
            .FirstOrDefaultAsync();
    }

    public async Task AddTransactionAsync(ulong userId, long amount, string description, ulong? guildId)
    {
        if (!guildId.HasValue) throw new ArgumentException("Guild ID must be provided.");
        await using var uow = dbService.GetDbContext();

        var transaction = new TransactionHistory
        {
            UserId = userId, GuildId = guildId.Value, Amount = amount, Description = description
        };

        uow.TransactionHistories.Add(transaction);
        await uow.SaveChangesAsync();
    }

    public async Task<IEnumerable<TransactionHistory>?> GetTransactionsAsync(ulong userId, ulong? guildId)
    {
        await using var uow = dbService.GetDbContext();
        if (!guildId.HasValue) throw new ArgumentException("Guild ID must be provided.");

        return await uow.TransactionHistories
            .Where(x => x.UserId == userId && x.GuildId == guildId.Value)?
            .ToListAsync();
    }

    public async Task<string> GetCurrencyEmote(ulong? guildId)
    {
        if (!guildId.HasValue) throw new ArgumentException("Guild ID must be provided.");
        await using var uow = dbService.GetDbContext();

        return await uow.GuildConfigs
            .Where(x => x.GuildId == guildId.Value)
            .Select(x => x.CurrencyEmoji)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<LbCurrency>> GetAllUserBalancesAsync(ulong? guildId)
    {
        if (!guildId.HasValue) throw new ArgumentException("Guild ID must be provided.");
        await using var uow = dbService.GetDbContext();

        var balances = uow.GuildUserBalances
            .Where(x => x.GuildId == guildId.Value)
            .Select(x => new LbCurrency
            {
                UserId = x.UserId, Balance = x.Balance
            }).ToHashSet();

        return balances;
    }

    public async Task SetReward(int amount, int seconds, ulong? guildId)
    {
        if (!guildId.HasValue) throw new ArgumentException("Guild ID must be provided.");
        var settings = await guildSettingsService.GetGuildConfig(guildId.Value);
        settings.RewardAmount = amount;
        settings.RewardTimeoutSeconds = seconds;
        await guildSettingsService.UpdateGuildConfig(guildId.Value, settings);
    }

    public async Task<(int, int)> GetReward(ulong? guildId)
    {
        if (!guildId.HasValue) throw new ArgumentException("Guild ID must be provided.");
        var settings = await guildSettingsService.GetGuildConfig(guildId.Value);
        return (settings.RewardAmount, settings.RewardTimeoutSeconds);
    }
}