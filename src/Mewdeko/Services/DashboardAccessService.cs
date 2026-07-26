using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.Enums;

namespace Mewdeko.Services;

/// <summary>
///     In-memory view of a guild's restricted dashboard access configuration. Kept in
///     <see cref="DashboardAccessService.Cache" /> and refreshed on write so per-request
///     permission checks (the dashboard access enforcement filter runs on every proxied
///     request) never need to hit the database.
/// </summary>
public class DashboardAccessCache
{
    /// <summary>
    ///     Whether users with Administrator/ManageGuild permission may manage the access list
    ///     without being explicitly listed as a manager.
    /// </summary>
    public bool AdminsCanManageAccess { get; set; }

    /// <summary>
    ///     Users/roles explicitly allowed to manage the access list.
    /// </summary>
    public List<DashboardAccessManager> Managers { get; set; } = [];

    /// <summary>
    ///     Restricted access grants for the guild, each with its resolved per-section levels.
    /// </summary>
    public List<DashboardAccessGrantCache> Grants { get; set; } = [];
}

/// <summary>
///     A single dashboard access grant with its section levels flattened into a lookup for fast checks.
/// </summary>
public class DashboardAccessGrantCache
{
    /// <summary>
    ///     The grant's database ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Whether <see cref="TargetId" /> is a user or a role.
    /// </summary>
    public DashboardAccessTargetType TargetType { get; set; }

    /// <summary>
    ///     The Discord user or role ID this grant applies to.
    /// </summary>
    public ulong TargetId { get; set; }

    /// <summary>
    ///     Section name (matching a bot API controller name) to access level.
    /// </summary>
    public Dictionary<string, DashboardAccessLevel> Sections { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
///     Manages restricted dashboard access: which users/roles beyond the guild owner and
///     Administrator-permission holders may use the dashboard for a guild, which sections
///     they can see, and who is allowed to manage that list. Mirrors the caching approach
///     used by the command permission system (<c>PermissionService</c>) so the enforcement
///     filter, which runs on every proxied dashboard request, never needs a database round
///     trip on the hot path.
/// </summary>
public class DashboardAccessService(IDataConnectionFactory dbFactory) : INService, IReadyExecutor
{
    /// <summary>
    ///     Per-guild cache of dashboard access configuration.
    /// </summary>
    public ConcurrentDictionary<ulong, DashboardAccessCache> Cache { get; } = new();

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var settings = await db.DashboardAccessSettings.ToListAsync();
        var managers = await db.DashboardAccessManagers.ToListAsync();
        var grants = await db.DashboardAccesses.ToListAsync();
        var sections = await db.DashboardAccessSections.ToListAsync();

        var guildIds = settings.Select(s => s.GuildId)
            .Union(managers.Select(m => m.GuildId))
            .Union(grants.Select(g => g.GuildId))
            .Distinct();

        foreach (var guildId in guildIds)
        {
            Cache[guildId] = BuildCache(
                guildId,
                settings.FirstOrDefault(s => s.GuildId == guildId),
                managers.Where(m => m.GuildId == guildId).ToList(),
                grants.Where(g => g.GuildId == guildId).ToList(),
                sections);
        }
    }

    /// <summary>
    ///     Gets the cached dashboard access configuration for a guild, loading and caching it
    ///     from the database on first access.
    /// </summary>
    public async Task<DashboardAccessCache> GetCacheFor(ulong guildId)
    {
        if (Cache.TryGetValue(guildId, out var cached))
            return cached;

        return await RefreshCache(guildId);
    }

    /// <summary>
    ///     Reloads a single guild's dashboard access configuration from the database and
    ///     updates the cache. Call after any write.
    /// </summary>
    public async Task<DashboardAccessCache> RefreshCache(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var settings = await db.DashboardAccessSettings.FirstOrDefaultAsync(s => s.GuildId == guildId);
        var managers = await db.DashboardAccessManagers.Where(m => m.GuildId == guildId).ToListAsync();
        var grants = await db.DashboardAccesses.Where(g => g.GuildId == guildId).ToListAsync();
        var grantIds = grants.Select(g => g.Id).ToList();
        var sections = grantIds.Count == 0
            ? []
            : await db.DashboardAccessSections.Where(s => grantIds.Contains(s.DashboardAccessId)).ToListAsync();

        var cache = BuildCache(guildId, settings, managers, grants, sections);
        Cache[guildId] = cache;
        return cache;
    }

    private static DashboardAccessCache BuildCache(
        ulong guildId,
        DashboardAccessSettings? settings,
        List<DashboardAccessManager> managers,
        List<DashboardAccess> grants,
        List<DashboardAccessSection> allSections)
    {
        _ = guildId;
        return new DashboardAccessCache
        {
            AdminsCanManageAccess = settings?.AdminsCanManageAccess ?? false,
            Managers = managers,
            Grants = grants.Select(grant => new DashboardAccessGrantCache
            {
                Id = grant.Id,
                TargetType = (DashboardAccessTargetType)grant.TargetType,
                TargetId = grant.TargetId,
                Sections = allSections
                    .Where(s => s.DashboardAccessId == grant.Id)
                    .ToDictionary(s => s.Section, s => (DashboardAccessLevel)s.Level, StringComparer.OrdinalIgnoreCase)
            }).ToList()
        };
    }

    /// <summary>
    ///     Whether the given user (directly, or via one of their roles) is explicitly listed as
    ///     a dashboard access manager for the guild.
    /// </summary>
    public async Task<bool> IsExplicitManagerAsync(ulong guildId, ulong userId, IReadOnlyCollection<ulong> roleIds)
    {
        var cache = await GetCacheFor(guildId);
        return cache.Managers.Any(m =>
            m.TargetType == (int)DashboardAccessTargetType.User && m.TargetId == userId ||
            m.TargetType == (int)DashboardAccessTargetType.Role && roleIds.Contains(m.TargetId));
    }

    /// <summary>
    ///     Whether Administrator/ManageGuild permission holders are allowed to manage the access
    ///     list for the guild.
    /// </summary>
    public async Task<bool> AdminsCanManageAccessAsync(ulong guildId)
    {
        var cache = await GetCacheFor(guildId);
        return cache.AdminsCanManageAccess;
    }

    /// <summary>
    ///     Sets whether Administrator/ManageGuild permission holders may manage the access list.
    /// </summary>
    public async Task SetAdminsCanManageAccessAsync(ulong guildId, bool value)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = await db.DashboardAccessSettings.FirstOrDefaultAsync(s => s.GuildId == guildId);
        if (existing == null)
        {
            await db.InsertAsync(new DashboardAccessSettings
            {
                GuildId = guildId, AdminsCanManageAccess = value, DateAdded = DateTime.UtcNow
            });
        }
        else
        {
            existing.AdminsCanManageAccess = value;
            await db.UpdateAsync(existing);
        }

        await RefreshCache(guildId);
    }

