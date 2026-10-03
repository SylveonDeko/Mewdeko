using System.Threading;
using DataModel;
using Discord.Interactions;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.ServerStats.Services;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Modules.Xp.Services;
using Mewdeko.Services.Analytics;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Achievements.Services;

/// <summary>
///     Tracks what members do, unlocks achievements, hands out rewards, and announces unlocks. Also stores each
///     server's settings, its own achievements and categories, and its changes to the built in ones.
/// </summary>
public sealed partial class AchievementService : INService, IReadyExecutor, IUnloadableService
{
    /// <summary>
    ///     Most achievements a server can make.
    /// </summary>
    public const int MaxCustomAchievements = 200;

    /// <summary>
    ///     Most categories a server can make.
    /// </summary>
    public const int MaxCustomCategories = 25;

    /// <summary>
    ///     Longest achievement or category name.
    /// </summary>
    public const int NameLength = 80;

    /// <summary>
    ///     Longest description.
    /// </summary>
    public const int DescriptionLength = 200;

    /// <summary>
    ///     Longest keyword.
    /// </summary>
    public const int KeywordLength = 100;

    /// <summary>
    ///     Longest unlock message source.
    /// </summary>
    public const int MessageLength = 6000;

    /// <summary>
    ///     Most points one achievement can give.
    /// </summary>
    public const int MaxPoints = 10000;

    /// <summary>
    ///     Most XP or currency one achievement can give.
    /// </summary>
    public const long MaxReward = 1_000_000_000;

    /// <summary>
    ///     Analytics feature key.
    /// </summary>
    private const string FeatureKey = "achievements";

    /// <summary>
    ///     Name this service subscribes to events under.
    /// </summary>
    private const string EventModuleName = "AchievementService";

    /// <summary>
    ///     Number of member lock stripes.
    /// </summary>
    private const int MemberLockStripes = 64;

    /// <summary>
    ///     How long an unused member's unlocks stay cached.
    /// </summary>
    private static readonly TimeSpan UnlockCacheIdle = TimeSpan.FromMinutes(30);

    private readonly DiscordShardedClient client;
    private readonly IAnalyticsCollector collector;
    private readonly CommandHandler commandHandler;
    private readonly IServiceProvider services;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly GuildSettingsService guildSettings;
    private readonly InteractionService interactionService;
    private readonly InviteCountService inviteCounts;
    private readonly ILogger<AchievementService> logger;
    private readonly MessageCountService messageCounts;
    private readonly SemaphoreSlim[] memberLocks;
    private readonly ServerStatsSettingsService statsSettings;
    private readonly GeneratedBotStrings strings;
    private readonly XpService xpService;

