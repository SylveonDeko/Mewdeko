using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Moderation.Common;
using Microsoft.Extensions.Caching.Memory;

namespace Mewdeko.Modules.Moderation.Services;

/// <summary>
///     Which part of a guild a ban purge setting applies to.
/// </summary>
public enum BanPruneScope
{
    /// <summary>
    ///     The guild-wide default, used when no override matches.
    /// </summary>
    Guild = 0,

    /// <summary>
    ///     An override covering every channel inside one category.
    /// </summary>
    Category = 1,

    /// <summary>
    ///     An override covering a single channel.
    /// </summary>
    Channel = 2
}

/// <summary>
///     Resolves how many days of messages a ban purges. Every action that bans can carry its own
///     purge, and each of those can be overridden per category or per channel. Settings are cached
///     per guild so a ban never waits on the database.
/// </summary>
public class BanPruneService : INService
{
    /// <summary>
    ///     The largest purge Discord accepts.
    /// </summary>
    public const int MaxPruneDays = 7;

    /// <summary>
    ///     The action key meaning "every action in this scope".
    /// </summary>
    public const string AnyAction = "";

    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(6);

    private readonly IMemoryCache cache;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<BanPruneService> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BanPruneService" /> class.
    /// </summary>
    /// <param name="dbFactory">Factory for creating database connections.</param>
    /// <param name="cache">Memory cache holding each guild's settings.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public BanPruneService(IDataConnectionFactory dbFactory, IMemoryCache cache, ILogger<BanPruneService> logger)
    {
        this.dbFactory = dbFactory;
        this.cache = cache;
        this.logger = logger;
    }

    /// <summary>
    ///     Gets the purge in days for one action, honouring channel and category overrides.
    /// </summary>
    /// <remarks>
    ///     The most specific setting wins: a channel beats a category, which beats the guild default.
    ///     Within one scope, a setting naming the action beats one covering every action.
    ///     When nothing is configured the action's own default is used.
    /// </remarks>
    /// <param name="guildId">The guild the ban happens in.</param>
    /// <param name="action">The moderation action issuing the ban.</param>
    /// <param name="channel">The channel the ban was issued from, if any.</param>
    /// <returns>A value between 0 and <see cref="MaxPruneDays" />.</returns>
    public async Task<int> GetPruneDaysAsync(ulong guildId, BanPruneAction action, IChannel? channel = null)
    {
        var settings = await GetSettingsAsync(guildId).ConfigureAwait(false);
        if (settings.Count == 0)
            return action.DefaultDays;

        foreach (var (scope, scopeId) in ResolutionOrder(channel))
        {
            if (settings.TryGetValue((scope, scopeId, action.Key), out var forAction))
                return forAction;

            if (settings.TryGetValue((scope, scopeId, AnyAction), out var forAny))
                return forAny;
        }

        return action.DefaultDays;
    }

