using System.IO;
using System.Text;
using DataModel;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Achievements.Services;

/// <summary>
///     Builds the embeds, pages, and cards achievement commands show, so text and slash commands look the same.
/// </summary>
public sealed class AchievementViewService : INService
{
    /// <summary>
    ///     Achievements per page in long lists.
    /// </summary>
    public const int PageSize = 8;

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly AchievementCardRenderer renderer;
    private readonly AchievementService service;
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AchievementViewService" /> class.
    /// </summary>
    /// <param name="service">The achievement service.</param>
    /// <param name="renderer">The profile card renderer.</param>
    /// <param name="strings">Localized bot strings.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">Database connection factory.</param>
    public AchievementViewService(AchievementService service, AchievementCardRenderer renderer,
        GeneratedBotStrings strings, DiscordShardedClient client, IDataConnectionFactory dbFactory)
    {
        this.service = service;
        this.renderer = renderer;
        this.strings = strings;
        this.client = client;
        this.dbFactory = dbFactory;
    }

    /// <summary>
    ///     A text progress bar.
    /// </summary>
    /// <param name="fraction">0 to 1.</param>
    /// <param name="width">Segments.</param>
    /// <returns>The bar.</returns>
    public static string Bar(double fraction, int width = 10)
    {
        var filled = (int)Math.Round(Math.Clamp(fraction, 0, 1) * width);
        return new string('▰', filled) + new string('▱', width - filled);
    }

    /// <summary>
    ///     Why a member may not see something, or null when they may.
    /// </summary>
    /// <param name="guildId">The guild ID, for localization.</param>
    /// <param name="viewer">Who is looking.</param>
    /// <param name="target">Whose achievements.</param>
    /// <param name="area">What they want to see.</param>
    /// <returns>The refusal text, or null.</returns>
    public async Task<string?> PrivacyRefusalAsync(ulong guildId, IUser viewer, IUser target,
        AchievementPrivacyArea area)
    {
        return await service.CanViewAsync(viewer.Id, target.Id, area)
            ? null
            : strings.AchievementPrivate(guildId, target.Username);
    }

    /// <summary>
    ///     The overview: rank, points, progress per category, and recent unlocks.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="target">Whose achievements.</param>
    /// <param name="prefix">The command prefix, for the hint.</param>
    /// <returns>The embed.</returns>
    public async Task<EmbedBuilder> OverviewAsync(SocketGuild guild, SocketGuildUser target, string prefix)
    {
        var summary = await service.GetSummaryAsync(guild.Id, target.Id);
        var progress = await service.GetProgressAsync(guild, target.Id, d => d.Enabled);
        var catalog = await service.GetCatalogAsync(guild.Id);

        var description = new StringBuilder()
            .AppendLine(strings.AchievementOverviewHeadline(guild.Id, summary.Tier.Name, summary.Points.ToString("N0"),
                summary.Rank > 0 ? $"#{summary.Rank:N0}" : "-"))
            .AppendLine(strings.AchievementOverviewUnlocked(guild.Id, summary.Unlocked.ToString("N0"),
                summary.Total.ToString("N0")));
        if (summary.NextTier is { } next)
        {
            var span = Math.Max(1, next.MinPoints - summary.Tier.MinPoints);
            var fraction = (double)(summary.Points - summary.Tier.MinPoints) / span;
            description.AppendLine(strings.AchievementOverviewNextTier(guild.Id, Bar(fraction),
                (next.MinPoints - summary.Points).ToString("N0"), next.Name));
        }
        else
        {
            description.AppendLine(strings.AchievementOverviewTopTier(guild.Id));
        }

        var embed = new EmbedBuilder()
            .WithColor(TierColor(summary))
            .WithAuthor(target.DisplayName, target.GetDisplayAvatarUrl())
            .WithTitle(strings.AchievementOverviewTitle(guild.Id))
            .WithDescription(description.ToString());

        foreach (var category in catalog.Categories)
        {
            var items = progress.Where(p => p.Definition.CategoryKey == category.Key).ToList();
            if (items.Count == 0)
                continue;
            var done = items.Count(p => p.Unlocked);
            var value = strings.AchievementOverviewCategory(guild.Id, Bar((double)done / items.Count, 8), done,
                items.Count);
            embed.AddField(category.Name, value, true);
        }

        var recent = progress.Where(p => p.Unlocked).OrderByDescending(p => p.UnlockedAt).Take(3).ToList();
        if (recent.Count > 0)
        {
            var lines = recent.Select(p => strings.AchievementRecentLine(guild.Id,
                p.Definition.Name, TimestampTag.FromDateTime(DateTime.SpecifyKind(p.UnlockedAt!.Value, DateTimeKind.Utc),
                    TimestampTagStyles.Relative)));
            embed.AddField(strings.AchievementRecentTitle(guild.Id), string.Join("\n", lines));
        }

        embed.WithFooter(strings.AchievementOverviewHint(guild.Id, prefix));
        return embed;
    }

