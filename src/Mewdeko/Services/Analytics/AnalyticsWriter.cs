using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Settings;

namespace Mewdeko.Services.Analytics;

/// <summary>
///     Writes what the collector gathers into Postgres, folds minute buckets into coarser ones and prunes old rows.
/// </summary>
public sealed class AnalyticsWriter : INService, IReadyExecutor, IDisposable
{
    private static readonly TimeSpan RawFlushInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan BucketFlushInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RollupInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromHours(6);

    private readonly AnalyticsCollector collector;
    private readonly BotConfigService config;
    private readonly IDataConnectionFactory dbFactory;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ILogger<AnalyticsWriter> logger;
    private readonly Dictionary<string, MetricRegistryEntry> registry = new(StringComparer.Ordinal);
    private Timer? bucketTimer;
    private bool disposed;
    private Timer? maintenanceTimer;

    private Timer? rawTimer;
    private Timer? rollupTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AnalyticsWriter" /> class.
    /// </summary>
    /// <param name="collector">The collector to drain.</param>
    /// <param name="config">The bot config.</param>
    /// <param name="dbFactory">Creates database connections.</param>
    /// <param name="logger">Records write failures.</param>
    public AnalyticsWriter(AnalyticsCollector collector, BotConfigService config, IDataConnectionFactory dbFactory,
        ILogger<AnalyticsWriter> logger)
    {
        this.collector = collector;
        this.config = config;
        this.dbFactory = dbFactory;
        this.logger = logger;
    }

    /// <summary>
    ///     When the last successful flush finished.
    /// </summary>
    public DateTime? LastFlushAt { get; private set; }

    /// <summary>
    ///     When the last successful rollup finished.
    /// </summary>
    public DateTime? LastRollupAt { get; private set; }

    /// <summary>
    ///     When the last successful retention pass finished.
    /// </summary>
    public DateTime? LastMaintenanceAt { get; private set; }

