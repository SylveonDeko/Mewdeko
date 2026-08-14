using System.Linq.Expressions;
using DataModel;
using LinqToDB.Async;

namespace Mewdeko.Modules.Currency.Services.Impl;

/// <summary>
///     Implementation of the currency service for managing user balances and transactions within a specific guild.
/// </summary>
public class GuildCurrencyService : CurrencyServiceBase<GuildUserBalance>
{
    private readonly GuildSettingsService guildSettingsService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GuildCurrencyService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database service.</param>
    /// <param name="guildSettingsService">The guild settings service.</param>
    public GuildCurrencyService(IDataConnectionFactory dbFactory, GuildSettingsService guildSettingsService)
        : base(dbFactory)
    {
        this.guildSettingsService = guildSettingsService;
    }

    /// <inheritdoc />
    protected override Expression<Func<GuildUserBalance, bool>> UserKey(ulong userId, ulong? guildId)
    {
        var resolved = RequireGuildId(guildId);
        return x => x.UserId == userId && x.GuildId == resolved;
    }

    /// <inheritdoc />
    protected override GuildUserBalance NewBalanceRow(ulong userId, ulong? guildId, long balance)
    {
        return new GuildUserBalance
        {
            UserId = userId, GuildId = RequireGuildId(guildId), Balance = balance, DateAdded = DateTime.UtcNow
        };
    }

    /// <inheritdoc />
    protected override ulong TransactionGuildId(ulong? guildId)
    {
        return RequireGuildId(guildId);
    }

    /// <inheritdoc />
    public override async Task<string> GetCurrencyEmote(ulong? guildId)
    {
        var resolved = RequireGuildId(guildId);

        await using var db = await DbFactory.CreateConnectionAsync();

        return await db.GuildConfigs
            .Where(x => x.GuildId == resolved)
            .Select(x => x.CurrencyEmoji)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<LbCurrency>> GetAllUserBalancesAsync(ulong? guildId = null)
    {
        var resolved = RequireGuildId(guildId);

        await using var db = await DbFactory.CreateConnectionAsync();

        return await db.GuildUserBalances
            .Where(x => x.GuildId == resolved)
            .Select(x => new LbCurrency
            {
                UserId = x.UserId, Balance = x.Balance, Bank = x.Bank
            })
            .ToListAsync();
    }

    /// <inheritdoc />
    public override async Task SetReward(int amount, int seconds, ulong? guildId)
    {
        var resolved = RequireGuildId(guildId);

        var settings = await guildSettingsService.GetGuildConfig(resolved);
        settings.RewardAmount = amount;
        settings.RewardTimeoutSeconds = seconds;
        await guildSettingsService.UpdateGuildConfig(resolved, settings);
    }

    /// <inheritdoc />
    public override async Task<(int, int)> GetReward(ulong? guildId)
    {
        var settings = await guildSettingsService.GetGuildConfig(RequireGuildId(guildId));
        return (settings.RewardAmount, settings.RewardTimeoutSeconds);
    }

    private static ulong RequireGuildId(ulong? guildId)
    {
        return guildId ?? throw new ArgumentException("Guild ID must be provided.", nameof(guildId));
    }
}