    /// <summary>
    ///     Pages of achievements in one category, or every category when the key is null.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="target">Whose progress.</param>
    /// <param name="categoryKey">The category key, or null for all.</param>
    /// <returns>The pages, or null when the category is unknown.</returns>
    public async Task<List<PageBuilder>?> CategoryPagesAsync(SocketGuild guild, SocketGuildUser target,
        string? categoryKey)
    {
        var catalog = await service.GetCatalogAsync(guild.Id);
        AchievementCategoryInfo? category = null;
        if (categoryKey is not null)
        {
            category = FindCategory(catalog, categoryKey);
            if (category is null)
                return null;
        }

        var progress = await service.GetProgressAsync(guild, target.Id,
            d => category is null || d.CategoryKey == category.Key);
        var reveal = service.GetSettings(guild.Id).Row.RevealHidden;
        var title = category is null
            ? strings.AchievementAllTitle(guild.Id)
            : category.Name;
        var done = progress.Count(p => p.Unlocked);

        if (progress.Count == 0)
        {
            return
            [
                new PageBuilder()
                    .WithOkColor()
                    .WithTitle(title)
                    .WithDescription(strings.AchievementCategoryEmpty(guild.Id))
            ];
        }

        return progress
            .Chunk(PageSize)
            .Select((chunk, index) => new PageBuilder()
                .WithOkColor()
                .WithAuthor(target.DisplayName, target.GetDisplayAvatarUrl())
                .WithTitle(title)
                .WithDescription(string.Join("\n\n", chunk.Select(p => ProgressLine(guild.Id, p, reveal))))
                .WithFooter(strings.AchievementCategoryFooter(guild.Id, done, progress.Count, index + 1,
                    (progress.Count + PageSize - 1) / PageSize)))
            .ToList();
    }

    /// <summary>
    ///     One achievement as a list entry with its progress.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="progress">The progress.</param>
    /// <param name="reveal">Whether hidden achievements show their names.</param>
    /// <returns>The entry.</returns>
    public string ProgressLine(ulong guildId, AchievementProgress progress, bool reveal)
    {
        var def = progress.Definition;
        var grade = AchievementCatalog.GetGrade(def.Grade);
        if (progress.Unlocked)
        {
            var when = TimestampTag.FromDateTime(DateTime.SpecifyKind(progress.UnlockedAt!.Value, DateTimeKind.Utc),
                TimestampTagStyles.ShortDate);
            return strings.AchievementLineUnlocked(guildId, def.Name, grade.Name, def.Points, def.Description, when);
        }

        if (def.Hidden && !reveal)
            return strings.AchievementLineHidden(guildId, grade.Name, def.Points);

        if (def.IsMetric && progress.Current is { } current)
        {
            var shown = Math.Min(current, def.Threshold);
            return strings.AchievementLineProgress(guildId, def.Name, grade.Name, def.Points, def.Description,
                Bar(progress.Fraction), shown.ToString("N0"), def.Threshold.ToString("N0"));
        }

        return strings.AchievementLineLocked(guildId, def.Name, grade.Name, def.Points, def.Description);
    }

