using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Currency.Services.Impl
{
    /// <summary>
    /// Implementation of the currency dbContext for managing global user balances and transactions.
    /// </summary>
    public class GlobalCurrencyService : ICurrencyService
    {
        private readonly MewdekoContext dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalCurrencydbContext"/> class.
        /// </summary>
        /// <param name="dbContext">The database dbContext.</param>
        public GlobalCurrencyService(MewdekoContext dbContext)
        {
            this.dbContext = dbContext;
        }

        /// <inheritdoc/>
        public async Task AddUserBalanceAsync(ulong userId, long amount, ulong? guildId = null)
        {


            // Check if the user already has a balance entry
            var existingBalance = await dbContext.GlobalUserBalances
                .FirstOrDefaultAsync(g => g.UserId == userId);

            if (existingBalance != null)
            {
                // Update the existing balance
                existingBalance.Balance += amount;
                dbContext.GlobalUserBalances.Update(existingBalance);
            }
            else
            {
                // Create a new balance entry
                var globalBalance = new GlobalUserBalance
                {
                    UserId = userId, Balance = amount
                };
                dbContext.GlobalUserBalances.Add(globalBalance);
            }

            // Save changes to the database
            await dbContext.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<long> GetUserBalanceAsync(ulong userId, ulong? guildId = null)
        {

            // Retrieve user balance from the database
            return await dbContext.GlobalUserBalances
                .Where(x => x.UserId == userId)
                .Select(x => x.Balance)
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc/>
        public async Task AddTransactionAsync(ulong userId, long amount, string description, ulong? guildId = null)
        {


            // Create a new transaction entry
            var transaction = new TransactionHistory
            {
                UserId = userId, Amount = amount, Description = description
            };

            // Add transaction to the database
            dbContext.TransactionHistories.Add(transaction);
            await dbContext.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<TransactionHistory>?> GetTransactionsAsync(ulong userId, ulong? guildId = null)
        {


            // Retrieve user transactions from the database
            return await dbContext.TransactionHistories
                .Where(x => x.UserId == userId && x.GuildId == 0)?
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<string> GetCurrencyEmote(ulong? guildId = null)
        {


            // Retrieve currency emote from the database
            return await dbContext.OwnerOnly
                .Select(x => x.CurrencyEmote)
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<LbCurrency>> GetAllUserBalancesAsync(ulong? _)
        {


            // Retrieve all user balances from the database
            return dbContext.GlobalUserBalances
                .Select(x => new LbCurrency
                {
                    UserId = x.UserId, Balance = x.Balance
                }).ToList();
        }

        /// <inheritdoc/>
        public async Task SetReward(int amount, int seconds, ulong? _)
        {

            // Update reward configuration in the database
            var config = await dbContext.OwnerOnly.FirstOrDefaultAsync();
            config.RewardAmount = amount;
            config.RewardTimeoutSeconds = seconds;
            dbContext.OwnerOnly.Update(config);
            await dbContext.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<(int, int)> GetReward(ulong? _)
        {

            // Retrieve reward configuration from the database
            var config = await dbContext.OwnerOnly.FirstOrDefaultAsync();
            return (config.RewardAmount, config.RewardTimeoutSeconds);
        }
    }
}