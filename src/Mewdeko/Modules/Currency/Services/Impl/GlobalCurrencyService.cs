using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Currency.Services.Impl
{
    /// <summary>
    /// Implementation of the currency service for managing global user balances and transactions.
    /// </summary>
    public class GlobalCurrencyService : ICurrencyService
    {
        private readonly DbService service;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalCurrencyService"/> class.
        /// </summary>
        /// <param name="service">The database service.</param>
        public GlobalCurrencyService(DbService service)
        {
            this.service = service;
        }

        /// <inheritdoc/>
        public async Task AddUserBalanceAsync(ulong userId, long amount, ulong? guildId = null)
        {
            await using var uow = service.GetDbContext();

            // Check if the user already has a balance entry
            var existingBalance = await uow.GlobalUserBalances
                .FirstOrDefaultAsync(g => g.UserId == userId);

            if (existingBalance != null)
            {
                // Update the existing balance
                existingBalance.Balance += amount;
                uow.GlobalUserBalances.Update(existingBalance);
            }
            else
            {
                // Create a new balance entry
                var globalBalance = new GlobalUserBalance
                {
                    UserId = userId, Balance = amount
                };
                uow.GlobalUserBalances.Add(globalBalance);
            }

            // Save changes to the database
            await uow.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<long> GetUserBalanceAsync(ulong userId, ulong? guildId = null)
        {
            await using var uow = service.GetDbContext();
            // Retrieve user balance from the database
            return await uow.GlobalUserBalances
                .Where(x => x.UserId == userId)
                .Select(x => x.Balance)
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc/>
        public async Task AddTransactionAsync(ulong userId, long amount, string description, ulong? guildId = null)
        {
            await using var uow = service.GetDbContext();

            // Create a new transaction entry
            var transaction = new TransactionHistory
            {
                UserId = userId, Amount = amount, Description = description
            };

            // Add transaction to the database
            uow.TransactionHistories.Add(transaction);
            await uow.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<TransactionHistory>?> GetTransactionsAsync(ulong userId, ulong? guildId = null)
        {
            await using var uow = service.GetDbContext();

            // Retrieve user transactions from the database
            return await uow.TransactionHistories
                .Where(x => x.UserId == userId && x.GuildId == 0)?
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<string> GetCurrencyEmote(ulong? guildId = null)
        {
            await using var uow = service.GetDbContext();

            // Retrieve currency emote from the database
            return await uow.OwnerOnly
                .Select(x => x.CurrencyEmote)
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<LbCurrency>> GetAllUserBalancesAsync(ulong? _)
        {
            await using var uow = service.GetDbContext();

            // Retrieve all user balances from the database
            return uow.GlobalUserBalances
                .Select(x => new LbCurrency
                {
                    UserId = x.UserId, Balance = x.Balance
                }).ToList();
        }

        /// <inheritdoc/>
        public async Task SetReward(int amount, int seconds, ulong? _)
        {
            await using var uow = service.GetDbContext();
            // Update reward configuration in the database
            var config = await uow.OwnerOnly.FirstOrDefaultAsync();
            config.RewardAmount = amount;
            config.RewardTimeoutSeconds = seconds;
            uow.OwnerOnly.Update(config);
            await uow.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<(int, int)> GetReward(ulong? _)
        {
            await using var uow = service.GetDbContext();
            // Retrieve reward configuration from the database
            var config = await uow.OwnerOnly.FirstOrDefaultAsync();
            return (config.RewardAmount, config.RewardTimeoutSeconds);
        }
    }
}