    /// <summary>
    ///     A member's five most recent or five best achievements.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="target">Whose achievements.</param>
    /// <param name="highest">True for best by grade and points, false for most recent.</param>
    /// <returns>The embed.</returns>
    public async Task<EmbedBuilder> TopFiveAsync(SocketGuild guild, SocketGuildUser target, bool highest)
    {
        var progress = (await service.GetProgressAsync(guild, target.Id)).Where(p => p.Unlocked);
        var picked = (highest
                ? progress.OrderByDescending(p => p.Definition.Grade).ThenByDescending(p => p.Definition.Points)
                : progress.OrderByDescending(p => p.UnlockedAt))
            .Take(5)
            .ToList();

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithAuthor(target.DisplayName, target.GetDisplayAvatarUrl())
            .WithTitle(highest ? strings.AchievementHighestTitle(guild.Id) : strings.AchievementLatestTitle(guild.Id));

        var description = picked.Count == 0
            ? strings.AchievementNoneYet(guild.Id, target.DisplayName)
            : string.Join("\n\n", picked.Select(p => ProgressLine(guild.Id, p, true)));
        return embed.WithDescription(description);
    }

    /// <summary>
    ///     Achievements whose name or description matches, with the caller's progress.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="viewer">Who searched.</param>
    /// <param name="query">The search text.</param>
    /// <returns>The embed.</returns>
    public async Task<EmbedBuilder> SearchAsync(SocketGuild guild, SocketGuildUser viewer, string query)
    {
        var reveal = service.GetSettings(guild.Id).Row.RevealHidden;
        var unlocks = await service.GetUnlocksAsync(guild.Id, viewer.Id);
        var progress = await service.GetProgressAsync(guild, viewer.Id, d =>
            (!d.Hidden || reveal || unlocks.ContainsKey(d.Key)) &&
            (d.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             d.Description.Contains(query, StringComparison.OrdinalIgnoreCase)));

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(strings.AchievementSearchTitle(guild.Id, query.TrimTo(60)));
        if (progress.Count == 0)
            return embed.WithDescription(strings.AchievementSearchNone(guild.Id));

        var shown = progress.Take(PageSize).ToList();
        embed.WithDescription(string.Join("\n\n", shown.Select(p => ProgressLine(guild.Id, p, reveal))));
        if (progress.Count > shown.Count)
            embed.WithFooter(strings.AchievementSearchMore(guild.Id, progress.Count - shown.Count));
        return embed;
    }

    /// <summary>
    ///     A member's global achievements and totals.
    /// </summary>
    /// <param name="guildId">The guild ID, for localization.</param>
    /// <param name="target">Whose achievements.</param>
    /// <returns>The embed.</returns>
    public async Task<EmbedBuilder> GlobalAsync(ulong guildId, IUser target)
    {
        var (points, unlocked, servers) = await service.GetGlobalTotalsAsync(target.Id);
        var progress = await service.GetGlobalProgressAsync(target.Id);
        var tier = AchievementCatalog.GetTier((int)Math.Min(points, int.MaxValue));
        var headline = strings.AchievementGlobalHeadline(guildId, points.ToString("N0"), unlocked.ToString("N0"),
            servers.ToString("N0"), tier.Name);
        var description = headline + "\n\n" + string.Join("\n\n", progress.Select(p => ProgressLine(guildId, p, true)));

        return new EmbedBuilder()
            .WithColor(tier.Grade is { } grade ? new Color(AchievementCatalog.GetGrade(grade).Color) : Mewdeko.OkColor)
            .WithAuthor(target.GlobalName ?? target.Username, target.GetAvatarUrl() ?? target.GetDefaultAvatarUrl())
            .WithTitle(strings.AchievementGlobalTitle(guildId))
            .WithDescription(description);
    }

