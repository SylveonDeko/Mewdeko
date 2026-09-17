using System.Globalization;
using System.Text.Json;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Controllers.Common.Analytics;
using Mewdeko.Database.DbContextStuff;

namespace Mewdeko.Services.Analytics;

public sealed partial class AnalyticsQueryService
{
    private const int MaxAnomalyCandidates = 2000;
    private const int MaxErrorRows = 20000;

    /// <summary>
    ///     Returns the most used commands with failure rate and raw p95.
    /// </summary>
    /// <param name="filter">Row filters.</param>
    /// <param name="limit">Rows to return.</param>
    public async Task<List<TopCommand>> TopCommandsAsync(CommandFilter filter, int limit)
    {
        var (where, parameters) = CommandWhere(filter);
        parameters.Add(new DataParameter("limit", Math.Clamp(limit, 1, 200), DataType.Int32));

        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var rows = await db.QueryToListAsync<TopCommandRow>(
            $"""
             SELECT "Command",
                    MIN("Module") AS "Module",
                    COUNT(*) AS "Count",
                    SUM(CASE WHEN "Ok" THEN 0 ELSE 1 END) AS "Failures",
                    COUNT(DISTINCT "GuildId") AS "Guilds",
                    AVG("DurationMs")::float8 AS "AvgMs",
                    (percentile_cont(0.95) WITHIN GROUP (ORDER BY "DurationMs"))::float8 AS "P95Ms"
             FROM "AnalyticsCommandInvocation"
             WHERE {where}
             GROUP BY "Command"
             ORDER BY "Count" DESC
             LIMIT @limit
             """, parameters.ToArray()).ConfigureAwait(false);

        return rows.Select(row => new TopCommand(row.Command, row.Module, row.Count, row.Failures,
            row.Count > 0 ? (double)row.Failures / row.Count : 0, row.AvgMs, row.P95Ms, row.Guilds)).ToList();
    }

    /// <summary>
    ///     Returns the commands that failed most, split by error class.
    /// </summary>
    /// <param name="filter">Row filters.</param>
    /// <param name="limit">Rows to return.</param>
    public async Task<List<FailingCommand>> FailingCommandsAsync(CommandFilter filter, int limit)
    {
        limit = Math.Clamp(limit, 1, 200);
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var grouped = await ApplyFilter(db.AnalyticsCommandInvocations, filter)
            .Where(x => !x.Ok && x.ErrorClass != null)
            .GroupBy(x => new
            {
                x.Command, x.ErrorClass
            })
            .Select(g => new
            {
                g.Key.Command,
                g.Key.ErrorClass,
                Count = g.LongCount(),
                LastSeen = g.Max(x => x.At),
                Module = g.Max(x => x.Module)
            })
            .OrderByDescending(x => x.Count)
            .Take(limit)
            .ToListAsync().ConfigureAwait(false);

        if (grouped.Count == 0) return [];

        var commands = grouped.Select(x => x.Command).Distinct().ToList();
        var classes = grouped.Select(x => x.ErrorClass!).Distinct().ToList();
        var latest = await ApplyFilter(db.AnalyticsCommandInvocations, filter)
            .Where(x => !x.Ok && commands.Contains(x.Command) && x.ErrorClass != null &&
                        classes.Contains(x.ErrorClass))
            .OrderByDescending(x => x.At)
            .Take(limit * 10)
            .Select(x => new
            {
                x.Command, x.ErrorClass, x.ErrorMessage
            })
            .ToListAsync().ConfigureAwait(false);

        var messages = new Dictionary<(string, string), string?>();
        foreach (var row in latest)
            messages.TryAdd((row.Command, row.ErrorClass!), row.ErrorMessage);

        return grouped.Select(row => new FailingCommand(row.Command, row.Module, row.ErrorClass!, row.Count,
            Utc(row.LastSeen), messages.GetValueOrDefault((row.Command, row.ErrorClass!)))).ToList();
    }

    /// <summary>
    ///     Returns one page of raw invocations, newest first.
    /// </summary>
    /// <param name="filter">Row filters.</param>
    /// <param name="page">1-based page.</param>
    /// <param name="pageSize">Rows per page, at most 200.</param>
    public async Task<PagedResponse<CommandInvocationItem>> InvocationsAsync(CommandFilter filter, int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var query = ApplyFilter(db.AnalyticsCommandInvocations, filter);
        var total = await query.LongCountAsync().ConfigureAwait(false);
        var rows = await query.OrderByDescending(x => x.At).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync().ConfigureAwait(false);

        var items = rows.Select(row => new CommandInvocationItem(row.Id, Utc(row.At), row.Bot, row.Shard,
            Snowflake(row.GuildId), row.GuildSize, row.Kind, row.Module, row.Command, row.Ok, row.ErrorClass,
            row.ErrorMessage, row.DurationMs, row.AckMs, row.Language)).ToList();
        return new PagedResponse<CommandInvocationItem>(items, page, pageSize, total);
    }

    /// <summary>
    ///     Returns the newest failures of one command with one error class.
    /// </summary>
    /// <param name="filter">Row filters; the command is required.</param>
    /// <param name="errorClass">The error class.</param>
    /// <param name="limit">Rows to return.</param>
    public async Task<List<CommandErrorSample>> CommandErrorSamplesAsync(CommandFilter filter, string errorClass,
        int limit)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var rows = await ApplyFilter(db.AnalyticsCommandInvocations, filter)
            .Where(x => !x.Ok && x.ErrorClass == errorClass)
            .OrderByDescending(x => x.At)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync().ConfigureAwait(false);

