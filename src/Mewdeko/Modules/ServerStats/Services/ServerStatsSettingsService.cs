using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.ServerStats.Common;

namespace Mewdeko.Modules.ServerStats.Services;

/// <summary>
///     Holds the per guild stats settings, exclusions and global privacy opt outs in memory so the message and voice
///     hot paths can decide whether to count an event without touching the database.
/// </summary>
public class ServerStatsSettingsService : INService, IReadyExecutor
{
    private readonly ConcurrentDictionary<ulong, HashSet<string>> activityFilters = new();
    private readonly IDataConnectionFactory dbFactory;
    private readonly ConcurrentDictionary<ulong, HashSet<ulong>> excludedChannels = new();
    private readonly ConcurrentDictionary<ulong, HashSet<ulong>> excludedRoles = new();
    private readonly ConcurrentDictionary<ulong, HashSet<ulong>> excludedUsers = new();
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), DateTime> lastCounted = new();
    private readonly ILogger<ServerStatsSettingsService> logger;
    private readonly ConcurrentDictionary<ulong, byte> optedOut = new();
    private readonly ConcurrentDictionary<ulong, ServerStatsSetting> settings = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ServerStatsSettingsService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public ServerStatsSettingsService(IDataConnectionFactory dbFactory, ILogger<ServerStatsSettingsService> logger)
    {
        this.dbFactory = dbFactory;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            foreach (var row in await db.ServerStatsSettings.ToListAsync())
                settings[row.GuildId] = row;

            foreach (var row in await db.ServerStatsExclusions.ToListAsync())
                Bucket((StatsExclusionKind)row.Kind).GetOrAdd(row.GuildId, _ => []).Add(row.TargetId);

            foreach (var userId in await db.StatsPrivacyOptOuts.Select(x => x.UserId).ToListAsync())
                optedOut[userId] = 0;

            foreach (var row in await db.ActivityFilters.ToListAsync())
            {
                activityFilters.GetOrAdd(row.GuildId, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase))
                    .Add(row.Name);
            }

            logger.LogInformation("Loaded server stats settings for {Guilds} guilds and {OptOuts} opt outs",
                settings.Count, optedOut.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load server stats settings");
        }
    }

    private ConcurrentDictionary<ulong, HashSet<ulong>> Bucket(StatsExclusionKind kind)
    {
        return kind switch
        {
            StatsExclusionKind.Channel => excludedChannels,
            StatsExclusionKind.Role => excludedRoles,
            _ => excludedUsers
        };
    }

    #region Hot path checks

    /// <summary>
    ///     Whether a user has opted out of stats everywhere.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>True when opted out.</returns>
    public bool IsOptedOut(ulong userId)
    {
        return optedOut.ContainsKey(userId);
    }

    /// <summary>
    ///     Whether a member's activity in a channel is excluded by guild settings.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel the activity happened in.</param>
    /// <param name="user">The member.</param>
    /// <returns>True when the activity must not be counted.</returns>
    public bool IsExcluded(ulong guildId, ulong channelId, IGuildUser user)
    {
        if (optedOut.ContainsKey(user.Id))
            return true;
        if (user.IsBot && !GetCachedSettings(guildId).CountBots)
            return true;
        if (excludedUsers.TryGetValue(guildId, out var users) && users.Contains(user.Id))
            return true;
        if (excludedChannels.TryGetValue(guildId, out var channels) && channels.Contains(channelId))
            return true;
        if (excludedRoles.TryGetValue(guildId, out var roles) && user.RoleIds.Any(roles.Contains))
            return true;
        return false;
    }

    /// <summary>
    ///     Whether a channel is excluded from stats.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>True when excluded.</returns>
    public bool IsChannelExcluded(ulong guildId, ulong channelId)
    {
        return excludedChannels.TryGetValue(guildId, out var channels) && channels.Contains(channelId);
    }

    /// <summary>
    ///     Gets the channel IDs excluded in a guild, for filtering queries.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The excluded channel IDs.</returns>
    public IReadOnlyCollection<ulong> GetExcludedChannels(ulong guildId)
    {
        return excludedChannels.TryGetValue(guildId, out var channels) ? channels.ToArray() : [];
    }

    /// <summary>
    ///     Gets the user IDs excluded in a guild plus every opted out user, for filtering queries.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The excluded user IDs.</returns>
    public IReadOnlyCollection<ulong> GetExcludedUsers(ulong guildId)
    {
        var set = new HashSet<ulong>(optedOut.Keys);
        if (excludedUsers.TryGetValue(guildId, out var users))
            set.UnionWith(users);
        return set;
    }

    /// <summary>
    ///     Records a counted message and reports whether the guild's cooldown allows counting it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The member.</param>
    /// <param name="now">The message time.</param>
    /// <returns>True when the message may be counted.</returns>
    public bool PassesCooldown(ulong guildId, ulong userId, DateTime now)
    {
        var cooldown = GetCachedSettings(guildId).MessageCooldownSeconds;
        if (cooldown <= 0)
            return true;

        var key = (guildId, userId);
        if (lastCounted.TryGetValue(key, out var last) && (now - last).TotalSeconds < cooldown)
            return false;

        lastCounted[key] = now;
        return true;
    }

    /// <summary>
    ///     Gets the cached settings for a guild without touching the database.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The settings, or defaults when none are stored.</returns>
    public ServerStatsSetting GetCachedSettings(ulong guildId)
    {
        return settings.TryGetValue(guildId, out var s)
            ? s
            : new ServerStatsSetting
            {
                GuildId = guildId
            };
    }

    #endregion

    #region Settings

    /// <summary>
    ///     Gets a guild's settings, creating defaults on first read.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The settings.</returns>
    public async Task<ServerStatsSetting> GetSettingsAsync(ulong guildId)
    {
        if (settings.TryGetValue(guildId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.ServerStatsSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (row == null)
        {
            row = new ServerStatsSetting
            {
                GuildId = guildId, DateAdded = DateTime.UtcNow
            };
            row.Id = await db.InsertWithInt32IdentityAsync(row);
        }

        settings[guildId] = row;
        return row;
    }

    /// <summary>
    ///     Updates a guild's settings.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="mutate">Applies the change.</param>
    /// <returns>The updated settings.</returns>
    public async Task<ServerStatsSetting> UpdateSettingsAsync(ulong guildId, Action<ServerStatsSetting> mutate)
    {
        var row = await GetSettingsAsync(guildId);
        mutate(row);
        row.DefaultLookbackDays = Math.Clamp(row.DefaultLookbackDays, 1, 90);
        row.MessageCooldownSeconds = Math.Clamp(row.MessageCooldownSeconds, 0, 300);

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(row);
        settings[guildId] = row;
        return row;
    }

    #endregion

    #region Exclusions

    /// <summary>
    ///     Adds an exclusion.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="targetId">The channel, role or user.</param>
    /// <param name="kind">What the target is.</param>
    /// <returns>True when added, false when it already existed.</returns>
    public async Task<bool> AddExclusionAsync(ulong guildId, ulong targetId, StatsExclusionKind kind)
    {
        var bucket = Bucket(kind).GetOrAdd(guildId, _ => []);
        lock (bucket)
        {
            if (!bucket.Add(targetId))
                return false;
        }

        await using var db = await dbFactory.CreateConnectionAsync();
        var exists = await db.ServerStatsExclusions.AnyAsync(x =>
            x.GuildId == guildId && x.TargetId == targetId && x.Kind == (int)kind);
        if (!exists)
        {
            await db.InsertAsync(new ServerStatsExclusion
            {
                GuildId = guildId, TargetId = targetId, Kind = (int)kind, DateAdded = DateTime.UtcNow
            });
        }

        return true;
    }

    /// <summary>
    ///     Removes an exclusion.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="targetId">The channel, role or user.</param>
    /// <param name="kind">What the target is.</param>
    /// <returns>True when removed.</returns>
    public async Task<bool> RemoveExclusionAsync(ulong guildId, ulong targetId, StatsExclusionKind kind)
    {
        if (Bucket(kind).TryGetValue(guildId, out var bucket))
        {
            lock (bucket)
            {
                bucket.Remove(targetId);
            }
        }

        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ServerStatsExclusions
            .Where(x => x.GuildId == guildId && x.TargetId == targetId && x.Kind == (int)kind)
            .DeleteAsync() > 0;
    }

    /// <summary>
    ///     Lists exclusions of a kind.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="kind">What to list.</param>
    /// <returns>The target IDs.</returns>
    public IReadOnlyCollection<ulong> GetExclusions(ulong guildId, StatsExclusionKind kind)
    {
        return Bucket(kind).TryGetValue(guildId, out var bucket) ? bucket.ToArray() : [];
    }

    #endregion

    #region Activity filters

    /// <summary>
    ///     Whether an activity name passes the guild's whitelist or blacklist.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The activity name.</param>
    /// <returns>True when the activity may be tracked.</returns>
    public bool IsActivityAllowed(ulong guildId, string name)
    {
        var mode = (ActivityFilterMode)GetCachedSettings(guildId).ActivityFilterMode;
        var listed = activityFilters.TryGetValue(guildId, out var names) && names.Contains(name);
        return mode == ActivityFilterMode.Whitelist ? listed : !listed;
    }

    /// <summary>
    ///     Toggles an activity name on the guild's filter list.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The activity name.</param>
    /// <returns>True when the name was added, false when it was removed.</returns>
    public async Task<bool> ToggleActivityFilterAsync(ulong guildId, string name)
    {
        name = name.Trim();
        var set = activityFilters.GetOrAdd(guildId, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        bool added;
        lock (set)
        {
            added = set.Add(name);
            if (!added)
                set.Remove(name);
        }

        await using var db = await dbFactory.CreateConnectionAsync();
        if (added)
        {
            var exists =
                await db.ActivityFilters.AnyAsync(x => x.GuildId == guildId && x.Name.ToLower() == name.ToLower());
            if (!exists)
            {
                await db.InsertAsync(new ActivityFilter
                {
                    GuildId = guildId, Name = name, DateAdded = DateTime.UtcNow
                });
            }
        }
        else
        {
            await db.ActivityFilters.Where(x => x.GuildId == guildId && x.Name.ToLower() == name.ToLower())
                .DeleteAsync();
        }

        return added;
    }

    /// <summary>
    ///     Lists the guild's activity filter names.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The names.</returns>
    public IReadOnlyCollection<string> GetActivityFilters(ulong guildId)
    {
        return activityFilters.TryGetValue(guildId, out var set) ? set.OrderBy(x => x).ToArray() : [];
    }

    #endregion

    #region Privacy

    /// <summary>
    ///     Opts a user out of stats everywhere and scrubs what has been recorded about them.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>True when the user was not already opted out.</returns>
    public async Task<bool> OptOutAsync(ulong userId)
    {
        if (!optedOut.TryAdd(userId, 0))
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.InsertAsync(new StatsPrivacyOptOut
        {
            UserId = userId, DateAdded = DateTime.UtcNow
        });

        await db.MessageTimestamps.Where(x => x.UserId == userId).DeleteAsync();
        await db.MessageCounts.Where(x => x.UserId == userId).DeleteAsync();
        await db.VoiceSegments.Where(x => x.UserId == userId).DeleteAsync();
        await db.VoiceTotals.Where(x => x.UserId == userId).DeleteAsync();
        await db.ActivitySegments.Where(x => x.UserId == userId).DeleteAsync();
        await db.ActivityTotals.Where(x => x.UserId == userId).DeleteAsync();

        logger.LogInformation("User {UserId} opted out of stats and was scrubbed", userId);
        return true;
    }

    /// <summary>
    ///     Opts a user back in to stats.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>True when the user was opted out.</returns>
    public async Task<bool> OptInAsync(ulong userId)
    {
        if (!optedOut.TryRemove(userId, out _))
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.StatsPrivacyOptOuts.Where(x => x.UserId == userId).DeleteAsync();
        return true;
    }

    #endregion
}