    /// <summary>
    ///     The leaderboard as a paginator.
    /// </summary>
    /// <param name="guild">The guild, or null for the global leaderboard.</param>
    /// <param name="user">Who may page through it.</param>
    /// <param name="sort">Ordering.</param>
    /// <param name="localeGuildId">Guild ID for localization.</param>
    /// <returns>The paginator, or null when nobody is ranked.</returns>
    public async Task<Paginator?> LeaderboardAsync(SocketGuild? guild, IUser user, AchievementLeaderboardSort sort,
        ulong localeGuildId)
    {
        const int size = 10;
        var scope = guild?.Id ?? AchievementCatalog.GlobalGuildId;
        var (first, total) = await service.GetLeaderboardAsync(scope, sort, 0, size);
        if (total == 0)
            return null;

        var title = guild is null
            ? strings.AchievementLeaderboardGlobalTitle(localeGuildId)
            : strings.AchievementLeaderboardTitle(localeGuildId, guild.Name);
        var sortLabel = sort switch
        {
            AchievementLeaderboardSort.Unlocked => strings.AchievementSortUnlocked(localeGuildId),
            AchievementLeaderboardSort.Recent => strings.AchievementSortRecent(localeGuildId),
            _ => strings.AchievementSortPoints(localeGuildId)
        };

        return new LazyPaginatorBuilder()
            .AddUser(user)
            .WithPageFactory(async index =>
            {
                var entries = index == 0 ? first : (await service.GetLeaderboardAsync(scope, sort, index, size)).Entries;
                var lines = entries.Select(e =>
                {
                    var name = guild?.GetUser(e.UserId)?.DisplayName ?? client.GetUser(e.UserId)?.Username ??
                        e.UserId.ToString();
                    var tier = AchievementCatalog.GetTier(e.Points);
                    return strings.AchievementLeaderboardLine(localeGuildId, e.Rank, name, e.Points.ToString("N0"),
                        e.Unlocked.ToString("N0"), tier.Name);
                });
                var footer = strings.AchievementLeaderboardFooter(localeGuildId, sortLabel, total.ToString("N0"),
                    index + 1, (total + size - 1) / size);
                return new PageBuilder()
                    .WithOkColor()
                    .WithTitle(title)
                    .WithDescription(string.Join("\n", lines))
                    .WithFooter(footer);
            })
            .WithFooter(PaginatorFooter.None)
            .WithMaxPageIndex(Math.Max(0, (total - 1) / size))
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();
    }

    /// <summary>
    ///     Every tracked number for a member.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="target">Whose stats.</param>
    /// <returns>The embed.</returns>
    public async Task<EmbedBuilder> StatsAsync(SocketGuild guild, SocketGuildUser target)
    {
        var metrics = AchievementCatalog.Metrics
            .Where(m => m.AllowCustom && m.Metric is not (AchievementMetric.Unlocked or AchievementMetric.Points))
            .Select(m => m.Metric)
            .ToHashSet();
        var values = (await service.ComputeMetricsAsync(guild, [target.Id], metrics)).GetValueOrDefault(target.Id) ?? [];
        var sources = await service.GetDataSourcesAsync(guild.Id);

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithAuthor(target.DisplayName, target.GetDisplayAvatarUrl())
            .WithTitle(strings.AchievementStatsTitle(guild.Id));

        foreach (var group in AchievementCatalog.Metrics.Where(m => metrics.Contains(m.Metric)).GroupBy(m => m.Source))
        {
            var lines = group.Select(m =>
            {
                var shown = values.TryGetValue(m.Metric, out var value)
                    ? m.Metric == AchievementMetric.BoostMonths && value < 0
                        ? strings.AchievementStatsNotBoosting(guild.Id)
                        : value.ToString("N0")
                    : strings.AchievementStatsNoData(guild.Id);
                return strings.AchievementStatsLine(guild.Id, m.Label, shown);
            });
            var off = sources.TryGetValue(group.Key, out var on) && !on;
            var heading = SourceName(guild.Id, group.Key);
            if (off)
                heading = strings.AchievementStatsSourceOff(guild.Id, heading);
            embed.AddField(heading, string.Join("\n", lines), true);
        }

        return embed;
    }