    /// <summary>
    ///     Gets the explicit dashboard access managers for a guild.
    /// </summary>
    public async Task<List<DashboardAccessManager>> GetManagersAsync(ulong guildId)
    {
        var cache = await GetCacheFor(guildId);
        return cache.Managers;
    }

    /// <summary>
    ///     Adds a user or role as an explicit dashboard access manager.
    /// </summary>
    public async Task<DashboardAccessManager> AddManagerAsync(
        ulong guildId, DashboardAccessTargetType targetType, ulong targetId, ulong grantedBy)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = await db.DashboardAccessManagers.FirstOrDefaultAsync(m =>
            m.GuildId == guildId && m.TargetType == (int)targetType && m.TargetId == targetId);
        if (existing != null)
            return existing;

        var manager = new DashboardAccessManager
        {
            GuildId = guildId,
            TargetType = (int)targetType,
            TargetId = targetId,
            GrantedBy = grantedBy,
            DateAdded = DateTime.UtcNow
        };

        manager.Id = await db.InsertWithInt32IdentityAsync(manager);
        await RefreshCache(guildId);
        return manager;
    }

    /// <summary>
    ///     Removes a dashboard access manager entry.
    /// </summary>
    public async Task<bool> RemoveManagerAsync(ulong guildId, int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var removed = await db.DashboardAccessManagers
            .Where(m => m.Id == id && m.GuildId == guildId)
            .DeleteAsync();

        if (removed > 0)
            await RefreshCache(guildId);

        return removed > 0;
    }

    /// <summary>
    ///     Gets the restricted access grants for a guild, each with its section levels.
    /// </summary>
    public async Task<List<DashboardAccessGrantCache>> GetGrantsAsync(ulong guildId)
    {
        var cache = await GetCacheFor(guildId);
        return cache.Grants;
    }

    /// <summary>
    ///     Creates or updates the restricted access grant for a user/role, replacing its section
    ///     levels with the given set.
    /// </summary>
    public async Task<int> UpsertGrantAsync(
        ulong guildId,
        DashboardAccessTargetType targetType,
        ulong targetId,
        ulong grantedBy,
        IReadOnlyDictionary<string, DashboardAccessLevel> sections)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var existing = await db.DashboardAccesses.FirstOrDefaultAsync(g =>
            g.GuildId == guildId && g.TargetType == (int)targetType && g.TargetId == targetId);

        int grantId;
        if (existing == null)
        {
            var grant = new DashboardAccess
            {
                GuildId = guildId,
                TargetType = (int)targetType,
                TargetId = targetId,
                GrantedBy = grantedBy,
                DateAdded = DateTime.UtcNow
            };
            grantId = await db.InsertWithInt32IdentityAsync(grant);
        }
        else
        {
            grantId = existing.Id;
            await db.DashboardAccessSections.Where(s => s.DashboardAccessId == grantId).DeleteAsync();
        }

        foreach (var (section, level) in sections)
        {
            if (level == DashboardAccessLevel.None)
                continue;

            await db.InsertAsync(new DashboardAccessSection
            {
                DashboardAccessId = grantId, Section = section, Level = (int)level
            });
        }

        await RefreshCache(guildId);
        return grantId;
    }

    /// <summary>
    ///     Removes a restricted access grant and its section levels.
    /// </summary>
    public async Task<bool> RemoveGrantAsync(ulong guildId, int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var removed = await db.DashboardAccesses
            .Where(g => g.Id == id && g.GuildId == guildId)
            .DeleteAsync();

        if (removed > 0)
            await RefreshCache(guildId);

        return removed > 0;
    }

    /// <summary>
    ///     Resolves the highest access level the user (directly, or via any of their roles) has
    ///     been granted for a section in a guild.
    /// </summary>
    public async Task<DashboardAccessLevel> GetSectionAccessAsync(
        ulong guildId, ulong userId, IReadOnlyCollection<ulong> roleIds, string section)
    {
        var cache = await GetCacheFor(guildId);
        var best = DashboardAccessLevel.None;

        foreach (var grant in cache.Grants)
        {
            var matches = grant.TargetType == DashboardAccessTargetType.User && grant.TargetId == userId ||
                          grant.TargetType == DashboardAccessTargetType.Role && roleIds.Contains(grant.TargetId);

            if (!matches || !grant.Sections.TryGetValue(section, out var level))
                continue;

            if (level > best)
                best = level;
        }

        return best;
    }

    /// <summary>
    ///     Whether the user (directly, or via any of their roles) has been granted any access at
    ///     all in the guild, regardless of section. Used to decide whether a restricted user's
    ///     guild should appear in their dashboard guild switcher.
    /// </summary>
    public async Task<bool> HasAnyAccessAsync(ulong guildId, ulong userId, IReadOnlyCollection<ulong> roleIds)
    {
        var cache = await GetCacheFor(guildId);
        return HasAnyAccess(cache, userId, roleIds);
    }

    /// <summary>
    ///     Checks a preloaded guild cache without querying the database. This is used while building the
    ///     dashboard guild switcher so a user with many mutual guilds never causes one query per guild.
    ///     Guilds without a cache entry have no dashboard access configuration and therefore no restricted
    ///     access grants.
    /// </summary>
    public bool HasAnyCachedAccess(ulong guildId, ulong userId, IReadOnlyCollection<ulong> roleIds)
    {
        return Cache.TryGetValue(guildId, out var cache) && HasAnyAccess(cache, userId, roleIds);
    }

    private static bool HasAnyAccess(DashboardAccessCache cache, ulong userId, IReadOnlyCollection<ulong> roleIds)
    {
        return cache.Grants.Any(grant =>
            grant.Sections.Count > 0 &&
            (grant.TargetType == DashboardAccessTargetType.User && grant.TargetId == userId ||
             grant.TargetType == DashboardAccessTargetType.Role && roleIds.Contains(grant.TargetId)));
    }
}