    /// <summary>
    ///     Gets every setting configured in a guild.
    /// </summary>
    /// <param name="guildId">The guild to read.</param>
    /// <returns>The stored settings, most specific scope last.</returns>
    public async Task<IReadOnlyList<BanPruneSetting>> GetSettingListAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.BanPruneSettings
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.ScopeType)
            .ThenBy(x => x.ActionKey)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the purge for one scope and action, replacing any existing value.
    /// </summary>
    /// <param name="guildId">The guild to write to.</param>
    /// <param name="scope">The scope the value applies to.</param>
    /// <param name="scopeId">The category or channel id, ignored for <see cref="BanPruneScope.Guild" />.</param>
    /// <param name="action">The action to configure, or null to cover every action in the scope.</param>
    /// <param name="pruneDays">The purge in days, clamped to 0 through <see cref="MaxPruneDays" />.</param>
    public async Task SetAsync(
        ulong guildId,
        BanPruneScope scope,
        ulong scopeId,
        BanPruneAction? action,
        int pruneDays)
    {
        var days = Math.Clamp(pruneDays, 0, MaxPruneDays);
        var targetId = scope == BanPruneScope.Guild ? 0UL : scopeId;
        var scopeType = (int)scope;
        var actionKey = action?.Key ?? AnyAction;

        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = await db.BanPruneSettings
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.ScopeType == scopeType &&
                                      x.ScopeId == targetId && x.ActionKey == actionKey)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await db.InsertAsync(new BanPruneSetting
            {
                GuildId = guildId,
                ScopeType = scopeType,
                ScopeId = targetId,
                ActionKey = actionKey,
                PruneDays = days,
                DateAdded = DateTime.UtcNow
            }).ConfigureAwait(false);
        }
        else
        {
            existing.PruneDays = days;
            await db.UpdateAsync(existing).ConfigureAwait(false);
        }

        Invalidate(guildId);
    }

    /// <summary>
    ///     Removes the purge setting for one scope and action.
    /// </summary>
    /// <param name="guildId">The guild to write to.</param>
    /// <param name="scope">The scope to clear.</param>
    /// <param name="scopeId">The category or channel id, ignored for <see cref="BanPruneScope.Guild" />.</param>
    /// <param name="action">The action to clear, or null for the setting covering every action.</param>
    /// <returns>True when a setting existed and was removed.</returns>
    public async Task<bool> ClearAsync(ulong guildId, BanPruneScope scope, ulong scopeId, BanPruneAction? action)
    {
        var targetId = scope == BanPruneScope.Guild ? 0UL : scopeId;
        var scopeType = (int)scope;
        var actionKey = action?.Key ?? AnyAction;

        await using var db = await dbFactory.CreateConnectionAsync();

        var deleted = await db.BanPruneSettings
            .Where(x => x.GuildId == guildId && x.ScopeType == scopeType &&
                        x.ScopeId == targetId && x.ActionKey == actionKey)
            .DeleteAsync()
            .ConfigureAwait(false);

        Invalidate(guildId);
        return deleted > 0;
    }

    /// <summary>
    ///     Removes every setting attached to one scope, whichever actions they name.
    /// </summary>
    /// <param name="guildId">The guild to write to.</param>
    /// <param name="scope">The scope to clear.</param>
    /// <param name="scopeId">The category or channel id, ignored for <see cref="BanPruneScope.Guild" />.</param>
    /// <returns>The number of settings removed.</returns>
    public async Task<int> ClearScopeAsync(ulong guildId, BanPruneScope scope, ulong scopeId)
    {
        var targetId = scope == BanPruneScope.Guild ? 0UL : scopeId;
        var scopeType = (int)scope;

        await using var db = await dbFactory.CreateConnectionAsync();

        var deleted = await db.BanPruneSettings
            .Where(x => x.GuildId == guildId && x.ScopeType == scopeType && x.ScopeId == targetId)
            .DeleteAsync()
            .ConfigureAwait(false);

        Invalidate(guildId);
        return deleted;
    }

    /// <summary>
    ///     Drops every setting in a guild.
    /// </summary>
    /// <param name="guildId">The guild to reset.</param>
    /// <returns>The number of settings removed.</returns>
    public async Task<int> ResetAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var deleted = await db.BanPruneSettings
            .Where(x => x.GuildId == guildId)
            .DeleteAsync()
            .ConfigureAwait(false);

        Invalidate(guildId);
        return deleted;
    }

    private static IEnumerable<(BanPruneScope Scope, ulong ScopeId)> ResolutionOrder(IChannel? channel)
    {
        if (channel is not null)
        {
            yield return (BanPruneScope.Channel, channel.Id);

            if (channel is SocketThreadChannel { ParentChannel: not null } thread)
                yield return (BanPruneScope.Channel, thread.ParentChannel.Id);

            if (channel is INestedChannel { CategoryId: not null } nested)
                yield return (BanPruneScope.Category, nested.CategoryId.Value);
        }

        yield return (BanPruneScope.Guild, 0UL);
    }

    private void Invalidate(ulong guildId)
    {
        cache.Remove(CacheKey(guildId));
    }

    private static string CacheKey(ulong guildId)
    {
        return $"banprune_{guildId}";
    }

    private async Task<IReadOnlyDictionary<(BanPruneScope, ulong, string), int>> GetSettingsAsync(ulong guildId)
    {
        if (cache.TryGetValue(CacheKey(guildId),
                out IReadOnlyDictionary<(BanPruneScope, ulong, string), int>? cached) && cached is not null)
            return cached;

        IReadOnlyDictionary<(BanPruneScope, ulong, string), int> settings;
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var rows = await db.BanPruneSettings
                .Where(x => x.GuildId == guildId)
                .ToListAsync()
                .ConfigureAwait(false);

            settings = rows.ToDictionary(
                x => ((BanPruneScope)x.ScopeType, x.ScopeId, x.ActionKey ?? AnyAction),
                x => x.PruneDays);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load ban purge settings for guild {GuildId}", guildId);
            return new Dictionary<(BanPruneScope, ulong, string), int>();
        }

        cache.Set(CacheKey(guildId), settings, CacheExpiration);
        return settings;
    }
}