    /// <summary>
    ///     The display name of a data source.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="source">The source key.</param>
    /// <returns>The name.</returns>
    public string SourceName(ulong guildId, string source)
    {
        return source switch
        {
            "messages" => strings.AchievementSourceMessages(guildId),
            "voice" => strings.AchievementSourceVoice(guildId),
            "invites" => strings.AchievementSourceInvites(guildId),
            "commands" => strings.AchievementSourceCommands(guildId),
            "xp" => strings.AchievementSourceXp(guildId),
            "reputation" => strings.AchievementSourceReputation(guildId),
            "currency" => strings.AchievementSourceCurrency(guildId),
            "discord" => strings.AchievementSourceDiscord(guildId),
            _ => strings.AchievementSourceTracked(guildId)
        };
    }

    /// <summary>
    ///     Draws a member's profile card.
    /// </summary>
    /// <param name="guild">The guild, or null for the global card.</param>
    /// <param name="target">Whose card.</param>
    /// <param name="localeGuildId">Guild ID for localization.</param>
    /// <returns>The PNG.</returns>
    public async Task<MemoryStream> CardAsync(SocketGuild? guild, IUser target, ulong localeGuildId)
    {
        AchievementMemberSummary summary;
        var badges = new AchievementBadge?[AchievementCatalog.BadgeSlots];
        string scope;
        if (guild is null)
        {
            var (points, unlocked, _) = await service.GetGlobalTotalsAsync(target.Id);
            var clamped = (int)Math.Min(points, int.MaxValue);
            summary = new AchievementMemberSummary
            {
                UserId = target.Id,
                Points = clamped,
                Unlocked = (int)Math.Min(unlocked, int.MaxValue),
                Total = (int)Math.Min(unlocked, int.MaxValue),
                Tier = AchievementCatalog.GetTier(clamped),
                NextTier = AchievementCatalog.GetNextTier(clamped),
                Equipped = new string?[AchievementCatalog.BadgeSlots]
            };
            scope = strings.AchievementCardGlobalScope(localeGuildId);
        }
        else
        {
            summary = await service.GetSummaryAsync(guild.Id, target.Id);
            var owned = (await service.GetOwnedBadgesAsync(guild.Id, target.Id)).ToDictionary(b => b.Key);
            for (var i = 0; i < badges.Length; i++)
            {
                var key = summary.Equipped[i];
                badges[i] = key is not null && owned.TryGetValue(key, out var badge) ? badge : null;
            }

            if (badges.All(b => b is null))
            {
                var best = owned.Values.OrderByDescending(b => b.Grade).Take(AchievementCatalog.BadgeSlots).ToList();
                for (var i = 0; i < best.Count; i++)
                    badges[i] = best[i];
            }

            scope = guild.Name;
        }

        uint? accent = null;
        await using (var db = await dbFactory.CreateConnectionAsync())
        {
            var color = await db.DiscordUsers.Where(x => x.UserId == target.Id).Select(x => x.ProfileColor)
                .FirstOrDefaultAsync();
            if (color is > 0)
                accent = (uint)color.Value;
        }

        var progressLabel = summary.NextTier is { } next
            ? strings.AchievementCardProgress(localeGuildId, (next.MinPoints - summary.Points).ToString("N0"), next.Name)
            : strings.AchievementOverviewTopTier(localeGuildId);

        var name = target is SocketGuildUser member ? member.DisplayName : target.GlobalName ?? target.Username;
        return await renderer.RenderAsync(new AchievementCardRenderer.CardData
        {
            Name = name,
            AvatarUrl = target.GetAvatarUrl(ImageFormat.Png, 256) ?? target.GetDefaultAvatarUrl(),
            Scope = scope,
            Summary = summary,
            Badges = badges,
            Accent = accent,
            RankLabel = strings.AchievementCardRank(localeGuildId),
            PointsLabel = strings.AchievementCardPoints(localeGuildId),
            AchievementsLabel = strings.AchievementCardAchievements(localeGuildId),
            ProgressLabel = progressLabel,
            EmptySlotLabel = strings.AchievementCardEmptySlot(localeGuildId)
        });
    }

