using DataModel;
using LinqToDB;
using LinqToDB.Async;

namespace Mewdeko.Modules.Currency.Services;

/// <summary>
///     Loads and persists per-guild economy tuning, creating defaults on first access.
/// </summary>
/// <remarks>
///     Read on nearly every currency command, so resolved configs are cached in memory and invalidated
///     on write rather than hitting the database each time.
/// </remarks>
public class CurrencyConfigService : INService
{
    private readonly ConcurrentDictionary<ulong, CurrencyConfig> cache = new();
    private readonly IDataConnectionFactory dbFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CurrencyConfigService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    public CurrencyConfigService(IDataConnectionFactory dbFactory)
    {
        this.dbFactory = dbFactory;
    }

    /// <summary>
    ///     Gets the economy configuration for a guild, creating it with defaults if absent.
    /// </summary>
    /// <param name="guildId">The guild to load settings for.</param>
    /// <returns>The guild's economy configuration.</returns>
    public async Task<CurrencyConfig> GetConfigAsync(ulong guildId)
    {
        if (cache.TryGetValue(guildId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();

        var config = await db.CurrencyConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (config is null)
        {
            config = new CurrencyConfig
            {
                GuildId = guildId, DateAdded = DateTime.UtcNow
            };

            try
            {
                config.Id = await db.InsertWithInt32IdentityAsync(config);
            }
            catch (Exception)
            {
                config = await db.CurrencyConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId) ?? config;
            }
        }

        cache[guildId] = config;
        return config;
    }

    /// <summary>
    ///     Applies a change to a guild's economy configuration and persists it.
    /// </summary>
    /// <param name="guildId">The guild to update.</param>
    /// <param name="mutate">An action applying the desired changes to the configuration.</param>
    /// <returns>The updated configuration.</returns>
    public async Task<CurrencyConfig> UpdateAsync(ulong guildId, Action<CurrencyConfig> mutate)
    {
        var config = await GetConfigAsync(guildId);
        mutate(config);

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);

        cache[guildId] = config;
        return config;
    }

    /// <summary>
    ///     Restores a guild's economy configuration to its defaults.
    /// </summary>
    /// <param name="guildId">The guild to reset.</param>
    /// <returns>The freshly defaulted configuration.</returns>
    public async Task<CurrencyConfig> ResetAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.CurrencyConfigs.Where(x => x.GuildId == guildId).DeleteAsync();

        cache.TryRemove(guildId, out _);
        return await GetConfigAsync(guildId);
    }

    /// <summary>
    ///     Validates a wager against the guild's configured betting limits.
    /// </summary>
    /// <param name="config">The guild's economy configuration.</param>
    /// <param name="bet">The proposed wager.</param>
    /// <returns>
    ///     Why the wager is not acceptable, or <see cref="BetValidation.Ok" /> if it is.
    /// </returns>
    public static BetValidation ValidateBet(CurrencyConfig config, long bet)
    {
        if (!config.GamblingEnabled)
            return BetValidation.GamblingDisabled;
        if (bet < config.MinBet)
            return BetValidation.BelowMinimum;
        if (config.MaxBet > 0 && bet > config.MaxBet)
            return BetValidation.AboveMaximum;

        return BetValidation.Ok;
    }
}

/// <summary>
///     Why a proposed wager was or was not accepted.
/// </summary>
public enum BetValidation
{
    /// <summary>
    ///     The wager is within the guild's limits.
    /// </summary>
    Ok,

    /// <summary>
    ///     Wagering games are turned off in this guild.
    /// </summary>
    GamblingDisabled,

    /// <summary>
    ///     The wager is below the configured minimum.
    /// </summary>
    BelowMinimum,

    /// <summary>
    ///     The wager exceeds the configured maximum.
    /// </summary>
    AboveMaximum
}