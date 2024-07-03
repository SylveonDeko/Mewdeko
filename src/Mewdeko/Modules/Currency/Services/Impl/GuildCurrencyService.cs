using Mewdeko.Database.DbContextStuff;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Currency.Services.Impl
{
    /// <summary>
    /// Implementation of the currency service for managing user balances and transactions within a specific guild.
    /// </summary>
    public class GuildCurrencyService : ICurrencyService
    {
        private readonly DbContextProvider dbProvider;
        private readonly GuildSettingsService guildSettingsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="GuildCurrencyService"/> class.
        /// </summary>
        /// <param name="dbContext">The database service.</param>
        /// <param name="guildSettingsService">The guild settings service.</param>
        public GuildCurrencyService(DbContextProvider dbProvider, GuildSettingsService guildSettingsService)
        {
            this.dbProvider = dbProvider;
            this.guildSettingsService = guildSettingsService;
        }

        /// <inheritdoc/>
        public async Task AddUserBalanceAsync(ulong userId, long amount, ulong? guildId)
        {

            if (!guildId.HasValue) throw new ArgumentException("Guild ID must be provided.");
            await using var dbContext = await dbProvider.GetContextAsync();

            // Check if the user already has a balance entry in the guild
            var existingBalance = await dbContext.GuildUserBalances
                .FirstOrDefaultAsync(g => g.UserId == userId && g.GuildId == guildId.Value);

            if (existingBalance != null)
            {
                // Update the existing balance
                existingBalance.Balance += amount;
                dbContext.GuildUserBalances.Update(existingBalance);
            }
            else
            {
                // Create a new balance entry for the user in the guild
                var guildBalance = new GuildUserBalance
                {
                    UserId = userId, GuildId = guildId.Value, Balance = amount
                };
                dbContext.GuildUserBalances.Add(guildBalance);
            }

            // Save changes to the database
            await dbContext.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<long> GetUserBalanceAsync(ulong userId, ulong? guildId)
        {
            if (!guildId.HasValue) throw new ArgumentException("Guild ID must be provided.");

            await using var dbContext = await dbProvider.GetContextAsync();

            return await dbContext.GuildUserBalances
                .Where(x => x.UserId == userId && x.GuildId == guildId.Value)
                .Select(x => x.Balance)
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc/>
        public async Task AddTransactionAsync(ulong userId, long amount, string description, ulong? guildId)
        {
            if (!guildId.HasValue) throw new ArgumentException("Guild ID must be provided.");


            var transaction = new TransactionHistory
            {
                UserId = userId, GuildId = guildId.Value, Amount = amount, Description = description
            };
            await using var dbContext = await dbProvider.GetContextAsync();

            dbContext.TransactionHistories.Add(transaction);
            await dbContext.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<TransactionHistory>?> GetTransactionsAsync(ulong userId, ulong? guildId)
        {

            if (!guildId.HasValue) throw new ArgumentException("Guild ID must be provided.");
            await using var dbContext = await dbProvider.GetContextAsync();

            return await dbContext.TransactionHistories
                .Where(x => x.UserId == userId && x.GuildId == guildId.Value)?
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<string> GetCurrencyEmote(ulong? guildId)
        {
            if (!guildId.HasValue) throw new ArgumentException("Guild ID must be provided.");

            await using var dbContext = await dbProvider.GetContextAsync();

            return await dbContext.GuildConfigs
                .Where(x => x.GuildId == guildId.Value)
                .Select(x => x.CurrencyEmoji)
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<LbCurrency>> GetAllUserBalancesAsync(ulong? guildId)
        {
            if (!guildId.HasValue) throw new ArgumentException("Guild ID must be provided.");
            await using var dbContext = await dbProvider.GetContextAsync();


            var balances = dbContext.GuildUserBalances
                .Where(x => x.GuildId == guildId.Value)
                .Select(x => new LbCurrency
                {
                    UserId = x.UserId, Balance = x.Balance
                }).ToHashSet();

            return balances;
        }

        /// <inheritdoc/>
        public async Task SetReward(int amount, int seconds, ulong? guildId)
        {
            if (!guildId.HasValue) throw new ArgumentException("Guild ID must be provided.");
            var settings = await guildSettingsService.GetGuildConfig(guildId.Value);
            settings.RewardAmount = amount;
            settings.RewardTimeoutSeconds = seconds;
            await guildSettingsService.UpdateGuildConfig(guildId.Value, settings);
        }

        /// <inheritdoc/>
        public async Task<(int, int)> GetReward(ulong? guildId)
        {
            if (!guildId.HasValue) throw new ArgumentException("Guild ID must be provided.");
            var settings = await guildSettingsService.GetGuildConfig(guildId.Value);
            return (settings.RewardAmount, settings.RewardTimeoutSeconds);
        }
    }
}