    /// <summary>
    ///     Pages listing a member's badges, with the equipped slots first.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="target">Whose badges.</param>
    /// <returns>The pages.</returns>
    public async Task<List<PageBuilder>> BadgePagesAsync(SocketGuild guild, SocketGuildUser target)
    {
        var owned = await service.GetOwnedBadgesAsync(guild.Id, target.Id);
        var summary = await service.GetSummaryAsync(guild.Id, target.Id);
        var catalog = await service.GetCatalogAsync(guild.Id);
        var allCount = AchievementService.AllBadges(catalog).Count;

        var slots = string.Join("\n", summary.Equipped.Select((key, i) =>
        {
            var badge = key is null ? null : owned.FirstOrDefault(b => b.Key == key);
            return badge is null
                ? strings.AchievementBadgeSlotEmpty(guild.Id, i + 1)
                : strings.AchievementBadgeSlot(guild.Id, i + 1, badge.Name, AchievementCatalog.GetGrade(badge.Grade).Name);
        }));

        if (owned.Count == 0)
        {
            return
            [
                new PageBuilder()
                    .WithOkColor()
                    .WithAuthor(target.DisplayName, target.GetDisplayAvatarUrl())
                    .WithTitle(strings.AchievementBadgesTitle(guild.Id, 0, allCount))
                    .WithDescription(strings.AchievementBadgesNone(guild.Id))
            ];
        }

        return owned
            .Chunk(12)
            .Select((chunk, index) => new PageBuilder()
                .WithOkColor()
                .WithAuthor(target.DisplayName, target.GetDisplayAvatarUrl())
                .WithTitle(strings.AchievementBadgesTitle(guild.Id, owned.Count, allCount))
                .WithDescription(slots + "\n\n" + string.Join("\n", chunk.Select(b =>
                    strings.AchievementBadgeLine(guild.Id, b.Name, AchievementCatalog.GetGrade(b.Grade).Name, b.Key))))
                .WithFooter(strings.AchievementBadgesFooter(guild.Id, index + 1, (owned.Count + 11) / 12)))
            .ToList();
    }

    /// <summary>
    ///     One badge: what it is and how to earn it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="badge">The badge.</param>
    /// <param name="owned">Whether the caller owns it.</param>
    /// <returns>The embed.</returns>
    public EmbedBuilder BadgePreview(ulong guildId, AchievementBadge badge, bool owned)
    {
        var grade = AchievementCatalog.GetGrade(badge.Grade);
        var title = badge.Name;
        return new EmbedBuilder()
            .WithColor(new Color(grade.Color))
            .WithTitle(title)
            .WithDescription(strings.AchievementBadgePreview(guildId, grade.Name, badge.Source,
                owned ? strings.AchievementBadgeOwned(guildId) : strings.AchievementBadgeNotOwned(guildId), badge.Key));
    }

    /// <summary>
    ///     Pages listing every achievement with its key and state, for staff.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <returns>The pages.</returns>
    public async Task<List<PageBuilder>> AdminListAsync(SocketGuild guild)
    {
        var catalog = await service.GetCatalogAsync(guild.Id);
        var counts = await service.GetUnlockCountsAsync(guild.Id);
        var lines = new List<string>();
        foreach (var category in catalog.Categories.Where(c => c.Key != AchievementCatalog.GlobalCategory))
        {
            var items = catalog.All.Where(d => d.CategoryKey == category.Key).ToList();
            if (items.Count == 0)
                continue;
            lines.Add($"**{category.Name}**");
            lines.AddRange(items.Select(d => strings.AchievementAdminLine(guild.Id, d.Name, d.Key,
                d.Enabled ? strings.AchievementAdminActive(guild.Id) : strings.AchievementAdminInactive(guild.Id),
                counts.GetValueOrDefault(d.Key))));
        }

        var settings = service.GetSettings(guild.Id);
        var status = settings.Enabled ? strings.AchievementStatusOn(guild.Id) : strings.AchievementStatusOff(guild.Id);
        return lines
            .Chunk(20)
            .Select((chunk, index) => new PageBuilder()
                .WithOkColor()
                .WithTitle(strings.AchievementAdminTitle(guild.Id, status))
                .WithDescription(string.Join("\n", chunk))
                .WithFooter(strings.AchievementAdminFooter(guild.Id, index + 1, (lines.Count + 19) / 20)))
            .ToList();
    }