    /// <summary>
    ///     The last write failure, if the most recent flush failed.
    /// </summary>
    public string? LastError { get; private set; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        rawTimer?.Dispose();
        bucketTimer?.Dispose();
        rollupTimer?.Dispose();
        maintenanceTimer?.Dispose();
        gate.Dispose();
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        rawTimer = new Timer(_ => _ = FlushAsync(false), null, RawFlushInterval, RawFlushInterval);
        bucketTimer = new Timer(_ => _ = FlushAsync(true), null, BucketFlushInterval, BucketFlushInterval);
        rollupTimer = new Timer(_ => _ = RollupAsync(), null, TimeSpan.FromMinutes(2), RollupInterval);
        maintenanceTimer = new Timer(_ => _ = MaintenanceAsync(), null, TimeSpan.FromMinutes(10),
            MaintenanceInterval);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Writes everything the collector holds. Raw rows go every call; buckets only when
    ///     <paramref name="includeBuckets" /> is set, so counters are written once per minute.
    /// </summary>
    /// <param name="includeBuckets">Whether to write the minute buckets as well as the raw rows.</param>
    public async Task FlushAsync(bool includeBuckets)
    {
        if (!collector.Enabled) return;
        var acquired = includeBuckets
            ? await gate.WaitAsync(TimeSpan.FromSeconds(45)).ConfigureAwait(false)
            : await gate.WaitAsync(0).ConfigureAwait(false);
        if (!acquired) return;

        var started = Stopwatch.GetTimestamp();
        try
        {
            var drained = collector.Drain();
            await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

            if (!drained.CommandRows.IsEmpty)
                await db.BulkCopyAsync(drained.CommandRows.ToArray()).ConfigureAwait(false);
            if (!drained.GuildEventRows.IsEmpty)
                await db.BulkCopyAsync(drained.GuildEventRows.ToArray()).ConfigureAwait(false);
            if (!drained.PageViewRows.IsEmpty)
                await db.BulkCopyAsync(drained.PageViewRows.ToArray()).ConfigureAwait(false);

            foreach (var ((hour, guildId, feature), (count, errors)) in drained.FeatureActivity)
                await db.ExecuteAsync(
                    """
                    INSERT INTO "AnalyticsFeatureActivity" ("HourUtc", "GuildId", "Feature", "Bot", "Count", "Errors")
                    VALUES (@hour, @guild, @feature, @bot, @count, @errors)
                    ON CONFLICT ("HourUtc", "GuildId", "Feature") DO UPDATE
                    SET "Count" = "AnalyticsFeatureActivity"."Count" + EXCLUDED."Count",
                        "Errors" = "AnalyticsFeatureActivity"."Errors" + EXCLUDED."Errors"
                    """,
                    new DataParameter("hour", hour, DataType.DateTime),
                    new DataParameter("guild", (decimal)guildId, DataType.Decimal),
                    new DataParameter("feature", feature, DataType.Text),
                    new DataParameter("bot", collector.Bot, DataType.Text),
                    new DataParameter("count", count, DataType.Int32),
                    new DataParameter("errors", errors, DataType.Int32)).ConfigureAwait(false);

            foreach (var ((hour, guildId, eventType), count) in drained.GuildActivity)
                await db.ExecuteAsync(
                    """
                    INSERT INTO "AnalyticsGuildActivity" ("Hour", "GuildId", "EventType", "Bot", "Count")
                    VALUES (@hour, @guild, @type, @bot, @count)
                    ON CONFLICT ("GuildId", "EventType", "Hour") DO UPDATE
                    SET "Count" = "AnalyticsGuildActivity"."Count" + EXCLUDED."Count"
                    """,
                    new DataParameter("hour", hour, DataType.DateTime),
                    new DataParameter("guild", (decimal)guildId, DataType.Decimal),
                    new DataParameter("type", eventType, DataType.Text),
                    new DataParameter("bot", collector.Bot, DataType.Text),
                    new DataParameter("count", count, DataType.Int32)).ConfigureAwait(false);

            foreach (var ((hour, type, module, hash), sample) in drained.ErrorSamples)
                await db.ExecuteAsync(
                    """
                    INSERT INTO "AnalyticsErrorSample"
                        ("Hour", "Bot", "Shard", "Type", "Module", "Location", "MessageHash", "Message", "Count", "FirstSeen", "LastSeen", "LastGuildId")
                    VALUES (@hour, @bot, @shard, @type, @module, @location, @hash, @message, @count, @first, @last, @guild)
                    ON CONFLICT ("Hour", "Bot", "Type", COALESCE("Module", ''), "MessageHash") DO UPDATE
                    SET "Count" = "AnalyticsErrorSample"."Count" + EXCLUDED."Count",
                        "LastSeen" = GREATEST("AnalyticsErrorSample"."LastSeen", EXCLUDED."LastSeen"),
                        "LastGuildId" = COALESCE(EXCLUDED."LastGuildId", "AnalyticsErrorSample"."LastGuildId"),
                        "Shard" = COALESCE(EXCLUDED."Shard", "AnalyticsErrorSample"."Shard")
                    """,
                    new DataParameter("hour", hour, DataType.DateTime),
                    new DataParameter("bot", collector.Bot, DataType.Text),
                    new DataParameter("shard", sample.Shard, DataType.Int32),
                    new DataParameter("type", type, DataType.Text),
                    new DataParameter("module", module.Length == 0 ? null : module, DataType.Text),
                    new DataParameter("location", sample.Location, DataType.Text),
                    new DataParameter("hash", hash, DataType.Text),
                    new DataParameter("message", sample.Message, DataType.Text),
                    new DataParameter("count", sample.Count, DataType.Int32),
                    new DataParameter("first", sample.FirstSeen, DataType.DateTime),
                    new DataParameter("last", sample.LastSeen, DataType.DateTime),
                    new DataParameter("guild", sample.LastGuildId is null ? null : (decimal)sample.LastGuildId.Value,
                        DataType.Decimal)).ConfigureAwait(false);

            if (includeBuckets || drained.Series.Count > 5000)
            {
                foreach (var (key, series) in drained.Series)
                {
                    await UpsertBucketAsync(db, 1, key.Minute, key.Metric, key.Labels, series.Count, series.Sum,
                        series.Min, series.Max, series.Last,
                        series.Hist is null ? null : string.Join(',', series.Hist)).ConfigureAwait(false);
                    Register(key.Metric, series.Kind, key.Labels);
                }

                await FlushRegistryAsync(db).ConfigureAwait(false);
            }
            else if (drained.Series.Count > 0)
            {
                Requeue(drained.Series);
            }

            LastFlushAt = DateTime.UtcNow;
            LastError = null;
            collector.Gauge("an.flush", Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                ("table", includeBuckets ? "buckets" : "raw"));
            collector.Gauge("an.rows", drained.CommandRows.Count + drained.PageViewRows.Count +
                                       drained.GuildEventRows.Count, ("table", "raw"));
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            logger.LogWarning(ex, "Analytics flush failed");
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    ///     Folds finished 1m buckets into 5m and finished 5m buckets into 1h.
    /// </summary>
    public async Task RollupAsync()
    {
        if (!collector.Enabled) return;

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
            var now = DateTime.UtcNow;

            await FoldAsync(db, 1, 5, AnalyticsCollector.MinuteOf(now).AddMinutes(-(now.Minute % 5)).AddMinutes(-5),
                TimeSpan.FromHours(2)).ConfigureAwait(false);
            await FoldAsync(db, 5, 60, AnalyticsCollector.HourOf(now).AddHours(-1), TimeSpan.FromHours(26))
                .ConfigureAwait(false);

            LastRollupAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Analytics rollup failed");
        }
    }

    /// <summary>
    ///     Deletes rows past their retention window.
    /// </summary>
    public async Task MaintenanceAsync()
    {
        if (!collector.Enabled) return;

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
            var now = DateTime.UtcNow;
            var rawCutoff = now.AddDays(-Math.Max(1, config.Data.AnalyticsRetentionDays));

            await Prune(db, "AnalyticsBucket", "\"Resolution\" = 1 AND \"Bucket\" < @cutoff", now.AddHours(-48))
                .ConfigureAwait(false);
            await Prune(db, "AnalyticsBucket", "\"Resolution\" = 5 AND \"Bucket\" < @cutoff", now.AddDays(-14))
                .ConfigureAwait(false);
            await Prune(db, "AnalyticsBucket", "\"Resolution\" = 60 AND \"Bucket\" < @cutoff", now.AddDays(-400))
                .ConfigureAwait(false);
            await Prune(db, "AnalyticsCommandInvocation", "\"At\" < @cutoff", rawCutoff).ConfigureAwait(false);
            await Prune(db, "AnalyticsPageView", "\"At\" < @cutoff", rawCutoff).ConfigureAwait(false);
            await Prune(db, "AnalyticsGuildEventLog", "\"At\" < @cutoff", rawCutoff).ConfigureAwait(false);
            await Prune(db, "AnalyticsErrorSample", "\"LastSeen\" < @cutoff", now.AddDays(-90)).ConfigureAwait(false);
            await Prune(db, "AnalyticsFeatureActivity", "\"HourUtc\" < @cutoff", now.AddDays(-90))
                .ConfigureAwait(false);
            await Prune(db, "AnalyticsGuildActivity", "\"Hour\" < @cutoff", now.AddDays(-90)).ConfigureAwait(false);
            await Prune(db, "AnalyticsAlertEvent", "\"At\" < @cutoff", now.AddDays(-180)).ConfigureAwait(false);

            LastMaintenanceAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Analytics maintenance failed");
        }
    }

    /// <summary>
    ///     Adds or merges one bucket row.
    /// </summary>
    public static Task UpsertBucketAsync(DataConnection db, short resolution, DateTime bucket, string metric,
        string labels, double count, double sum, double? min, double? max, double? last, string? hist)
    {
        return db.ExecuteAsync(
            """
            INSERT INTO "AnalyticsBucket" ("Resolution", "Bucket", "Metric", "Labels", "Count", "Sum", "Min", "Max", "Last", "Hist")
            VALUES (@res, @bucket, @metric, @labels, @count, @sum, @min, @max, @last, @hist)
            ON CONFLICT ("Resolution", "Metric", "Labels", "Bucket") DO UPDATE
            SET "Count" = "AnalyticsBucket"."Count" + EXCLUDED."Count",
                "Sum" = "AnalyticsBucket"."Sum" + EXCLUDED."Sum",
                "Min" = LEAST("AnalyticsBucket"."Min", EXCLUDED."Min"),
                "Max" = GREATEST("AnalyticsBucket"."Max", EXCLUDED."Max"),
                "Last" = COALESCE(EXCLUDED."Last", "AnalyticsBucket"."Last"),
                "Hist" = CASE
                    WHEN "AnalyticsBucket"."Hist" IS NULL THEN EXCLUDED."Hist"
                    WHEN EXCLUDED."Hist" IS NULL THEN "AnalyticsBucket"."Hist"
                    ELSE (SELECT string_agg((a.v::bigint + b.v::bigint)::text, ',' ORDER BY a.i)
                          FROM unnest(string_to_array("AnalyticsBucket"."Hist", ',')) WITH ORDINALITY AS a(v, i)
                          JOIN unnest(string_to_array(EXCLUDED."Hist", ',')) WITH ORDINALITY AS b(v, i) ON a.i = b.i)
                END
            """,
            new DataParameter("res", resolution, DataType.Int16),
            new DataParameter("bucket", bucket, DataType.DateTime),
            new DataParameter("metric", metric, DataType.Text),
            new DataParameter("labels", labels, DataType.Text),
            new DataParameter("count", count, DataType.Double),
            new DataParameter("sum", sum, DataType.Double),
            new DataParameter("min", min is null || min == double.MaxValue ? null : min, DataType.Double),
            new DataParameter("max", max is null || max == double.MinValue ? null : max, DataType.Double),
            new DataParameter("last", last, DataType.Double),
            new DataParameter("hist", hist, DataType.Text));
    }

    private async Task FoldAsync(DataConnection db, short from, short to, DateTime upTo, TimeSpan lookBack)
    {
        var since = upTo - lookBack;
        var toMinutes = (int)to;

        var rows = await db.GetTable<AnalyticsBucket>()
            .Where(b => b.Resolution == from && b.Bucket >= since && b.Bucket < upTo)
            .ToListAsync().ConfigureAwait(false);
        if (rows.Count == 0) return;

        var folded = new Dictionary<(DateTime Bucket, string Metric, string Labels), Fold>();
        foreach (var row in rows)
        {
            var aligned = new DateTime(row.Bucket.Ticks - row.Bucket.Ticks % TimeSpan.FromMinutes(toMinutes).Ticks,
                DateTimeKind.Utc);
            var key = (aligned, row.Metric, row.Labels);
            if (!folded.TryGetValue(key, out var fold))
            {
                fold = new Fold();
                folded[key] = fold;
            }

            fold.Add(row);
        }

        var lastCutoff = upTo;
        foreach (var ((bucket, metric, labels), fold) in folded)
        {
            var exists = await db.GetTable<AnalyticsBucket>()
                .AnyAsync(b => b.Resolution == to && b.Bucket == bucket && b.Metric == metric && b.Labels == labels)
                .ConfigureAwait(false);
            if (exists) continue;

            await UpsertBucketAsync(db, to, bucket, metric, labels, fold.Count, fold.Sum, fold.Min, fold.Max,
                fold.Last, fold.Hist).ConfigureAwait(false);
        }

        _ = lastCutoff;
    }

    private static Task<int> Prune(DataConnection db, string table, string where, DateTime cutoff)
    {
        return db.ExecuteAsync($"DELETE FROM \"{table}\" WHERE {where}",
            new DataParameter("cutoff", cutoff, DataType.DateTime));
    }

    private void Requeue(IReadOnlyDictionary<AnalyticsCollector.SeriesKey, AnalyticsCollector.Series> series)
    {
        foreach (var (key, value) in series)
        {
            var labels = AnalyticsCollector.ParseLabels(key.Labels)
                .Where(pair => pair.Key != "bot")
                .Select(pair => (pair.Key, pair.Value)).ToArray();
            switch (value.Kind)
            {
                case AnalyticsCollector.MetricKind.Counter:
                    collector.Counter(key.Metric, value.Sum, labels);
                    break;
                case AnalyticsCollector.MetricKind.Gauge:
                    collector.Gauge(key.Metric, value.Last, labels);
                    break;
                default:
                    for (var i = 0; i < value.Hist!.Length; i++)
                    {
                        var bound = i < AnalyticsCollector.HistogramBounds.Length
                            ? AnalyticsCollector.HistogramBounds[i]
                            : AnalyticsCollector.HistogramBounds[^1] * 2;
                        for (var n = 0; n < value.Hist[i]; n++) collector.Duration(key.Metric, bound, labels);
                    }

                    break;
            }
        }
    }

    private void Register(string metric, AnalyticsCollector.MetricKind kind, string labels)
    {
        lock (registry)
        {
            if (!registry.TryGetValue(metric, out var entry))
            {
                entry = new MetricRegistryEntry(kind.ToString().ToLowerInvariant());
                registry[metric] = entry;
            }

            foreach (var (key, value) in AnalyticsCollector.ParseLabels(labels))
            {
                if (!entry.Labels.TryGetValue(key, out var values))
                {
                    values = new HashSet<string>(StringComparer.Ordinal);
                    entry.Labels[key] = values;
                }

                if (values.Count < 50) values.Add(value);
            }

            entry.Dirty = true;
        }
    }

    private async Task FlushRegistryAsync(DataConnection db)
    {
        List<(string Metric, MetricRegistryEntry Entry)> dirty;
        lock (registry)
        {
            dirty = registry.Where(pair => pair.Value.Dirty).Select(pair => (pair.Key, pair.Value)).ToList();
            foreach (var (_, entry) in dirty) entry.Dirty = false;
        }

        foreach (var (metric, entry) in dirty)
        {
            string json;
            lock (registry)
            {
                json = JsonSerializer.Serialize(entry.Labels.ToDictionary(pair => pair.Key,
                    pair => pair.Value.OrderBy(v => v, StringComparer.Ordinal).ToArray()));
            }

            await db.ExecuteAsync(
                """
                INSERT INTO "AnalyticsMetricRegistry" ("Metric", "Kind", "LabelsJson", "LastSeen")
                VALUES (@metric, @kind, @labels, @seen)
                ON CONFLICT ("Metric") DO UPDATE
                SET "Kind" = EXCLUDED."Kind", "LabelsJson" = EXCLUDED."LabelsJson", "LastSeen" = EXCLUDED."LastSeen"
                """,
                new DataParameter("metric", metric, DataType.Text),
                new DataParameter("kind", entry.Kind, DataType.Text),
                new DataParameter("labels", json, DataType.Text),
                new DataParameter("seen", DateTime.UtcNow, DataType.DateTime)).ConfigureAwait(false);
        }
    }

    private sealed class MetricRegistryEntry(string kind)
    {
        public string Kind { get; } = kind;
        public Dictionary<string, HashSet<string>> Labels { get; } = new(StringComparer.Ordinal);
        public bool Dirty { get; set; }
    }

    private sealed class Fold
    {
        public double Count;
        public double? Last;
        public double? Max;
        public double? Min;
        public double Sum;
        private long[]? hist;
        private DateTime lastAt = DateTime.MinValue;

        public string? Hist
        {
            get
            {
                return hist is null ? null : string.Join(',', hist);
            }
        }

        public void Add(AnalyticsBucket row)
        {
            Count += row.Count;
            Sum += row.Sum;
            if (row.Min is { } min && (Min is null || min < Min)) Min = min;
            if (row.Max is { } max && (Max is null || max > Max)) Max = max;
            if (row.Bucket >= lastAt)
            {
                lastAt = row.Bucket;
                Last = row.Last;
            }

            if (string.IsNullOrEmpty(row.Hist)) return;
            var parts = row.Hist.Split(',');
            hist ??= new long[parts.Length];
            for (var i = 0; i < parts.Length && i < hist.Length; i++)
                if (long.TryParse(parts[i], out var n))
                    hist[i] += n;
        }
    }
}