        return rows.Select(row => new CommandErrorSample(Utc(row.At), Snowflake(row.GuildId), row.Kind, row.Module,
            row.ErrorMessage, row.DurationMs)).ToList();
    }

    /// <summary>
    ///     Counts invocations by hour of day and guild size bucket.
    /// </summary>
    /// <param name="filter">Row filters.</param>
    public async Task<HeatmapResponse> HeatmapAsync(CommandFilter filter)
    {
        var (where, parameters) = CommandWhere(filter);
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var rows = await db.QueryToListAsync<HeatmapRow>(
            $"""
             SELECT EXTRACT(HOUR FROM "At")::int AS "Hour",
                    CASE WHEN "GuildSize" IS NULL THEN 5
                         WHEN "GuildSize" < 50 THEN 0
                         WHEN "GuildSize" < 250 THEN 1
                         WHEN "GuildSize" < 1000 THEN 2
                         WHEN "GuildSize" < 10000 THEN 3
                         ELSE 4 END AS "Size",
                    COUNT(*) AS "Count"
             FROM "AnalyticsCommandInvocation"
             WHERE {where}
             GROUP BY 1, 2
             """, parameters.ToArray()).ConfigureAwait(false);

        var cells = rows.Where(row => row.Size >= 0 && row.Size < SizeBuckets.Length)
            .Select(row => new HeatmapCell(row.Hour, SizeBuckets[row.Size], row.Count))
            .OrderBy(cell => cell.Hour)
            .ToList();
        return new HeatmapResponse(SizeBuckets.ToList(), cells);
    }

    /// <summary>
    ///     Returns the guilds with the most gateway events in range.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <param name="bot">Instance filter.</param>
    /// <param name="eventType">Event type filter.</param>
    /// <param name="limit">Rows to return.</param>
    public async Task<List<TopGuild>> TopGuildsAsync(AnalyticsRange range, string? bot, string? eventType, int limit)
    {
        limit = Math.Clamp(limit, 1, 200);
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var query = ActivityIn(db, range, bot);
        if (!string.IsNullOrEmpty(eventType)) query = query.Where(x => x.EventType == eventType);

        var top = await query.GroupBy(x => x.GuildId)
            .Select(g => new
            {
                GuildId = g.Key, Events = g.Sum(x => (long)x.Count)
            })
            .OrderByDescending(x => x.Events)
            .Take(limit)
            .ToListAsync().ConfigureAwait(false);
        if (top.Count == 0) return [];

        var ids = top.Select(x => x.GuildId).ToList();
        var types = await ActivityIn(db, range, bot)
            .Where(x => ids.Contains(x.GuildId))
            .GroupBy(x => new
            {
                x.GuildId, x.EventType
            })
            .Select(g => new
            {
                g.Key.GuildId, g.Key.EventType, Count = g.Sum(x => (long)x.Count)
            })
            .ToListAsync().ConfigureAwait(false);

        var typesByGuild = types.GroupBy(x => x.GuildId).ToDictionary(g => g.Key, g => g
            .OrderByDescending(x => x.Count).Take(5)
            .Select(x => new EventCount(x.EventType, x.Count)).ToList());

        return top.Select(row => new TopGuild(Snowflake(row.GuildId)!, GuildName(row.GuildId),
            MemberCount(row.GuildId), row.Events,
            typesByGuild.GetValueOrDefault(row.GuildId) ?? [])).ToList();
    }

    /// <summary>
    ///     Finds hours where a guild's activity for one type was three deviations above its own two week baseline.
    /// </summary>
    /// <param name="range">The range to scan.</param>
    /// <param name="bot">Instance filter.</param>
    /// <param name="minCount">Smallest hourly count worth flagging.</param>
    /// <param name="limit">Rows to return.</param>
    public async Task<List<GuildAnomaly>> AnomaliesAsync(AnalyticsRange range, string? bot, int minCount, int limit)
    {
        limit = Math.Clamp(limit, 1, 500);
        minCount = Math.Max(1, minCount);
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var candidates = await ActivityIn(db, range, bot)
            .Where(x => x.Count >= minCount)
            .OrderByDescending(x => x.Count)
            .Take(MaxAnomalyCandidates)
            .Select(x => new
            {
                x.GuildId, x.EventType, x.Hour, x.Count
            })
            .ToListAsync().ConfigureAwait(false);
        if (candidates.Count == 0) return [];

        var guildIds = candidates.Select(x => x.GuildId).Distinct().Take(500).ToList();
        var eventTypes = candidates.Select(x => x.EventType).Distinct().ToList();
        var baselineStart = AnalyticsCollector.HourOf(range.From).AddDays(-14);

        var historyQuery = db.AnalyticsGuildActivities
            .Where(x => x.Hour >= baselineStart && x.Hour < range.To && guildIds.Contains(x.GuildId) &&
                        eventTypes.Contains(x.EventType));
        if (!string.IsNullOrEmpty(bot)) historyQuery = historyQuery.Where(x => x.Bot == bot);
        var history = await historyQuery
            .Select(x => new
            {
                x.GuildId, x.EventType, x.Hour, x.Count
            })
            .ToListAsync().ConfigureAwait(false);

        var seen = history.GroupBy(x => (x.GuildId, x.EventType))
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Hour, x => (long)x.Count));

        var flagged = new List<GuildAnomaly>();
        foreach (var candidate in candidates)
        {
            if (!seen.TryGetValue((candidate.GuildId, candidate.EventType), out var hours)) continue;

            var samples = new double[14];
            for (var day = 1; day <= 14; day++)
                samples[day - 1] = hours.GetValueOrDefault(candidate.Hour.AddDays(-day));

            var mean = samples.Average();
            var variance = samples.Sum(s => (s - mean) * (s - mean)) / samples.Length;
            var stddev = Math.Sqrt(variance);
            var z = (candidate.Count - mean) / Math.Max(stddev, 1);
            if (z < 3) continue;

            flagged.Add(new GuildAnomaly(Snowflake(candidate.GuildId)!, GuildName(candidate.GuildId),
                candidate.EventType, Utc(candidate.Hour), candidate.Count, Math.Round(mean, 2),
                Math.Round(stddev, 2), Math.Round(z, 2)));
        }

        return flagged.OrderByDescending(x => x.Z).Take(limit).ToList();
    }

    /// <summary>
    ///     Returns one guild's hourly activity by event type.
    /// </summary>
    /// <param name="guildId">The guild.</param>
    /// <param name="range">The range.</param>
    /// <param name="bot">Instance filter.</param>
    public async Task<GuildTimelineResponse> GuildTimelineAsync(ulong guildId, AnalyticsRange range, string? bot)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var rows = await ActivityIn(db, range, bot)
            .Where(x => x.GuildId == guildId)
            .Select(x => new
            {
                x.Hour, x.EventType, x.Count
            })
            .ToListAsync().ConfigureAwait(false);

        var types = rows.GroupBy(x => x.EventType)
            .OrderByDescending(g => g.Sum(x => (long)x.Count))
            .Select(g => g.Key)
            .ToList();
        var cells = rows.GroupBy(x => (x.Hour, x.EventType))
            .Select(g => new GuildTimelineCell(Utc(g.Key.Hour), g.Key.EventType, g.Sum(x => (long)x.Count)))
            .OrderBy(cell => cell.Hour)
            .ToList();
        return new GuildTimelineResponse(Snowflake(guildId)!, types, cells);
    }

    /// <summary>
    ///     Returns one page of a guild's security relevant events, newest first.
    /// </summary>
    /// <param name="guildId">The guild.</param>
    /// <param name="range">The range.</param>
    /// <param name="eventType">Event type filter.</param>
    /// <param name="page">1-based page.</param>
    /// <param name="pageSize">Rows per page, at most 200.</param>
    public async Task<PagedResponse<GuildEventItem>> GuildEventsAsync(ulong guildId, AnalyticsRange range,
        string? eventType, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var query = db.AnalyticsGuildEventLogs
            .Where(x => x.GuildId == guildId && x.At >= range.From && x.At < range.To);
        if (!string.IsNullOrEmpty(eventType)) query = query.Where(x => x.EventType == eventType);

        var total = await query.LongCountAsync().ConfigureAwait(false);
        var rows = await query.OrderByDescending(x => x.At).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync().ConfigureAwait(false);

        var items = rows.Select(row => new GuildEventItem(row.Id, Utc(row.At), row.EventType, row.Bot, row.Shard))
            .ToList();
        return new PagedResponse<GuildEventItem>(items, page, pageSize, total);
    }

    /// <summary>
    ///     Builds the summary card for one guild.
    /// </summary>
    /// <param name="guildId">The guild.</param>
    /// <param name="range">The range for commands, events and features.</param>
    public async Task<GuildCard> GuildCardAsync(ulong guildId, AnalyticsRange range)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var guild = client.GetGuild(guildId);
        var joinedAt = guild?.CurrentUser?.JoinedAt?.UtcDateTime;
        joinedAt ??= Utc(await db.GuildConfigs.Where(x => x.GuildId == guildId)
            .Select(x => x.DateAdded).FirstOrDefaultAsync().ConfigureAwait(false));

        var commands = await db.AnalyticsCommandInvocations
            .Where(x => x.GuildId == guildId && x.At >= range.From && x.At < range.To)
            .LongCountAsync().ConfigureAwait(false);
        var events = await db.AnalyticsGuildActivities
            .Where(x => x.GuildId == guildId && x.Hour >= AnalyticsCollector.HourOf(range.From) && x.Hour < range.To)
            .SumAsync(x => (long?)x.Count).ConfigureAwait(false) ?? 0;
        var features = await db.AnalyticsFeatureActivities
            .Where(x => x.GuildId == guildId && x.HourUtc >= AnalyticsCollector.HourOf(range.From) &&
                        x.HourUtc < range.To)
            .GroupBy(x => x.Feature)
            .Select(g => new FeatureUse(g.Key, g.Sum(x => (long)x.Count), g.Sum(x => (long)x.Errors)))
            .ToListAsync().ConfigureAwait(false);
        var topCommands = await db.AnalyticsCommandInvocations
            .Where(x => x.GuildId == guildId && x.At >= range.From && x.At < range.To)
            .GroupBy(x => x.Command)
            .Select(g => new CommandUse(g.Key, g.LongCount(), g.LongCount(x => !x.Ok)))
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToListAsync().ConfigureAwait(false);

        var sets = await GetFeatureSetsAsync(db).ConfigureAwait(false);
        var configured = sets.Configured.GetValueOrDefault(guildId)?.Order(StringComparer.Ordinal).ToList() ?? [];
        var enabled = sets.Enabled.GetValueOrDefault(guildId)?.Order(StringComparer.Ordinal).ToList() ?? [];

        return new GuildCard(Snowflake(guildId)!, guild?.Name, guild?.MemberCount,
            guild is null ? null : client.GetShardIdFor(guild), joinedAt, guild is not null, commands, events,
            features.OrderByDescending(f => f.Count).ToList(), guild is null ? null : Shape(guild), configured,
            enabled, topCommands);
    }

    /// <summary>
    ///     Returns per feature adoption: active guilds and use in range plus configured and enabled census counts.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <param name="bot">Instance filter.</param>
    public async Task<List<FeatureAdoption>> FeatureAdoptionAsync(AnalyticsRange range, string? bot)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var activity = await FeatureActivityByGuildAsync(db, range, bot).ConfigureAwait(false);
        var census = await LatestCensusAsync(db, "feature.").ConfigureAwait(false);

        var byFeature = activity.GroupBy(x => x.Feature).ToDictionary(g => g.Key, g => new
        {
            Guilds = g.LongCount(), Count = g.Sum(x => x.Count), Errors = g.Sum(x => x.Errors)
        });

        var features = new HashSet<string>(byFeature.Keys, StringComparer.Ordinal);
        foreach (var metric in census.Keys)
        {
            var parts = metric.Split('.');
            if (parts.Length >= 3) features.Add(parts[1]);
        }

        return features.Select(feature =>
            {
                var use = byFeature.GetValueOrDefault(feature);
                return new FeatureAdoption(feature, use?.Guilds ?? 0, use?.Count ?? 0, use?.Errors ?? 0,
                    census.TryGetValue($"feature.{feature}.configured", out var configured) ? configured : null,
                    census.TryGetValue($"feature.{feature}.enabled", out var enabled) ? enabled : null);
            })
            .OrderByDescending(x => x.ActiveGuilds).ThenBy(x => x.Feature)
            .ToList();
    }

    /// <summary>
    ///     Returns how many features each guild used in range, as a histogram and as size versus count points.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <param name="bot">Instance filter.</param>
    public async Task<FeatureDepthResponse> FeatureDepthAsync(AnalyticsRange range, string? bot)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var activity = await FeatureActivityByGuildAsync(db, range, bot).ConfigureAwait(false);

        var perGuild = activity.GroupBy(x => x.GuildId)
            .Select(g => new
            {
                GuildId = g.Key, Features = g.Select(x => x.Feature).Distinct().Count()
            })
            .ToList();

        var histogram = perGuild.GroupBy(x => x.Features)
            .Select(g => new FeatureDepthBucket(g.Key, g.LongCount()))
            .OrderBy(x => x.Features)
            .ToList();
        var points = perGuild
            .Select(x => new FeatureDepthPoint(Snowflake(x.GuildId)!, MemberCount(x.GuildId), x.Features))
            .OrderByDescending(x => x.MemberCount ?? -1)
            .Take(500)
            .ToList();
        return new FeatureDepthResponse(histogram, points);
    }

    /// <summary>
    ///     Returns the latest settings census rows, optionally only those whose table matches a feature key.
    /// </summary>
    /// <param name="feature">Feature key to filter on, or null for all.</param>
    public async Task<List<SettingUsage>> SettingsUsageAsync(string? feature)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var census = await LatestCensusAsync(db, "setting.").ConfigureAwait(false);
        var needle = feature?.Replace("_", string.Empty) ?? string.Empty;

        var rows = new List<SettingUsage>();
        foreach (var (metric, value) in census)
        {
            var parts = metric.Split('.', 4);
            if (parts.Length < 4) continue;
            var table = parts[1];
            if (needle.Length > 0 &&
                !table.Replace("_", string.Empty).Contains(needle, StringComparison.OrdinalIgnoreCase))
                continue;
            rows.Add(new SettingUsage(metric, table, parts[2], parts[3], value));
        }

        return rows.OrderBy(x => x.Table).ThenBy(x => x.Column).ThenByDescending(x => x.Count).Take(2000).ToList();
    }

    /// <summary>
    ///     Returns one census metric over the last N days.
    /// </summary>
    /// <param name="metric">The census metric name.</param>
    /// <param name="days">How many days back.</param>
    public async Task<List<DayValue>> CensusSeriesAsync(string metric, int days)
    {
        days = Math.Clamp(days, 1, 400);
        var since = DateTime.UtcNow.Date.AddDays(-days);
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var rows = await db.AnalyticsConfigCensuses
            .Where(x => x.Metric == metric && x.Day >= since)
            .OrderBy(x => x.Day)
            .ToListAsync().ConfigureAwait(false);
        return rows.Select(row => new DayValue(Utc(row.Day), row.Value)).ToList();
    }

    /// <summary>
    ///     Returns the nightly snapshots for the last N days.
    /// </summary>
    /// <param name="days">How many days back.</param>
    /// <param name="bot">Instance filter.</param>
    public async Task<List<SnapshotItem>> SnapshotsAsync(int days, string? bot)
    {
        days = Math.Clamp(days, 1, 400);
        var since = DateTime.UtcNow.Date.AddDays(-days);
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var query = db.AnalyticsDailySnapshots.Where(x => x.Day >= since);
        if (!string.IsNullOrEmpty(bot)) query = query.Where(x => x.Bot == bot);
        var rows = await query.OrderBy(x => x.Day).ThenBy(x => x.Bot).ToListAsync().ConfigureAwait(false);

        return rows.Select(row => new SnapshotItem(Utc(row.Day), row.Bot, row.Guilds, row.Users,
            ParseFeatures(row.FeaturesJson))).ToList();
    }

    /// <summary>
    ///     Summarises joins, leaves and bounces over the range.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <param name="filters">Exact label matches for the join and leave counters.</param>
    public async Task<ChurnResponse> ChurnAsync(AnalyticsRange range, IReadOnlyDictionary<string, string> filters)
    {
        var joinsBySize = await BreakdownAsync("guild.join", "size", range, filters, "sum", 10).ConfigureAwait(false);
        var leavesBySize = await BreakdownAsync("guild.leave", "size", range, filters, "sum", 10).ConfigureAwait(false);
        var joins = joinsBySize.Sum(x => x.Value);
        var leaves = leavesBySize.Sum(x => x.Value);

        var joinSeries = await SeriesAsync(new SeriesQuery("guild.join", range, filters, null, "sum", 1, 60))
            .ConfigureAwait(false);
        var leaveSeries = await SeriesAsync(new SeriesQuery("guild.leave", range, filters, null, "sum", 1, 60))
            .ConfigureAwait(false);
        var days = new SortedDictionary<DateTime, (double Joins, double Leaves)>();
        foreach (var point in joinSeries.Series.SelectMany(s => s.Points))
        {
            var day = DateTimeOffset.FromUnixTimeSeconds(point.BucketUnix).UtcDateTime.Date;
            var current = days.GetValueOrDefault(day);
            days[day] = (current.Joins + (point.Value ?? 0), current.Leaves);
        }

        foreach (var point in leaveSeries.Series.SelectMany(s => s.Points))
        {
            var day = DateTimeOffset.FromUnixTimeSeconds(point.BucketUnix).UtcDateTime.Date;
            var current = days.GetValueOrDefault(day);
            days[day] = (current.Joins, current.Leaves + (point.Value ?? 0));
        }

        var bounced = await BouncedGuildsAsync(range, filters.GetValueOrDefault("bot"), int.MaxValue)
            .ConfigureAwait(false);

        return new ChurnResponse(joins, leaves, joins - leaves, bounced.Count,
            joins > 0 ? bounced.Count / joins : null, joinsBySize, leavesBySize,
            days.Select(pair => new ChurnDay(Utc(pair.Key), pair.Value.Joins, pair.Value.Leaves)).ToList());
    }

    /// <summary>
    ///     For each of the last N days, how many guilds joined and how many of those the bot is still in.
    /// </summary>
    /// <param name="days">How many days back.</param>
    /// <param name="filters">Exact label matches for the join counter.</param>
    public async Task<List<RetentionPoint>> RetentionAsync(int days, IReadOnlyDictionary<string, string> filters)
    {
        days = Math.Clamp(days, 1, 90);
        var today = DateTime.UtcNow.Date;
        var range = new AnalyticsRange(today.AddDays(-days), today.AddDays(1));
        var joinSeries = await SeriesAsync(new SeriesQuery("guild.join", range, filters, null, "sum", 1, 60))
            .ConfigureAwait(false);

        var joined = new Dictionary<DateTime, double>();
        foreach (var point in joinSeries.Series.SelectMany(s => s.Points))
        {
            var day = DateTimeOffset.FromUnixTimeSeconds(point.BucketUnix).UtcDateTime.Date;
            joined[day] = joined.GetValueOrDefault(day) + (point.Value ?? 0);
        }

        var retained = new Dictionary<DateTime, long>();
        foreach (var guild in client.Guilds)
        {
            var joinedAt = guild.CurrentUser?.JoinedAt?.UtcDateTime.Date;
            if (joinedAt is null || joinedAt < range.From) continue;
            retained[joinedAt.Value] = retained.GetValueOrDefault(joinedAt.Value) + 1;
        }

        var points = new List<RetentionPoint>(days);
        for (var day = range.From; day < today; day = day.AddDays(1))
        {
            var joins = joined.GetValueOrDefault(day);
            var kept = retained.GetValueOrDefault(day);
            points.Add(new RetentionPoint(Utc(day), joins, kept, joins > 0 ? Math.Min(1, kept / joins) : null));
        }

        return points;
    }

    /// <summary>
    ///     Returns guilds whose activity started in range, lasted at most a day, and that the bot is no longer in.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <param name="bot">Instance filter.</param>
    /// <param name="limit">Rows to return.</param>
    public async Task<List<BouncedGuild>> BouncedGuildsAsync(AnalyticsRange range, string? bot, int limit)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var from = AnalyticsCollector.HourOf(range.From);

        var grouped = await ActivityIn(db, range, bot)
            .GroupBy(x => x.GuildId)
            .Select(g => new
            {
                GuildId = g.Key,
                First = g.Min(x => x.Hour),
                Last = g.Max(x => x.Hour),
                Events = g.Sum(x => (long)x.Count)
            })
            .ToListAsync().ConfigureAwait(false);
        if (grouped.Count == 0) return [];

        var candidates = grouped
            .Where(x => x.Last - x.First <= TimeSpan.FromHours(24) && client.GetGuild(x.GuildId) is null)
            .ToList();
        if (candidates.Count == 0) return [];

        var ids = candidates.Select(x => x.GuildId).Take(2000).ToList();
        var earlierStart = from.AddDays(-7);
        var earlierQuery = db.AnalyticsGuildActivities
            .Where(x => x.Hour >= earlierStart && x.Hour < from && ids.Contains(x.GuildId));
        if (!string.IsNullOrEmpty(bot)) earlierQuery = earlierQuery.Where(x => x.Bot == bot);
        var earlier = (await earlierQuery.Select(x => x.GuildId).Distinct().ToListAsync().ConfigureAwait(false))
            .ToHashSet();

        return candidates.Where(x => !earlier.Contains(x.GuildId))
            .OrderByDescending(x => x.Last)
            .Take(Math.Max(1, limit))
            .Select(x => new BouncedGuild(Snowflake(x.GuildId)!, Utc(x.First), Utc(x.Last), x.Events))
            .ToList();
    }

    /// <summary>
    ///     Returns guilds that were active in the week before last but not in the last week and are still present.
    /// </summary>
    /// <param name="bot">Instance filter.</param>
    /// <param name="limit">Rows to return.</param>
    public async Task<List<SilentGuild>> SilentGuildsAsync(string? bot, int limit)
    {
        var now = AnalyticsCollector.HourOf(DateTime.UtcNow);
        var weekAgo = now.AddDays(-7);
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var previous = await ActivityIn(db, new AnalyticsRange(weekAgo.AddDays(-7), weekAgo), bot)
            .GroupBy(x => x.GuildId)
            .Select(g => new
            {
                GuildId = g.Key, Last = g.Max(x => x.Hour), Events = g.Sum(x => (long)x.Count)
            })
            .ToListAsync().ConfigureAwait(false);
        if (previous.Count == 0) return [];

        var recent = (await ActivityIn(db, new AnalyticsRange(weekAgo, now.AddHours(1)), bot)
            .Select(x => x.GuildId).Distinct().ToListAsync().ConfigureAwait(false)).ToHashSet();

        return previous.Where(x => !recent.Contains(x.GuildId) && client.GetGuild(x.GuildId) is not null)
            .OrderByDescending(x => x.Events)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(x => new SilentGuild(Snowflake(x.GuildId)!, GuildName(x.GuildId), MemberCount(x.GuildId),
                Utc(x.Last), x.Events))
            .ToList();
    }

    /// <summary>
    ///     Groups error samples in range by type, module and message.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <param name="bot">Instance filter.</param>
    /// <param name="limit">Rows to return.</param>
    public async Task<List<ErrorGroup>> ErrorGroupsAsync(AnalyticsRange range, string? bot, int limit)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var rows = await ErrorsIn(db, range, bot)
            .OrderByDescending(x => x.LastSeen)
            .Take(MaxErrorRows)
            .ToListAsync().ConfigureAwait(false);

        return rows.GroupBy(x => (x.Type, Module: x.Module ?? string.Empty, x.MessageHash))
            .Select(g =>
            {
                var latest = g.MaxBy(x => x.LastSeen)!;
                return new ErrorGroup(g.Key.Type, latest.Module, latest.Location, g.Key.MessageHash,
                    g.Sum(x => (long)x.Count), Utc(g.Min(x => x.FirstSeen)), Utc(latest.LastSeen), latest.Message);
            })
            .OrderByDescending(x => x.Count)
            .Take(Math.Clamp(limit, 1, 500))
            .ToList();
    }

    /// <summary>
    ///     Returns the hourly samples of one error, newest first.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <param name="bot">Instance filter.</param>
    /// <param name="type">Exception type.</param>
    /// <param name="module">Module, or null for any.</param>
    /// <param name="hash">Message hash, or null for any.</param>
    /// <param name="limit">Rows to return.</param>
    public async Task<List<ErrorSampleItem>> ErrorSamplesAsync(AnalyticsRange range, string? bot, string type,
        string? module, string? hash, int limit)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var query = ErrorsIn(db, range, bot).Where(x => x.Type == type);
        if (!string.IsNullOrEmpty(module)) query = query.Where(x => x.Module == module);
        if (!string.IsNullOrEmpty(hash)) query = query.Where(x => x.MessageHash == hash);

        var rows = await query.OrderByDescending(x => x.LastSeen)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync().ConfigureAwait(false);
        return rows.Select(row => new ErrorSampleItem(Utc(row.Hour), row.Bot, row.Shard, row.Location, row.Message,
            row.Count, Utc(row.FirstSeen), Utc(row.LastSeen), Snowflake(row.LastGuildId))).ToList();
    }

    /// <summary>
    ///     Summarises AI requests, tokens, latency and estimated cost per model.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <param name="filters">Exact label matches.</param>
    public async Task<AiSummaryResponse> AiSummaryAsync(AnalyticsRange range,
        IReadOnlyDictionary<string, string> filters)
    {
        var resolution = ResolveResolution(range, null);
        var from = Align(range.From, TimeSpan.FromMinutes(resolution));
        var requests = await LoadBucketsAsync("ai.requests", resolution, from, range.To).ConfigureAwait(false);
        var tokens = await LoadBucketsAsync("ai.tokens", resolution, from, range.To).ConfigureAwait(false);
        var durations = await LoadBucketsAsync("ai.duration", resolution, from, range.To).ConfigureAwait(false);

        var models = new Dictionary<string, AiAccumulator>(StringComparer.Ordinal);

        AiAccumulator For(string model)
        {
            if (!models.TryGetValue(model, out var accumulator))
            {
                accumulator = new AiAccumulator();
                models[model] = accumulator;
            }

            return accumulator;
        }

        foreach (var row in requests)
        {
            var labels = AnalyticsCollector.ParseLabels(row.Labels);
            if (!Matches(labels, filters)) continue;
            var accumulator = For(labels.GetValueOrDefault("model", "unknown"));
            accumulator.Requests += row.Sum;
            if (labels.GetValueOrDefault("ok") == "0") accumulator.Failures += row.Sum;
            if (labels.TryGetValue("provider", out var provider) && provider.Length > 0)
                accumulator.Provider = provider;
        }

        foreach (var row in tokens)
        {
            var labels = AnalyticsCollector.ParseLabels(row.Labels);
            if (!Matches(labels, filters)) continue;
            var accumulator = For(labels.GetValueOrDefault("model", "unknown"));
            if (labels.GetValueOrDefault("dir") == "out") accumulator.TokensOut += row.Sum;
            else accumulator.TokensIn += row.Sum;
        }

        foreach (var row in durations)
        {
            var labels = AnalyticsCollector.ParseLabels(row.Labels);
            if (!Matches(labels, filters)) continue;
            For(labels.GetValueOrDefault("model", "unknown")).Duration.Add(row);
        }

        var items = models.Select(pair => new AiModelSummary(pair.Key, pair.Value.Provider, pair.Value.Requests,
                pair.Value.Failures, pair.Value.TokensIn, pair.Value.TokensOut,
                pair.Value.Duration.Value("p95", range.Span.TotalSeconds),
                AiPriceTable.Estimate(pair.Key, pair.Value.TokensIn, pair.Value.TokensOut)))
            .OrderByDescending(x => x.Requests)
            .ToList();

        var priced = items.Where(x => x.CostUsd.HasValue).ToList();
        return new AiSummaryResponse(items, items.Sum(x => x.Requests), items.Sum(x => x.TokensIn),
            items.Sum(x => x.TokensOut), priced.Count == 0 ? null : priced.Sum(x => x.CostUsd!.Value));
    }

    /// <summary>
    ///     Returns the guilds that used AI chat the most in range.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <param name="bot">Instance filter.</param>
    /// <param name="limit">Rows to return.</param>
    public async Task<List<AiGuild>> AiGuildsAsync(AnalyticsRange range, string? bot, int limit)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var query = db.AnalyticsFeatureActivities
            .Where(x => x.Feature == "ai_chat" && x.HourUtc >= AnalyticsCollector.HourOf(range.From) &&
                        x.HourUtc < range.To);
        if (!string.IsNullOrEmpty(bot)) query = query.Where(x => x.Bot == bot);

        var rows = await query.GroupBy(x => x.GuildId)
            .Select(g => new
            {
                GuildId = g.Key, Count = g.Sum(x => (long)x.Count), Errors = g.Sum(x => (long)x.Errors)
            })
            .OrderByDescending(x => x.Count)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync().ConfigureAwait(false);

        return rows.Select(row => new AiGuild(Snowflake(row.GuildId)!, GuildName(row.GuildId), row.Count, row.Errors))
            .ToList();
    }

    /// <summary>
    ///     Returns the busiest dashboard routes with visitors, p95 and 5xx counts.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <param name="limit">Rows to return.</param>
    public async Task<List<RouteStats>> TopRoutesAsync(AnalyticsRange range, int limit)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var rows = await db.QueryToListAsync<RouteRow>(
            """
            SELECT "Route",
                   COUNT(*) AS "Views",
                   COUNT(DISTINCT "VisitorHash") AS "Visitors",
                   (percentile_cont(0.95) WITHIN GROUP (ORDER BY "DurationMs"))::float8 AS "P95Ms",
                   SUM(CASE WHEN "Status" >= 500 THEN 1 ELSE 0 END) AS "Errors"
            FROM "AnalyticsPageView"
            WHERE "At" >= @from AND "At" < @to
            GROUP BY "Route"
            ORDER BY "Views" DESC
            LIMIT @limit
            """,
            new DataParameter("from", range.From, DataType.DateTime),
            new DataParameter("to", range.To, DataType.DateTime),
            new DataParameter("limit", Math.Clamp(limit, 1, 500), DataType.Int32)).ConfigureAwait(false);

        return rows.Select(row => new RouteStats(row.Route, row.Views, row.Visitors, row.P95Ms, row.Errors)).ToList();
    }

    /// <summary>
    ///     Returns route and status pairs that returned 5xx in range.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <param name="limit">Rows to return.</param>
    public async Task<List<ErrorRoute>> ErrorRoutesAsync(AnalyticsRange range, int limit)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var rows = await db.AnalyticsPageViews
            .Where(x => x.At >= range.From && x.At < range.To && x.Status >= 500)
            .GroupBy(x => new
            {
                x.Route, x.Status
            })
            .Select(g => new
            {
                g.Key.Route, g.Key.Status, Count = g.LongCount(), LastSeen = g.Max(x => x.At)
            })
            .OrderByDescending(x => x.Count)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync().ConfigureAwait(false);

        return rows.Select(row => new ErrorRoute(row.Route, row.Status, row.Count, Utc(row.LastSeen))).ToList();
    }

    /// <summary>
    ///     Counts views and visitors of the login route, the OAuth callback and the dashboard.
    /// </summary>
    /// <param name="range">The range.</param>
    public async Task<FunnelResponse> LoginFunnelAsync(AnalyticsRange range)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var views = db.AnalyticsPageViews.Where(x => x.At >= range.From && x.At < range.To);

        var login = views.Where(x => x.Route.Contains("login"));
        var callback = views.Where(x => x.Route.Contains("callback"));
        var dashboard = views.Where(x => x.Route.StartsWith("/dashboard"));

        return new FunnelResponse(
            await login.LongCountAsync().ConfigureAwait(false),
            await login.Select(x => x.VisitorHash).Distinct().LongCountAsync().ConfigureAwait(false),
            await callback.LongCountAsync().ConfigureAwait(false),
            await callback.Select(x => x.VisitorHash).Distinct().LongCountAsync().ConfigureAwait(false),
            await dashboard.LongCountAsync().ConfigureAwait(false),
            await dashboard.Select(x => x.VisitorHash).Distinct().LongCountAsync().ConfigureAwait(false));
    }

    private static IQueryable<AnalyticsCommandInvocation> ApplyFilter(IQueryable<AnalyticsCommandInvocation> query,
        CommandFilter filter)
    {
        query = query.Where(x => x.At >= filter.Range.From && x.At < filter.Range.To);
        if (!string.IsNullOrEmpty(filter.Bot)) query = query.Where(x => x.Bot == filter.Bot);
        if (filter.Shard is { } shard) query = query.Where(x => x.Shard == shard);
        if (!string.IsNullOrEmpty(filter.Kind)) query = query.Where(x => x.Kind == filter.Kind);
        if (!string.IsNullOrEmpty(filter.Module)) query = query.Where(x => x.Module == filter.Module);
        if (!string.IsNullOrEmpty(filter.Command)) query = query.Where(x => x.Command == filter.Command);
        if (filter.GuildId is { } guildId) query = query.Where(x => x.GuildId == guildId);
        if (filter.Ok is { } ok) query = query.Where(x => x.Ok == ok);
        return query;
    }

    private static (string Where, List<DataParameter> Parameters) CommandWhere(CommandFilter filter)
    {
        var clauses = new List<string>
        {
            "\"At\" >= @from", "\"At\" < @to"
        };
        var parameters = new List<DataParameter>
        {
            new("from", filter.Range.From, DataType.DateTime), new("to", filter.Range.To, DataType.DateTime)
        };

        if (!string.IsNullOrEmpty(filter.Bot))
        {
            clauses.Add("\"Bot\" = @bot");
            parameters.Add(new DataParameter("bot", filter.Bot, DataType.Text));
        }

        if (filter.Shard is { } shard)
        {
            clauses.Add("\"Shard\" = @shard");
            parameters.Add(new DataParameter("shard", shard, DataType.Int32));
        }

        if (!string.IsNullOrEmpty(filter.Kind))
        {
            clauses.Add("\"Kind\" = @kind");
            parameters.Add(new DataParameter("kind", filter.Kind, DataType.Text));
        }

        if (!string.IsNullOrEmpty(filter.Module))
        {
            clauses.Add("\"Module\" = @module");
            parameters.Add(new DataParameter("module", filter.Module, DataType.Text));
        }

        if (!string.IsNullOrEmpty(filter.Command))
        {
            clauses.Add("\"Command\" = @command");
            parameters.Add(new DataParameter("command", filter.Command, DataType.Text));
        }

        if (filter.GuildId is { } guildId)
        {
            clauses.Add("\"GuildId\" = @guild");
            parameters.Add(new DataParameter("guild", (decimal)guildId, DataType.Decimal));
        }

        if (filter.Ok is { } ok)
        {
            clauses.Add("\"Ok\" = @ok");
            parameters.Add(new DataParameter("ok", ok, DataType.Boolean));
        }

        return (string.Join(" AND ", clauses), parameters);
    }

    private static IQueryable<AnalyticsCommandInvocation> CommandsIn(MewdekoDb db, AnalyticsRange range,
        string? bot)
    {
        var query = db.AnalyticsCommandInvocations.Where(x => x.At >= range.From && x.At < range.To);
        if (!string.IsNullOrEmpty(bot)) query = query.Where(x => x.Bot == bot);
        return query;
    }

    private static IQueryable<AnalyticsGuildActivity> ActivityIn(MewdekoDb db, AnalyticsRange range, string? bot)
    {
        var from = AnalyticsCollector.HourOf(range.From);
        var query = db.AnalyticsGuildActivities.Where(x => x.Hour >= from && x.Hour < range.To);
        if (!string.IsNullOrEmpty(bot)) query = query.Where(x => x.Bot == bot);
        return query;
    }

    private static IQueryable<AnalyticsErrorSample> ErrorsIn(MewdekoDb db, AnalyticsRange range, string? bot)
    {
        var query = db.AnalyticsErrorSamples.Where(x => x.LastSeen >= range.From && x.Hour < range.To);
        if (!string.IsNullOrEmpty(bot)) query = query.Where(x => x.Bot == bot);
        return query;
    }

    private static async Task<List<FeatureGuildUse>> FeatureActivityByGuildAsync(MewdekoDb db, AnalyticsRange range,
        string? bot)
    {
        var from = AnalyticsCollector.HourOf(range.From);
        var query = db.AnalyticsFeatureActivities.Where(x => x.HourUtc >= from && x.HourUtc < range.To);
        if (!string.IsNullOrEmpty(bot)) query = query.Where(x => x.Bot == bot);

        return await query.GroupBy(x => new
            {
                x.Feature, x.GuildId
            })
            .Select(g => new FeatureGuildUse(g.Key.Feature, g.Key.GuildId, g.Sum(x => (long)x.Count),
                g.Sum(x => (long)x.Errors)))
            .Take(MaxRowsPerQuery)
            .ToListAsync().ConfigureAwait(false);
    }

    private static async Task<Dictionary<string, double>> LatestCensusAsync(MewdekoDb db, string prefix)
    {
        var latest = await db.AnalyticsConfigCensuses
            .Where(x => x.Metric.StartsWith(prefix))
            .MaxAsync(x => (DateTime?)x.Day).ConfigureAwait(false);
        if (latest is null) return new Dictionary<string, double>(StringComparer.Ordinal);

        var rows = await db.AnalyticsConfigCensuses
            .Where(x => x.Day == latest.Value && x.Metric.StartsWith(prefix))
            .ToListAsync().ConfigureAwait(false);
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var row in rows) result[row.Metric] = row.Value;
        return result;
    }

    private static Dictionary<string, double>? ParseFeatures(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, double>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Snowflake(ulong? id)
    {
        return id?.ToString(CultureInfo.InvariantCulture);
    }

    private string? GuildName(ulong guildId)
    {
        return client.GetGuild(guildId)?.Name;
    }

    private int? MemberCount(ulong guildId)
    {
        return client.GetGuild(guildId)?.MemberCount;
    }

    private sealed record FeatureGuildUse(string Feature, ulong GuildId, long Count, long Errors);

    private sealed class AiAccumulator
    {
        public string? Provider { get; set; }
        public double Requests { get; set; }
        public double Failures { get; set; }
        public double TokensIn { get; set; }
        public double TokensOut { get; set; }
        public Accumulator Duration { get; } = new();
    }

    private sealed class TopCommandRow
    {
        public string Command { get; set; } = string.Empty;
        public string? Module { get; set; }
        public long Count { get; set; }
        public long Failures { get; set; }
        public long Guilds { get; set; }
        public double? AvgMs { get; set; }
        public double? P95Ms { get; set; }
    }

    private sealed class HeatmapRow
    {
        public int Hour { get; set; }
        public int Size { get; set; }
        public long Count { get; set; }
    }

    private sealed class RouteRow
    {
        public string Route { get; set; } = string.Empty;
        public long Views { get; set; }
        public long Visitors { get; set; }
        public double? P95Ms { get; set; }
        public long Errors { get; set; }
    }
}