    /// <summary>
    ///     Finds a category by key or name.
    /// </summary>
    /// <param name="catalog">The server's catalog.</param>
    /// <param name="input">The key or name.</param>
    /// <returns>The category, or null.</returns>
    public static AchievementCategoryInfo? FindCategory(AchievementGuildCatalog catalog, string input)
    {
        var trimmed = input.Trim();
        return catalog.Categories.FirstOrDefault(c => c.Key.Equals(trimmed, StringComparison.OrdinalIgnoreCase)) ??
               catalog.Categories.FirstOrDefault(c => c.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)) ??
               catalog.Categories.FirstOrDefault(c => c.Name.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Finds an achievement by key or name.
    /// </summary>
    /// <param name="catalog">The server's catalog.</param>
    /// <param name="input">The key or name.</param>
    /// <returns>The achievement, or null.</returns>
    public static AchievementDefinition? FindAchievement(AchievementGuildCatalog catalog, string input)
    {
        var trimmed = input.Trim();
        if (catalog.ByKey.TryGetValue(trimmed, out var byKey))
            return byKey;
        return catalog.All.FirstOrDefault(d => !d.IsGlobal && d.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)) ??
               catalog.All.FirstOrDefault(d => !d.IsGlobal && d.Name.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     A human description of why a write was refused.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="error">The error.</param>
    /// <returns>The text.</returns>
    public string DescribeError(ulong guildId, AchievementError error)
    {
        return error switch
        {
            AchievementError.NotFound => strings.AchievementErrorNotFound(guildId),
            AchievementError.NameInvalid => strings.AchievementErrorName(guildId, AchievementService.NameLength),
            AchievementError.TextTooLong => strings.AchievementErrorTooLong(guildId),
            AchievementError.MetricInvalid => strings.AchievementErrorMetric(guildId),
            AchievementError.ThresholdInvalid => strings.AchievementErrorThreshold(guildId),
            AchievementError.KeywordMissing => strings.AchievementErrorKeyword(guildId),
            AchievementError.TooManyAchievements => strings.AchievementErrorTooMany(guildId,
                AchievementService.MaxCustomAchievements),
            AchievementError.TooManyCategories => strings.AchievementErrorTooManyCategories(guildId,
                AchievementService.MaxCustomCategories),
            AchievementError.RoleNotAssignable => strings.AchievementErrorRole(guildId),
            AchievementError.CategoryInvalid => strings.AchievementErrorCategory(guildId),
            AchievementError.IconInvalid => strings.AchievementErrorIcon(guildId),
            AchievementError.TooManyUploads => strings.AchievementErrorTooManyUploads(guildId,
                AchievementIconService.MaxUploads),
            AchievementError.RewardInvalid => strings.AchievementErrorReward(guildId),
            AchievementError.AlreadyInState => strings.AchievementErrorState(guildId),
            AchievementError.BadgeInvalid => strings.AchievementErrorBadge(guildId),
            _ => strings.AchievementErrorUnknown(guildId)
        };
    }

    private static Color TierColor(AchievementMemberSummary summary)
    {
        return summary.Tier.Grade is { } grade ? new Color(AchievementCatalog.GetGrade(grade).Color) : Mewdeko.OkColor;
    }
}