    private readonly ConcurrentDictionary<ulong, AchievementGuildSettings> settingsCache = new();
    private readonly ConcurrentDictionary<ulong, AchievementGuildCatalog> catalogCache = new();
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), UnlockState> unlockCache = new();
    private readonly ConcurrentDictionary<ulong, AchievementUserSetting> userSettingsCache = new();
    private readonly SemaphoreSlim writeLock = new(1, 1);

    /// <summary>
    ///     Initializes a new instance of the <see cref="AchievementService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">Database connection factory.</param>
    /// <param name="strings">Localized bot strings.</param>
    /// <param name="eventHandler">Event handler for gateway events.</param>
    /// <param name="guildSettings">Guild settings, used for prefixes.</param>
    /// <param name="messageCounts">Message counting, a data source for message achievements.</param>
    /// <param name="statsSettings">Server stats settings, which own voice tracking.</param>
    /// <param name="inviteCounts">Invite tracking, a data source for invite achievements.</param>
    /// <param name="xpService">XP, for levels and XP rewards.</param>
    /// <param name="commandHandler">Text command handler, for command achievements.</param>
    /// <param name="interactionService">Slash command handler, for command achievements.</param>
    /// <param name="services">Service provider, for the currency service.</param>
    /// <param name="collector">Analytics collector.</param>
    /// <param name="logger">Logger instance.</param>
    public AchievementService(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        GeneratedBotStrings strings,
        EventHandler eventHandler,
        GuildSettingsService guildSettings,
        MessageCountService messageCounts,
        ServerStatsSettingsService statsSettings,
        InviteCountService inviteCounts,
        XpService xpService,
        CommandHandler commandHandler,
        InteractionService interactionService,
        IServiceProvider services,
        IAnalyticsCollector collector,
        ILogger<AchievementService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.strings = strings;
        this.eventHandler = eventHandler;
        this.guildSettings = guildSettings;
        this.messageCounts = messageCounts;
        this.statsSettings = statsSettings;
        this.inviteCounts = inviteCounts;
        this.xpService = xpService;
        this.commandHandler = commandHandler;
        this.interactionService = interactionService;
        this.services = services;
        this.collector = collector;
        this.logger = logger;

        memberLocks = new SemaphoreSlim[MemberLockStripes];
        for (var i = 0; i < MemberLockStripes; i++)
            memberLocks[i] = new SemaphoreSlim(1, 1);

        SubscribeEvents();
    }

    /// <summary>
    ///     Loads every server's settings and starts the evaluation and sweep timers.
    /// </summary>
    /// <returns>A task that completes when settings are loaded.</returns>
    public async Task OnReadyAsync()
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var rows = await db.AchievementSettings.ToListAsync();
            foreach (var row in rows)
                settingsCache[row.GuildId] = Parse(row);

            logger.LogInformation("Loaded achievement settings for {Count} servers, {Enabled} enabled",
                rows.Count, rows.Count(r => r.Enabled));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load achievement settings");
        }

        SeedVoiceSessions();
        StartTimers();
    }

    /// <summary>
    ///     Unsubscribes from events and stops the timers.
    /// </summary>
    /// <returns>A completed task.</returns>
    public Task Unload()
    {
        UnsubscribeEvents();
        StopTimers();
        return Task.CompletedTask;
    }

    #region Settings

    /// <summary>
    ///     Gets a guild's command prefix.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <returns>The prefix.</returns>
    public Task<string> GetPrefixAsync(IGuild guild)
    {
        return guildSettings.GetPrefix(guild);
    }

    /// <summary>
    ///     Gets a server's settings, or defaults when it has none.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The settings.</returns>
    public AchievementGuildSettings GetSettings(ulong guildId)
    {
        return settingsCache.TryGetValue(guildId, out var settings)
            ? settings
            : Parse(new AchievementSetting
            {
                GuildId = guildId
            });
    }

    /// <summary>
    ///     Whether members can earn achievements in a server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>True when enabled.</returns>
    public bool IsEnabled(ulong guildId)
    {
        return settingsCache.TryGetValue(guildId, out var settings) && settings.Enabled;
    }

    /// <summary>
    ///     Changes a server's settings, creating the row when needed.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="change">Applies the change to the row.</param>
    /// <returns>The updated settings.</returns>
    public async Task<AchievementGuildSettings> UpdateSettingsAsync(ulong guildId, Action<AchievementSetting> change)
    {
        await writeLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var row = await db.AchievementSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
            var wasEnabled = row?.Enabled ?? false;
            if (row is null)
            {
                row = new AchievementSetting
                {
                    GuildId = guildId,
                    DateAdded = DateTime.UtcNow
                };
                change(row);
                row.Id = await db.InsertWithInt32IdentityAsync(row);
            }
            else
            {
                change(row);
                await db.UpdateAsync(row);
            }

            var parsed = Parse(row);
            settingsCache[guildId] = parsed;
            catalogCache.TryRemove(guildId, out _);

            if (row.Enabled && !wasEnabled)
            {
                collector.Feature(FeatureKey, guildId);
                QueueSweep(guildId);
            }

            return parsed;
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    ///     Turns achievements on or off, switching on message counting when turning on.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="enabled">The new state.</param>
    /// <returns>The updated settings.</returns>
    public async Task<AchievementGuildSettings> SetEnabledAsync(ulong guildId, bool enabled)
    {
        if (enabled && !messageCounts.IsCounting(guildId))
        {
            try
            {
                await messageCounts.ToggleGuildMessageCount(guildId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not turn on message counting for achievements in {GuildId}", guildId);
            }
        }

        return await UpdateSettingsAsync(guildId, row => row.Enabled = enabled);
    }

    /// <summary>
    ///     Which features supply data to achievements in a server, and whether each is on.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>Source key to whether it is on.</returns>
    public async Task<Dictionary<string, bool>> GetDataSourcesAsync(ulong guildId)
    {
        var inviteSettings = await inviteCounts.GetInviteCountSettingsAsync(guildId);
        var guildConfig = await guildSettings.GetGuildConfig(guildId);
        var xpSettings = await xpService.GetGuildXpSettingsAsync(guildId);
        await using var db = await dbFactory.CreateConnectionAsync();
        var repConfig = await db.RepConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

        return new Dictionary<string, bool>
        {
            ["messages"] = messageCounts.IsCounting(guildId),
            ["voice"] = statsSettings.GetCachedSettings(guildId).TrackVoice,
            ["invites"] = inviteSettings.IsEnabled,
            ["commands"] = !guildConfig.StatsOptOut,
            ["xp"] = !xpSettings.XpGainDisabled,
            ["reputation"] = repConfig?.Enabled ?? true,
            ["currency"] = true,
            ["discord"] = true,
            ["achievements"] = true
        };
    }

    /// <summary>
    ///     Turns on message counting so message achievements can track.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>True when counting is on afterwards.</returns>
    public async Task<bool> EnableMessageCountingAsync(ulong guildId)
    {
        if (messageCounts.IsCounting(guildId))
            return true;
        return await messageCounts.ToggleGuildMessageCount(guildId);
    }

    private static AchievementGuildSettings Parse(AchievementSetting row)
    {
        return new AchievementGuildSettings
        {
            Row = row,
            DisabledCategories = SplitKeys(row.DisabledCategories).ToHashSet(StringComparer.Ordinal),
            CategoryOrder = SplitKeys(row.CategoryOrder).ToList(),
            ExcludedRoles = SplitIds(row.ExcludedRoleIds),
            ExcludedChannels = SplitIds(row.ExcludedChannelIds),
            CardAssignments = AchievementCardAssignments.Parse(row.CardAssignments)
        };
    }

    /// <summary>
    ///     Splits a comma separated key list.
    /// </summary>
    /// <param name="value">The stored list.</param>
    /// <returns>The keys.</returns>
    public static IEnumerable<string> SplitKeys(string? value)
    {
        return (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    ///     Splits a comma separated ID list.
    /// </summary>
    /// <param name="value">The stored list.</param>
    /// <returns>The IDs.</returns>
    public static HashSet<ulong> SplitIds(string? value)
    {
        return SplitKeys(value)
            .Select(x => ulong.TryParse(x, out var id) ? id : 0)
            .Where(x => x != 0)
            .ToHashSet();
    }

    /// <summary>
    ///     Joins keys or IDs into a stored list.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items.</param>
    /// <returns>The stored list.</returns>
    public static string JoinKeys<T>(IEnumerable<T> items)
    {
        return string.Join(',', items.Select(x => x?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
    }

    #endregion

    #region Catalog

    /// <summary>
    ///     Everything a server can earn, built from the catalog, its overrides, and its own achievements.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The resolved catalog.</returns>
    public async Task<AchievementGuildCatalog> GetCatalogAsync(ulong guildId)
    {
        if (catalogCache.TryGetValue(guildId, out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var overrides = await db.AchievementOverrides.Where(x => x.GuildId == guildId).ToListAsync();
        var customs = await db.CustomAchievements.Where(x => x.GuildId == guildId).ToListAsync();
        var categories = await db.AchievementCategories.Where(x => x.GuildId == guildId).ToListAsync();
        var uploads = await db.AchievementIconUploads
            .Where(x => x.GuildId == guildId)
            .Select(x => new { x.Id, x.PublicUrl, x.Kind })
            .ToListAsync();
        var uploadUrls = uploads.ToDictionary(x => x.Id, x => x.PublicUrl ?? IconUploadPath(guildId, x.Id));
        var cardUploads = uploads.Where(x => x.Kind == AchievementIconService.CardKind).Select(x => x.Id).ToHashSet();

        var built = BuildCatalog(GetSettings(guildId), overrides, customs, categories, uploadUrls, cardUploads);
        catalogCache[guildId] = built;
        return built;
    }

    /// <summary>
    ///     The API path an upload is served on, relative to the API root, for instances without a CDN.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The upload ID.</param>
    /// <returns>The path.</returns>
    public static string IconUploadPath(ulong guildId, int id)
    {
        return $"achievements/{guildId}/icons/{id}";
    }

    /// <summary>
    ///     Drops the cached catalog of a server so the next read rebuilds it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public void RefreshCatalog(ulong guildId)
    {
        InvalidateCatalog(guildId);
    }

    private static AchievementGuildCatalog BuildCatalog(AchievementGuildSettings settings,
        List<AchievementOverride> overrides, List<CustomAchievement> customs, List<AchievementCategory> categories,
        IReadOnlyDictionary<int, string> uploadUrls, IReadOnlySet<int> cardUploads)
    {
        var overrideByKey = overrides.ToDictionary(x => x.AchievementKey, StringComparer.Ordinal);
        var customCategoryByKey = categories.ToDictionary(c => AchievementCatalog.CategoryKey(c.Id), StringComparer.Ordinal);

        var categoryInfos = AchievementCatalog.BuiltInCategories.ToList();
        categoryInfos.AddRange(categories.OrderBy(c => c.Id).Select(c => new AchievementCategoryInfo(
            AchievementCatalog.CategoryKey(c.Id), c.Name,
            string.IsNullOrWhiteSpace(c.Icon) ? AchievementIcons.GlyphPrefix + AchievementIcons.FolderGlyph : c.Icon,
            c.Description ?? "", false, false)));

        var order = settings.CategoryOrder;
        var orderedCategories = categoryInfos
            .Select((c, i) => (Category: c, Index: order.IndexOf(c.Key) is var at and >= 0 ? at : order.Count + i))
            .OrderBy(x => x.Index)
            .Select(x => x.Category)
            .ToList();

        var all = new List<AchievementDefinition>();
        foreach (var builtIn in AchievementCatalog.BuiltIns)
        {
            overrideByKey.TryGetValue(builtIn.Key, out var o);
            var categoryOff = settings.DisabledCategories.Contains(builtIn.CategoryKey);
            all.Add(new AchievementDefinition
            {
                Key = builtIn.Key,
                CategoryKey = builtIn.CategoryKey,
                Name = string.IsNullOrWhiteSpace(o?.Name) ? builtIn.Name : o!.Name!,
                Description = string.IsNullOrWhiteSpace(o?.Description) ? builtIn.Description : o!.Description!,
                Icon = o?.Icon ?? "",
                Grade = builtIn.Grade,
                Points = o?.Points ?? builtIn.Points,
                Hidden = o?.Hidden ?? builtIn.Hidden,
                Enabled = !(o?.Disabled ?? false) && !categoryOff,
                SelfEnabled = !(o?.Disabled ?? false),
                Trigger = builtIn.Trigger,
                Metric = builtIn.Metric,
                Threshold = builtIn.Threshold,
                Feat = builtIn.Feat,
                Keyword = builtIn.Keyword,
                RoleRewardId = o?.RoleRewardId,
                CurrencyReward = o?.CurrencyReward ?? 0,
                XpReward = o?.XpReward ?? 0,
                IsGlobal = builtIn.IsGlobal,
                IsOverridden = o is not null,
                Position = builtIn.Position
            });
        }

        foreach (var custom in customs.OrderBy(c => c.Position).ThenBy(c => c.Id))
        {
            var categoryKey = ResolveCategoryKey(custom.CategoryKey, customCategoryByKey);
            var trigger = (AchievementTrigger)custom.TriggerType;
            var metric = (AchievementMetric)custom.Metric;
            var grade = (AchievementGrade)Math.Clamp(custom.Grade, 0, AchievementCatalog.Grades.Count - 1);
            all.Add(new AchievementDefinition
            {
                Key = AchievementCatalog.CustomKey(custom.Id),
                CategoryKey = categoryKey,
                Name = custom.Name,
                Description = string.IsNullOrWhiteSpace(custom.Description)
                    ? DescribeGoal(trigger, metric, custom.Threshold, custom.Keyword)
                    : custom.Description!,
                Icon = custom.Icon ?? "",
                Grade = grade,
                Points = custom.Points ?? AchievementCatalog.GetGrade(grade).Points,
                Hidden = custom.Hidden,
                Enabled = custom.Enabled && !settings.DisabledCategories.Contains(categoryKey),
                SelfEnabled = custom.Enabled,
                Trigger = trigger,
                Metric = metric,
                Threshold = custom.Threshold,
                Keyword = custom.Keyword,
                ChannelId = custom.ChannelId,
                RoleRewardId = custom.RoleRewardId,
                CurrencyReward = custom.CurrencyReward,
                XpReward = custom.XpReward,
                IsCustom = true,
                CustomId = custom.Id,
                Position = 10000 + custom.Position
            });
        }

        var categoryRank = orderedCategories.Select((c, i) => (c.Key, i)).ToDictionary(x => x.Key, x => x.i);
        all = all
            .OrderBy(d => categoryRank.GetValueOrDefault(d.CategoryKey, int.MaxValue))
            .ThenBy(d => d.Position)
            .ToList();

        var earnable = all.Where(d => d.Enabled && !d.IsGlobal).ToList();
        var watchers = earnable
            .Where(d => d.Trigger is AchievementTrigger.Keyword or AchievementTrigger.Reaction &&
                        !string.IsNullOrWhiteSpace(d.Keyword))
            .ToList();

        return new AchievementGuildCatalog
        {
            All = all,
            ByKey = all.ToDictionary(d => d.Key, StringComparer.Ordinal),
            Categories = orderedCategories,
            CustomCategories = customCategoryByKey,
            Earnable = earnable,
            Watchers = watchers,
            UploadUrls = uploadUrls,
            CardUploads = cardUploads
        };
    }

    private static string ResolveCategoryKey(string key, IReadOnlyDictionary<string, AchievementCategory> custom)
    {
        if (custom.ContainsKey(key))
            return key;
        var builtIn = AchievementCatalog.GetBuiltInCategory(key);
        return builtIn is not null && key != AchievementCatalog.GlobalCategory && key != AchievementCatalog.PrestigeCategory
            ? key
            : AchievementCatalog.CustomCategory;
    }

    /// <summary>
    ///     Describes the goal of an achievement in plain words.
    /// </summary>
    /// <param name="trigger">What unlocks it.</param>
    /// <param name="metric">The metric.</param>
    /// <param name="threshold">The goal.</param>
    /// <param name="keyword">The phrase or emoji.</param>
    /// <returns>The description.</returns>
    public static string DescribeGoal(AchievementTrigger trigger, AchievementMetric metric, long threshold,
        string? keyword)
    {
        return trigger switch
        {
            AchievementTrigger.Keyword => $"Say \"{keyword}\"",
            AchievementTrigger.Reaction => $"React with {keyword}",
            AchievementTrigger.Manual => "Handed out by the staff",
            _ => DescribeMetric(metric, threshold)
        };
    }

    /// <summary>
    ///     Describes reaching a value of a metric.
    /// </summary>
    /// <param name="metric">The metric.</param>
    /// <param name="threshold">The value.</param>
    /// <returns>The description.</returns>
    public static string DescribeMetric(AchievementMetric metric, long threshold)
    {
        var info = AchievementCatalog.GetMetric(metric);
        var unit = threshold == 1 ? info.Unit : info.UnitPlural;
        return metric switch
        {
            AchievementMetric.TenureDays => $"Stay in the server for {threshold:N0} {unit}",
            AchievementMetric.BoostMonths => $"Keep boosting for {threshold:N0} {unit} in a row",
            AchievementMetric.XpLevel => $"Reach XP level {threshold:N0}",
            AchievementMetric.NetWorth => $"Have a net worth of {threshold:N0}",
            _ => $"Reach {threshold:N0} {unit} ({info.Label.ToLowerInvariant()})"
        };
    }

    private void InvalidateCatalog(ulong guildId)
    {
        catalogCache.TryRemove(guildId, out _);
    }

    #endregion

    #region Editing

    /// <summary>
    ///     Saves a server's changes to a built in achievement.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="key">The built in key.</param>
    /// <param name="draft">The changes.</param>
    /// <returns>The resolved achievement.</returns>
    public async Task<AchievementResult<AchievementDefinition>> SaveOverrideAsync(SocketGuild guild, string key,
        AchievementOverrideDraft draft)
    {
        var builtIn = AchievementCatalog.GetBuiltIn(key);
        if (builtIn is null)
            return AchievementResult<AchievementDefinition>.Fail(AchievementError.NotFound);

        var error = ValidateCommon(guild, draft.Name, draft.Description, draft.Points, draft.RoleRewardId,
            draft.CurrencyReward, draft.XpReward, allowEmptyName: true);
        if (error != AchievementError.None)
            return AchievementResult<AchievementDefinition>.Fail(error);
        var (iconError, icon) = await NormalizeIconAsync(guild.Id, draft.Icon);
        if (iconError != AchievementError.None)
            return AchievementResult<AchievementDefinition>.Fail(iconError);

        int? oldPoints;
        int newPoints;
        await writeLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var row = await db.AchievementOverrides.FirstOrDefaultAsync(x => x.GuildId == guild.Id && x.AchievementKey == key);
            oldPoints = row?.Points ?? builtIn.Points;
            var isNew = row is null;
            row ??= new AchievementOverride
            {
                GuildId = guild.Id,
                AchievementKey = key
            };

            row.Disabled = !draft.Enabled;
            row.Name = Clean(draft.Name);
            row.Description = Clean(draft.Description);
            row.Icon = icon;
            row.Points = draft.Points is { } p && p != builtIn.Points ? p : null;
            row.Hidden = draft.Hidden is { } h && h != builtIn.Hidden ? h : null;
            row.RoleRewardId = draft.RoleRewardId is > 0 ? draft.RoleRewardId : null;
            row.CurrencyReward = Math.Max(0, draft.CurrencyReward);
            row.XpReward = Math.Max(0, draft.XpReward);
            row.DateModified = DateTime.UtcNow;
            newPoints = row.Points ?? builtIn.Points;

            if (IsDefault(row))
            {
                if (!isNew)
                    await db.AchievementOverrides.Where(x => x.Id == row.Id).DeleteAsync();
            }
            else if (isNew)
            {
                await db.InsertAsync(row);
            }
            else
            {
                await db.UpdateAsync(row);
            }
        }
        finally
        {
            writeLock.Release();
        }

        InvalidateCatalog(guild.Id);
        if (oldPoints != newPoints)
            await RepriceAsync(guild.Id, key, newPoints);

        var catalog = await GetCatalogAsync(guild.Id);
        return AchievementResult<AchievementDefinition>.Ok(catalog.ByKey[key]);
    }

    /// <summary>
    ///     Puts a built in achievement back to its default.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="key">The built in key.</param>
    /// <returns>The resolved achievement.</returns>
    public async Task<AchievementResult<AchievementDefinition>> ResetOverrideAsync(ulong guildId, string key)
    {
        var builtIn = AchievementCatalog.GetBuiltIn(key);
        if (builtIn is null)
            return AchievementResult<AchievementDefinition>.Fail(AchievementError.NotFound);

        int? oldPoints;
        await using (var db = await dbFactory.CreateConnectionAsync())
        {
            oldPoints = await db.AchievementOverrides
                .Where(x => x.GuildId == guildId && x.AchievementKey == key)
                .Select(x => x.Points)
                .FirstOrDefaultAsync();
            await db.AchievementOverrides.Where(x => x.GuildId == guildId && x.AchievementKey == key).DeleteAsync();
        }

        InvalidateCatalog(guildId);
        if (oldPoints is not null && oldPoints != builtIn.Points)
            await RepriceAsync(guildId, key, builtIn.Points);

        var catalog = await GetCatalogAsync(guildId);
        return AchievementResult<AchievementDefinition>.Ok(catalog.ByKey[key]);
    }

    /// <summary>
    ///     Turns one achievement on or off, built in or custom.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="key">The achievement key.</param>
    /// <param name="enabled">The new state.</param>
    /// <returns>The resolved achievement.</returns>
    public async Task<AchievementResult<AchievementDefinition>> SetAchievementEnabledAsync(ulong guildId, string key,
        bool enabled)
    {
        if (AchievementCatalog.TryParseCustomKey(key, out var id))
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var updated = await db.CustomAchievements
                .Where(x => x.GuildId == guildId && x.Id == id)
                .Set(x => x.Enabled, enabled)
                .Set(x => x.DateModified, DateTime.UtcNow)
                .UpdateAsync();
            if (updated == 0)
                return AchievementResult<AchievementDefinition>.Fail(AchievementError.NotFound);
        }
        else
        {
            if (AchievementCatalog.GetBuiltIn(key) is null)
                return AchievementResult<AchievementDefinition>.Fail(AchievementError.NotFound);

            await writeLock.WaitAsync();
            try
            {
                await using var db = await dbFactory.CreateConnectionAsync();
                var row = await db.AchievementOverrides.FirstOrDefaultAsync(x => x.GuildId == guildId && x.AchievementKey == key);
                if (row is null)
                {
                    if (!enabled)
                    {
                        await db.InsertAsync(new AchievementOverride
                        {
                            GuildId = guildId,
                            AchievementKey = key,
                            Disabled = true,
                            DateModified = DateTime.UtcNow
                        });
                    }
                }
                else
                {
                    row.Disabled = !enabled;
                    row.DateModified = DateTime.UtcNow;
                    if (IsDefault(row))
                        await db.AchievementOverrides.Where(x => x.Id == row.Id).DeleteAsync();
                    else
                        await db.UpdateAsync(row);
                }
            }
            finally
            {
                writeLock.Release();
            }
        }

        InvalidateCatalog(guildId);
        var catalog = await GetCatalogAsync(guildId);
        return AchievementResult<AchievementDefinition>.Ok(catalog.ByKey[key]);
    }

    /// <summary>
    ///     Turns many achievements on or off at once.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="keys">The achievement keys.</param>
    /// <param name="enabled">The new state.</param>
    /// <returns>How many changed.</returns>
    public async Task<int> SetManyEnabledAsync(ulong guildId, IReadOnlyCollection<string> keys, bool enabled)
    {
        var changed = 0;
        foreach (var key in keys.Distinct())
        {
            var result = await SetAchievementEnabledAsync(guildId, key, enabled);
            if (result.Success)
                changed++;
        }

        return changed;
    }

    /// <summary>
    ///     Creates a server made achievement.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="createdBy">Who made it.</param>
    /// <param name="draft">The achievement.</param>
    /// <returns>The resolved achievement.</returns>
    public async Task<AchievementResult<AchievementDefinition>> CreateCustomAsync(SocketGuild guild, ulong createdBy,
        CustomAchievementDraft draft)
    {
        var error = await ValidateCustomAsync(guild, draft);
        if (error != AchievementError.None)
            return AchievementResult<AchievementDefinition>.Fail(error);

        int id;
        await writeLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var count = await db.CustomAchievements.CountAsync(x => x.GuildId == guild.Id);
            if (count >= MaxCustomAchievements)
                return AchievementResult<AchievementDefinition>.Fail(AchievementError.TooManyAchievements);

            var position = await db.CustomAchievements
                .Where(x => x.GuildId == guild.Id)
                .Select(x => (int?)x.Position)
                .MaxAsync() ?? -1;

            var row = new CustomAchievement
            {
                GuildId = guild.Id,
                CreatedBy = createdBy,
                Position = position + 1,
                DateAdded = DateTime.UtcNow
            };
            Apply(row, draft);
            id = await db.InsertWithInt32IdentityAsync(row);
        }
        finally
        {
            writeLock.Release();
        }

        InvalidateCatalog(guild.Id);
        if (draft.Trigger == AchievementTrigger.Metric)
            QueueSweep(guild.Id);

        var catalog = await GetCatalogAsync(guild.Id);
        return AchievementResult<AchievementDefinition>.Ok(catalog.ByKey[AchievementCatalog.CustomKey(id)]);
    }

    /// <summary>
    ///     Replaces a server made achievement.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="id">The achievement ID.</param>
    /// <param name="draft">The new state.</param>
    /// <returns>The resolved achievement.</returns>
    public async Task<AchievementResult<AchievementDefinition>> UpdateCustomAsync(SocketGuild guild, int id,
        CustomAchievementDraft draft)
    {
        var error = await ValidateCustomAsync(guild, draft);
        if (error != AchievementError.None)
            return AchievementResult<AchievementDefinition>.Fail(error);

        int oldPoints;
        int newPoints;
        await writeLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var row = await db.CustomAchievements.FirstOrDefaultAsync(x => x.GuildId == guild.Id && x.Id == id);
            if (row is null)
                return AchievementResult<AchievementDefinition>.Fail(AchievementError.NotFound);

            oldPoints = row.Points ?? AchievementCatalog.GetGrade((AchievementGrade)row.Grade).Points;
            Apply(row, draft);
            row.DateModified = DateTime.UtcNow;
            newPoints = row.Points ?? AchievementCatalog.GetGrade((AchievementGrade)row.Grade).Points;
            await db.UpdateAsync(row);
        }
        finally
        {
            writeLock.Release();
        }

        var key = AchievementCatalog.CustomKey(id);
        InvalidateCatalog(guild.Id);
        if (oldPoints != newPoints)
            await RepriceAsync(guild.Id, key, newPoints);
        if (draft.Trigger == AchievementTrigger.Metric)
            QueueSweep(guild.Id);

        var catalog = await GetCatalogAsync(guild.Id);
        return AchievementResult<AchievementDefinition>.Ok(catalog.ByKey[key]);
    }

    /// <summary>
    ///     Deletes a server made achievement and every unlock of it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The achievement ID.</param>
    /// <returns>True when it existed.</returns>
    public async Task<bool> DeleteCustomAsync(ulong guildId, int id)
    {
        var key = AchievementCatalog.CustomKey(id);
        await using var db = await dbFactory.CreateConnectionAsync();
        var deleted = await db.CustomAchievements.Where(x => x.GuildId == guildId && x.Id == id).DeleteAsync();
        if (deleted == 0)
            return false;

        await db.UserAchievements.Where(x => x.GuildId == guildId && x.AchievementKey == key).DeleteAsync();
        await RecountMembersAsync(db, guildId);
        await db.AchievementMembers
            .Where(x => x.GuildId == guildId && (x.Badge1 == key || x.Badge2 == key || x.Badge3 == key || x.Badge4 == key))
            .Set(x => x.Badge1, x => x.Badge1 == key ? null : x.Badge1)
            .Set(x => x.Badge2, x => x.Badge2 == key ? null : x.Badge2)
            .Set(x => x.Badge3, x => x.Badge3 == key ? null : x.Badge3)
            .Set(x => x.Badge4, x => x.Badge4 == key ? null : x.Badge4)
            .UpdateAsync();

        DropCachedKey(guildId, key);
        InvalidateCatalog(guildId);
        return true;
    }

    /// <summary>
    ///     Sets the order of a server's own achievements.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ids">Achievement IDs in the new order. IDs not listed keep their place after these.</param>
    /// <returns>A task that completes when saved.</returns>
    public async Task ReorderCustomAsync(ulong guildId, IReadOnlyList<int> ids)
    {
        await writeLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var rows = await db.CustomAchievements.Where(x => x.GuildId == guildId).ToListAsync();
            var ordered = ids.Distinct()
                .Select(id => rows.FirstOrDefault(r => r.Id == id))
                .Where(r => r is not null)
                .Concat(rows.Where(r => !ids.Contains(r.Id)).OrderBy(r => r.Position))
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                if (ordered[i]!.Position == i)
                    continue;
                var rowId = ordered[i]!.Id;
                await db.CustomAchievements.Where(x => x.Id == rowId).Set(x => x.Position, i).UpdateAsync();
            }
        }
        finally
        {
            writeLock.Release();
        }

        InvalidateCatalog(guildId);
    }

    /// <summary>
    ///     Creates a server made category.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">Name.</param>
    /// <param name="description">Description.</param>
    /// <param name="icon">Icon, or null for the folder.</param>
    /// <returns>The category.</returns>
    public async Task<AchievementResult<AchievementCategory>> CreateCategoryAsync(ulong guildId, string name,
        string? description, string? icon)
    {
        var error = ValidateCategory(name, description);
        if (error != AchievementError.None)
            return AchievementResult<AchievementCategory>.Fail(error);
        var (iconError, storedIcon) = await NormalizeIconAsync(guildId, icon);
        if (iconError != AchievementError.None)
            return AchievementResult<AchievementCategory>.Fail(iconError);

        await using var db = await dbFactory.CreateConnectionAsync();
        if (await db.AchievementCategories.CountAsync(x => x.GuildId == guildId) >= MaxCustomCategories)
            return AchievementResult<AchievementCategory>.Fail(AchievementError.TooManyCategories);

        var row = new AchievementCategory
        {
            GuildId = guildId,
            Name = name.Trim(),
            Description = Clean(description),
            Icon = storedIcon,
            DateAdded = DateTime.UtcNow
        };
        row.Id = await db.InsertWithInt32IdentityAsync(row);
        InvalidateCatalog(guildId);
        return AchievementResult<AchievementCategory>.Ok(row);
    }

    /// <summary>
    ///     Renames a server made category.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The category ID.</param>
    /// <param name="name">Name.</param>
    /// <param name="description">Description.</param>
    /// <param name="icon">Icon, or null for the folder.</param>
    /// <returns>The category.</returns>
    public async Task<AchievementResult<AchievementCategory>> UpdateCategoryAsync(ulong guildId, int id, string name,
        string? description, string? icon)
    {
        var error = ValidateCategory(name, description);
        if (error != AchievementError.None)
            return AchievementResult<AchievementCategory>.Fail(error);
        var (iconError, storedIcon) = await NormalizeIconAsync(guildId, icon);
        if (iconError != AchievementError.None)
            return AchievementResult<AchievementCategory>.Fail(iconError);

        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.AchievementCategories.FirstOrDefaultAsync(x => x.GuildId == guildId && x.Id == id);
        if (row is null)
            return AchievementResult<AchievementCategory>.Fail(AchievementError.NotFound);

        row.Name = name.Trim();
        row.Description = Clean(description);
        row.Icon = storedIcon;
        await db.UpdateAsync(row);
        InvalidateCatalog(guildId);
        return AchievementResult<AchievementCategory>.Ok(row);
    }

    /// <summary>
    ///     Deletes a server made category, moving its achievements to the server category.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The category ID.</param>
    /// <returns>True when it existed.</returns>
    public async Task<bool> DeleteCategoryAsync(ulong guildId, int id)
    {
        var key = AchievementCatalog.CategoryKey(id);
        await using var db = await dbFactory.CreateConnectionAsync();
        var deleted = await db.AchievementCategories.Where(x => x.GuildId == guildId && x.Id == id).DeleteAsync();
        if (deleted == 0)
            return false;

        await db.CustomAchievements
            .Where(x => x.GuildId == guildId && x.CategoryKey == key)
            .Set(x => x.CategoryKey, AchievementCatalog.CustomCategory)
            .UpdateAsync();

        var settings = GetSettings(guildId);
        if (settings.DisabledCategories.Contains(key) || settings.CategoryOrder.Contains(key))
        {
            await UpdateSettingsAsync(guildId, row =>
            {
                row.DisabledCategories = JoinKeys(SplitKeys(row.DisabledCategories).Where(k => k != key));
                row.CategoryOrder = JoinKeys(SplitKeys(row.CategoryOrder).Where(k => k != key));
            });
        }

        InvalidateCatalog(guildId);
        return true;
    }

    /// <summary>
    ///     Saves the category order and which categories are off.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="order">Category keys in order.</param>
    /// <param name="disabled">Category keys turned off.</param>
    /// <returns>The updated settings.</returns>
    public async Task<AchievementGuildSettings> SetCategoryLayoutAsync(ulong guildId, IEnumerable<string> order,
        IEnumerable<string> disabled)
    {
        var catalog = await GetCatalogAsync(guildId);
        var known = catalog.Categories.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        var orderList = order.Where(known.Contains).Distinct().ToList();
        var disabledList = disabled.Where(known.Contains).Distinct().ToList();
        return await UpdateSettingsAsync(guildId, row =>
        {
            row.CategoryOrder = JoinKeys(orderList);
            row.DisabledCategories = JoinKeys(disabledList);
        });
    }

    /// <summary>
    ///     A server made achievement as an editor would load it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The achievement ID.</param>
    /// <returns>The draft, or null.</returns>
    public async Task<CustomAchievementDraft?> GetCustomDraftAsync(ulong guildId, int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.CustomAchievements.FirstOrDefaultAsync(x => x.GuildId == guildId && x.Id == id);
        return row is null
            ? null
            : new CustomAchievementDraft
            {
                CategoryKey = row.CategoryKey,
                Name = row.Name,
                Description = row.Description,
                Icon = row.Icon,
                Grade = (AchievementGrade)row.Grade,
                Points = row.Points,
                Hidden = row.Hidden,
                Enabled = row.Enabled,
                Trigger = (AchievementTrigger)row.TriggerType,
                Metric = (AchievementMetric)row.Metric,
                Threshold = row.Threshold,
                Keyword = row.Keyword,
                ChannelId = row.ChannelId,
                RoleRewardId = row.RoleRewardId,
                CurrencyReward = row.CurrencyReward,
                XpReward = row.XpReward
            };
    }

    /// <summary>
    ///     A server's changes to a built in achievement as an editor would load them.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="key">The built in key.</param>
    /// <returns>The draft, or null when the key is unknown.</returns>
    public async Task<AchievementOverrideDraft?> GetOverrideDraftAsync(ulong guildId, string key)
    {
        if (AchievementCatalog.GetBuiltIn(key) is null)
            return null;

        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.AchievementOverrides.FirstOrDefaultAsync(x => x.GuildId == guildId && x.AchievementKey == key);
        return new AchievementOverrideDraft
        {
            Enabled = !(row?.Disabled ?? false),
            Name = row?.Name,
            Description = row?.Description,
            Icon = row?.Icon,
            Points = row?.Points,
            Hidden = row?.Hidden,
            RoleRewardId = row?.RoleRewardId,
            CurrencyReward = row?.CurrencyReward ?? 0,
            XpReward = row?.XpReward ?? 0
        };
    }

    /// <summary>
    ///     Changes the rewards of any achievement, built in or server made.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="key">The achievement key.</param>
    /// <param name="roleId">New reward role, 0 to clear, or null to keep.</param>
    /// <param name="currency">New currency reward, or null to keep.</param>
    /// <param name="xp">New XP reward, or null to keep.</param>
    /// <returns>The resolved achievement.</returns>
    public async Task<AchievementResult<AchievementDefinition>> SetRewardsAsync(SocketGuild guild, string key,
        ulong? roleId, long? currency, int? xp)
    {
        if (AchievementCatalog.TryParseCustomKey(key, out var id))
        {
            var draft = await GetCustomDraftAsync(guild.Id, id);
            if (draft is null)
                return AchievementResult<AchievementDefinition>.Fail(AchievementError.NotFound);
            if (roleId is not null)
                draft.RoleRewardId = roleId == 0 ? null : roleId;
            draft.CurrencyReward = currency ?? draft.CurrencyReward;
            draft.XpReward = xp ?? draft.XpReward;
            return await UpdateCustomAsync(guild, id, draft);
        }

        var overrideDraft = await GetOverrideDraftAsync(guild.Id, key);
        if (overrideDraft is null)
            return AchievementResult<AchievementDefinition>.Fail(AchievementError.NotFound);
        if (roleId is not null)
            overrideDraft.RoleRewardId = roleId == 0 ? null : roleId;
        overrideDraft.CurrencyReward = currency ?? overrideDraft.CurrencyReward;
        overrideDraft.XpReward = xp ?? overrideDraft.XpReward;
        return await SaveOverrideAsync(guild, key, overrideDraft);
    }

    /// <summary>
    ///     Changes the icon of any achievement, built in or server made.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="key">The achievement key.</param>
    /// <param name="icon">The icon, or null to use its category's.</param>
    /// <returns>The resolved achievement.</returns>
    public async Task<AchievementResult<AchievementDefinition>> SetIconAsync(SocketGuild guild, string key,
        string? icon)
    {
        if (AchievementCatalog.TryParseCustomKey(key, out var id))
        {
            var draft = await GetCustomDraftAsync(guild.Id, id);
            if (draft is null)
                return AchievementResult<AchievementDefinition>.Fail(AchievementError.NotFound);
            draft.Icon = icon;
            return await UpdateCustomAsync(guild, id, draft);
        }

        var overrideDraft = await GetOverrideDraftAsync(guild.Id, key);
        if (overrideDraft is null)
            return AchievementResult<AchievementDefinition>.Fail(AchievementError.NotFound);
        overrideDraft.Icon = icon;
        return await SaveOverrideAsync(guild, key, overrideDraft);
    }

    /// <summary>
    ///     Adds or removes a role or channel from the server's exclusions.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The role or channel ID.</param>
    /// <param name="isRole">True for a role, false for a channel.</param>
    /// <returns>True when it is now excluded.</returns>
    public async Task<bool> ToggleExclusionAsync(ulong guildId, ulong id, bool isRole)
    {
        var excluded = false;
        await UpdateSettingsAsync(guildId, row =>
        {
            var set = SplitIds(isRole ? row.ExcludedRoleIds : row.ExcludedChannelIds);
            excluded = set.Add(id);
            if (!excluded)
                set.Remove(id);
            if (isRole)
                row.ExcludedRoleIds = JoinKeys(set);
            else
                row.ExcludedChannelIds = JoinKeys(set);
        });
        return excluded;
    }

    private async Task<AchievementError> ValidateCustomAsync(SocketGuild guild, CustomAchievementDraft draft)
    {
        var error = ValidateCommon(guild, draft.Name, draft.Description, draft.Points, draft.RoleRewardId,
            draft.CurrencyReward, draft.XpReward, allowEmptyName: false);
        if (error != AchievementError.None)
            return error;

        if (!Enum.IsDefined(draft.Grade))
            return AchievementError.RewardInvalid;
        if ((await NormalizeIconAsync(guild.Id, draft.Icon)).Error != AchievementError.None)
            return AchievementError.IconInvalid;

        switch (draft.Trigger)
        {
            case AchievementTrigger.Metric:
                if (!AchievementCatalog.GetMetric(draft.Metric).AllowCustom)
                    return AchievementError.MetricInvalid;
                if (draft.Threshold <= 0)
                    return AchievementError.ThresholdInvalid;
                break;
            case AchievementTrigger.Keyword:
            case AchievementTrigger.Reaction:
                if (string.IsNullOrWhiteSpace(draft.Keyword))
                    return AchievementError.KeywordMissing;
                if (draft.Keyword.Length > KeywordLength)
                    return AchievementError.TextTooLong;
                break;
            case AchievementTrigger.Manual:
                break;
            default:
                return AchievementError.MetricInvalid;
        }

        var catalog = await GetCatalogAsync(guild.Id);
        var categoryKey = string.IsNullOrWhiteSpace(draft.CategoryKey) ? AchievementCatalog.CustomCategory : draft.CategoryKey;
        if (categoryKey is AchievementCatalog.GlobalCategory or AchievementCatalog.PrestigeCategory ||
            catalog.Categories.All(c => c.Key != categoryKey))
            return AchievementError.CategoryInvalid;

        return AchievementError.None;
    }

    private AchievementError ValidateCommon(SocketGuild guild, string? name, string? description, int? points,
        ulong? roleId, long currency, int xp, bool allowEmptyName)
    {
        var trimmed = name?.Trim() ?? "";
        if (!allowEmptyName && trimmed.Length == 0 || trimmed.Length > NameLength)
            return AchievementError.NameInvalid;
        if ((description?.Length ?? 0) > DescriptionLength)
            return AchievementError.TextTooLong;
        if (points is < 0 or > MaxPoints || currency < 0 || currency > MaxReward || xp < 0 || xp > MaxReward)
            return AchievementError.RewardInvalid;
        if (roleId is > 0 && !CanAssign(guild, roleId.Value))
            return AchievementError.RoleNotAssignable;
        return AchievementError.None;
    }

    /// <summary>
    ///     Checks an icon and turns it into its stored form. Uploads must belong to the server.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="input">The icon as given.</param>
    /// <returns>The error, and the value to store or null to clear.</returns>
    public async Task<(AchievementError Error, string? Stored)> NormalizeIconAsync(ulong guildId, string? input)
    {
        if (!AchievementIcons.TryNormalize(input, out var stored))
            return (AchievementError.IconInvalid, null);
        if (AchievementIcons.UploadId(AchievementIcons.Parse(stored)) is not { } uploadId)
            return (AchievementError.None, stored);

        await using var db = await dbFactory.CreateConnectionAsync();
        var owned = await db.AchievementIconUploads.AnyAsync(x => x.Id == uploadId && x.GuildId == guildId);
        return owned ? (AchievementError.None, stored) : (AchievementError.IconInvalid, null);
    }

    private static AchievementError ValidateCategory(string name, string? description)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0 || trimmed.Length > NameLength)
            return AchievementError.NameInvalid;
        return (description?.Length ?? 0) > DescriptionLength ? AchievementError.TextTooLong : AchievementError.None;
    }

    /// <summary>
    ///     Whether the bot can give out a role.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="roleId">The role ID.</param>
    /// <returns>True when the role exists, is not managed, and sits below the bot.</returns>
    public static bool CanAssign(SocketGuild guild, ulong roleId)
    {
        var role = guild.GetRole(roleId);
        if (role is null || role.IsManaged || role.Id == guild.EveryoneRole.Id)
            return false;
        var bot = guild.CurrentUser;
        return bot.GuildPermissions.ManageRoles && bot.Hierarchy > role.Position;
    }

    private static void Apply(CustomAchievement row, CustomAchievementDraft draft)
    {
        row.CategoryKey = string.IsNullOrWhiteSpace(draft.CategoryKey) ? AchievementCatalog.CustomCategory : draft.CategoryKey;
        row.Name = draft.Name.Trim();
        row.Description = Clean(draft.Description);
        row.Icon = AchievementIcons.TryNormalize(draft.Icon, out var icon) ? icon : null;
        row.Grade = (int)draft.Grade;
        row.Points = draft.Points;
        row.Hidden = draft.Hidden;
        row.Enabled = draft.Enabled;
        row.TriggerType = (int)draft.Trigger;
        row.Metric = draft.Trigger == AchievementTrigger.Metric ? (int)draft.Metric : 0;
        row.Threshold = draft.Trigger == AchievementTrigger.Metric ? draft.Threshold : 0;
        row.Keyword = draft.Trigger is AchievementTrigger.Keyword or AchievementTrigger.Reaction
            ? draft.Keyword?.Trim()
            : null;
        row.ChannelId = draft.Trigger is AchievementTrigger.Keyword or AchievementTrigger.Reaction && draft.ChannelId is > 0
            ? draft.ChannelId
            : null;
        row.RoleRewardId = draft.RoleRewardId is > 0 ? draft.RoleRewardId : null;
        row.CurrencyReward = Math.Max(0, draft.CurrencyReward);
        row.XpReward = Math.Max(0, draft.XpReward);
    }

    private static bool IsDefault(AchievementOverride row)
    {
        return !row.Disabled && row.Name is null && row.Description is null && row.Icon is null &&
               row.Points is null && row.Hidden is null && row.RoleRewardId is null && row.CurrencyReward == 0 &&
               row.XpReward == 0;
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private async Task RepriceAsync(ulong guildId, string key, int points)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var changed = await db.UserAchievements
            .Where(x => x.GuildId == guildId && x.AchievementKey == key)
            .Set(x => x.Points, points)
            .UpdateAsync();
        if (changed > 0)
            await RecountMembersAsync(db, guildId);
    }

    private static async Task RecountMembersAsync(MewdekoDb db, ulong guildId)
    {
        await db.AchievementMembers
            .Where(m => m.GuildId == guildId)
            .Set(m => m.Points,
                m => db.UserAchievements.Where(u => u.GuildId == m.GuildId && u.UserId == m.UserId)
                    .Sum(u => (int?)u.Points) ?? 0)
            .Set(m => m.UnlockedCount,
                m => db.UserAchievements.Count(u => u.GuildId == m.GuildId && u.UserId == m.UserId))
            .UpdateAsync();
    }

    #endregion
}
