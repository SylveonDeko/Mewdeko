using System.Globalization;
using System.Text;
using System.Text.Json;
using DataModel;
using LinqToDB.Async;
using Mewdeko.Controllers.Common.Analytics;
using Mewdeko.Modules.OwnerOnly.Services;

namespace Mewdeko.Services.Analytics;

/// <summary>
///     Read side of the analytics pipeline: series over the bucket table plus every drilldown the dashboard shows.
/// </summary>
public sealed partial class AnalyticsQueryService : INService
{
    private const int MaxBucketsPerQuery = 5000;
    private const int MaxRowsPerQuery = 200000;

    private static readonly string[] SizeBuckets = ["tiny", "small", "medium", "large", "huge", "unknown"];

    private readonly DiscordShardedClient client;
    private readonly AnalyticsCollector collector;
    private readonly IDataConnectionFactory dbFactory;
    private readonly InstanceManagementService instances;
    private readonly AnalyticsWriter writer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AnalyticsQueryService" /> class.
    /// </summary>
    /// <param name="dbFactory">Creates database connections.</param>
    /// <param name="client">The Discord client, for guild names and sizes.</param>
    /// <param name="collector">The collector, for pending counts.</param>
    /// <param name="writer">The writer, for pipeline timestamps.</param>
    /// <param name="instances">The instance registry.</param>
    public AnalyticsQueryService(IDataConnectionFactory dbFactory, DiscordShardedClient client,
        AnalyticsCollector collector, AnalyticsWriter writer, InstanceManagementService instances)
    {
        this.dbFactory = dbFactory;
        this.client = client;
        this.collector = collector;
        this.writer = writer;
        this.instances = instances;
    }

    /// <summary>
    ///     Picks the bucket width for a range: 1m up to 6h, 5m up to 3d, else 1h.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <param name="forced">A requested width, honoured when it is 1, 5 or 60.</param>
    /// <returns>The width in minutes.</returns>
    public static short ResolveResolution(AnalyticsRange range, short? forced)
    {
        var resolution = forced is 1 or 5 or 60
            ? forced.Value
            : range.Span <= TimeSpan.FromHours(6)
                ? (short)1
                : range.Span <= TimeSpan.FromDays(3)
                    ? (short)5
                    : (short)60;

        while (resolution < 60 && range.Span.TotalMinutes / resolution > MaxBucketsPerQuery)
            resolution = resolution == 1 ? (short)5 : (short)60;

        return resolution;
    }

    /// <summary>
    ///     Returns the metric registry.
    /// </summary>
    public async Task<List<MetricInfo>> MetricsAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var rows = await db.AnalyticsMetricRegistries.OrderBy(x => x.Metric).ToListAsync().ConfigureAwait(false);
        return rows.Select(row => new MetricInfo(row.Metric, row.Kind, ParseRegistryLabels(row.LabelsJson),
            Utc(row.LastSeen))).ToList();
    }

    /// <summary>
    ///     Runs a series query over the bucket table.
    /// </summary>
    /// <param name="query">The query.</param>
    public async Task<SeriesResponse> SeriesAsync(SeriesQuery query)
    {
        var resolution = ResolveResolution(query.Range, query.Resolution);
        var width = TimeSpan.FromMinutes(resolution);
        var start = Align(query.Range.From, width);
        var bucketCount = (int)Math.Ceiling((query.Range.To - start) / width);
        if (bucketCount <= 0) bucketCount = 1;

        var rows = await LoadBucketsAsync(query.Metric, resolution, start, query.Range.To).ConfigureAwait(false);

        var accumulators = new Dictionary<string, Accumulator[]>(StringComparer.Ordinal);
        var seriesLabels = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var labels = AnalyticsCollector.ParseLabels(row.Labels);
            if (!Matches(labels, query.Filters)) continue;

            var name = SeriesName(query, labels);
            if (!accumulators.TryGetValue(name, out var slots))
            {
                slots = new Accumulator[bucketCount];
                for (var i = 0; i < slots.Length; i++) slots[i] = new Accumulator();
                accumulators[name] = slots;
                seriesLabels[name] = string.IsNullOrEmpty(query.GroupBy)
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>
                    {
                        [query.GroupBy] = name
                    };
            }

            var index = (int)((row.Bucket - start) / width);
            if (index < 0 || index >= bucketCount) continue;
            slots[index].Add(row);
        }

        var widthSeconds = width.TotalSeconds;
        var series = new List<SeriesItem>(accumulators.Count);
        foreach (var (name, slots) in accumulators)
        {
            var points = new List<SeriesPoint>(bucketCount);
            for (var i = 0; i < bucketCount; i++)
                points.Add(new SeriesPoint(ToUnix(start + width * i), slots[i].Value(query.Aggregation, widthSeconds)));
            series.Add(new SeriesItem(name, seriesLabels[name], points));
        }

        series.Sort((a, b) => Total(b).CompareTo(Total(a)));

        var truncated = false;
        if (query.MaxSeries > 0 && series.Count > query.MaxSeries)
        {
            truncated = true;
            var kept = series.Take(query.MaxSeries).ToList();
            var rest = series.Skip(query.MaxSeries).ToList();
            var other = new List<SeriesPoint>(bucketCount);
            for (var i = 0; i < bucketCount; i++)
            {
                double? sum = null;
                foreach (var item in rest)
                {
                    var value = item.Points[i].Value;
                    if (value.HasValue) sum = (sum ?? 0) + value.Value;
                }

                other.Add(new SeriesPoint(ToUnix(start + width * i), sum));
            }

            kept.Add(new SeriesItem("Other", new Dictionary<string, string>
            {
                ["count"] = rest.Count.ToString(CultureInfo.InvariantCulture),
                ["names"] = string.Join(", ", rest.Take(40).Select(item => item.Name))
            }, other));
            series = kept;
        }

        return new SeriesResponse(resolution, series, truncated);
    }

    /// <summary>
    ///     Aggregates a metric over a whole range into one value.
    /// </summary>
    /// <param name="metric">The metric name.</param>
    /// <param name="range">The range.</param>
    /// <param name="filters">Exact label matches.</param>
    /// <param name="aggregation">The aggregation.</param>
    public async Task<AggregateResponse> AggregateAsync(string metric, AnalyticsRange range,
        IReadOnlyDictionary<string, string> filters, string aggregation)
    {
        var resolution = ResolveResolution(range, null);
        var rows = await LoadBucketsAsync(metric, resolution, Align(range.From, TimeSpan.FromMinutes(resolution)),
            range.To).ConfigureAwait(false);

        var accumulator = new Accumulator();
        foreach (var row in rows)
        {
            if (!Matches(AnalyticsCollector.ParseLabels(row.Labels), filters)) continue;
            accumulator.Add(row);
        }

        return new AggregateResponse(accumulator.Value(aggregation, range.Span.TotalSeconds));
    }

    /// <summary>
    ///     Aggregates a metric per value of one label.
    /// </summary>
    /// <param name="metric">The metric name.</param>
    /// <param name="label">The label to split on.</param>
    /// <param name="range">The range.</param>
    /// <param name="filters">Exact label matches.</param>
    /// <param name="aggregation">The aggregation.</param>
    /// <param name="limit">Rows to return, largest first.</param>
    public async Task<List<BreakdownItem>> BreakdownAsync(string metric, string label, AnalyticsRange range,
        IReadOnlyDictionary<string, string> filters, string aggregation, int limit)
    {
        var resolution = ResolveResolution(range, null);
        var rows = await LoadBucketsAsync(metric, resolution, Align(range.From, TimeSpan.FromMinutes(resolution)),
            range.To).ConfigureAwait(false);

        var groups = new Dictionary<string, Accumulator>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var labels = AnalyticsCollector.ParseLabels(row.Labels);
            if (!Matches(labels, filters)) continue;
            var name = labels.TryGetValue(label, out var value) && value.Length > 0 ? value : "unknown";
            if (!groups.TryGetValue(name, out var accumulator))
            {
                accumulator = new Accumulator();
                groups[name] = accumulator;
            }

            accumulator.Add(row);
        }

        return groups
            .Select(pair => new BreakdownItem(pair.Key, pair.Value.Value(aggregation, range.Span.TotalSeconds) ?? 0))
            .OrderByDescending(item => item.Value)
            .Take(Math.Clamp(limit, 1, 500))
            .ToList();
    }

    /// <summary>
    ///     Counts gateway events per type over the range.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <param name="filters">Exact label matches.</param>
    public async Task<List<EventCount>> EventCountsAsync(AnalyticsRange range,
        IReadOnlyDictionary<string, string> filters)
    {
        var items = await BreakdownAsync("ev.count", "type", range, filters, "sum", 500).ConfigureAwait(false);
        return items.Select(item => new EventCount(item.Name, item.Value)).ToList();
    }

    /// <summary>
    ///     Returns a metric's daily totals for the last N days, summed across instances.
    /// </summary>
    /// <param name="metric">The metric name.</param>
    /// <param name="days">How many days back.</param>
    /// <param name="bot">Instance filter.</param>
    public async Task<List<DayValue>> DailyTotalsAsync(string metric, int days, string? bot)
    {
        days = Math.Clamp(days, 1, 400);
        var since = DateTime.UtcNow.Date.AddDays(-days);
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var query = db.AnalyticsDailyTotals.Where(x => x.Metric == metric && x.Day >= since);
        if (!string.IsNullOrEmpty(bot)) query = query.Where(x => x.Bot == bot);

        var rows = await query.ToListAsync().ConfigureAwait(false);
        return rows.GroupBy(x => x.Day.Date)
            .Select(g => new DayValue(Utc(g.Key), g.Sum(x => x.Value)))
            .OrderBy(x => x.Day)
            .ToList();
    }

    /// <summary>
    ///     Renders a series query as CSV with one column per series.
    /// </summary>
    /// <param name="query">The query.</param>
    public async Task<string> ExportCsvAsync(SeriesQuery query)
    {
        var result = await SeriesAsync(query).ConfigureAwait(false);
        var builder = new StringBuilder();
        builder.Append("bucket,bucketUnix");
        foreach (var series in result.Series) builder.Append(',').Append(CsvField(series.Name));
        builder.Append('\n');

        if (result.Series.Count == 0) return builder.ToString();

        var points = result.Series[0].Points.Count;
        for (var i = 0; i < points; i++)
        {
            var unix = result.Series[0].Points[i].BucketUnix;
            builder.Append(DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime
                    .ToString("O", CultureInfo.InvariantCulture))
                .Append(',').Append(unix.ToString(CultureInfo.InvariantCulture));
            foreach (var series in result.Series)
            {
                builder.Append(',');
                var value = series.Points[i].Value;
                if (value.HasValue) builder.Append(value.Value.ToString("R", CultureInfo.InvariantCulture));
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Describes the state of the pipeline.
    /// </summary>
    public async Task<HealthResponse> HealthAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var tables = new List<TableHealth>
        {
            new("AnalyticsBucket", await db.AnalyticsBuckets.CountAsync().ConfigureAwait(false),
                Utc(await db.AnalyticsBuckets.MaxAsync(x => (DateTime?)x.Bucket).ConfigureAwait(false))),
            new("AnalyticsCommandInvocation",
                await db.AnalyticsCommandInvocations.CountAsync().ConfigureAwait(false),
                Utc(await db.AnalyticsCommandInvocations.MaxAsync(x => (DateTime?)x.At).ConfigureAwait(false))),
            new("AnalyticsFeatureActivity", await db.AnalyticsFeatureActivities.CountAsync().ConfigureAwait(false),
                Utc(await db.AnalyticsFeatureActivities.MaxAsync(x => (DateTime?)x.HourUtc).ConfigureAwait(false))),
            new("AnalyticsGuildActivity", await db.AnalyticsGuildActivities.CountAsync().ConfigureAwait(false),
                Utc(await db.AnalyticsGuildActivities.MaxAsync(x => (DateTime?)x.Hour).ConfigureAwait(false))),
            new("AnalyticsGuildEventLog", await db.AnalyticsGuildEventLogs.CountAsync().ConfigureAwait(false),
                Utc(await db.AnalyticsGuildEventLogs.MaxAsync(x => (DateTime?)x.At).ConfigureAwait(false))),
            new("AnalyticsErrorSample", await db.AnalyticsErrorSamples.CountAsync().ConfigureAwait(false),
                Utc(await db.AnalyticsErrorSamples.MaxAsync(x => (DateTime?)x.LastSeen).ConfigureAwait(false))),
            new("AnalyticsDailySnapshot", await db.AnalyticsDailySnapshots.CountAsync().ConfigureAwait(false),
                Utc(await db.AnalyticsDailySnapshots.MaxAsync(x => (DateTime?)x.Day).ConfigureAwait(false))),
            new("AnalyticsDailyTotal", await db.AnalyticsDailyTotals.CountAsync().ConfigureAwait(false),
                Utc(await db.AnalyticsDailyTotals.MaxAsync(x => (DateTime?)x.Day).ConfigureAwait(false))),
            new("AnalyticsConfigCensus", await db.AnalyticsConfigCensuses.CountAsync().ConfigureAwait(false),
                Utc(await db.AnalyticsConfigCensuses.MaxAsync(x => (DateTime?)x.Day).ConfigureAwait(false))),
            new("AnalyticsPageView", await db.AnalyticsPageViews.CountAsync().ConfigureAwait(false),
                Utc(await db.AnalyticsPageViews.MaxAsync(x => (DateTime?)x.At).ConfigureAwait(false))),
            new("AnalyticsMetricRegistry", await db.AnalyticsMetricRegistries.CountAsync().ConfigureAwait(false),
                Utc(await db.AnalyticsMetricRegistries.MaxAsync(x => (DateTime?)x.LastSeen).ConfigureAwait(false)))
        };

        var since = DateTime.UtcNow.AddHours(-48);
        var heartbeats = await db.AnalyticsBuckets
            .Where(x => x.Resolution == 1 && x.Metric == "guild.count" && x.Bucket >= since)
            .GroupBy(x => x.Labels)
            .Select(g => new
            {
                Labels = g.Key, Last = g.Max(x => x.Bucket)
            })
            .ToListAsync().ConfigureAwait(false);

        var lastByBot = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        foreach (var heartbeat in heartbeats)
        {
            if (!AnalyticsCollector.ParseLabels(heartbeat.Labels).TryGetValue("bot", out var bot)) continue;
            if (!lastByBot.TryGetValue(bot, out var current) || heartbeat.Last > current)
                lastByBot[bot] = heartbeat.Last;
        }

        var registered = await instances.GetActiveInstancesAsync().ConfigureAwait(false);
        var instanceHealth = registered.Select(instance =>
        {
            var botId = instance.BotId.ToString(CultureInfo.InvariantCulture);
            return new InstanceHealth(botId, instance.BotName, instance.Host, instance.Port, instance.IsActive,
                Utc(instance.LastStatusUpdate),
                lastByBot.TryGetValue(botId, out var last) ? Utc(last) : null);
        }).ToList();

        var (pendingSeries, pendingRows) = collector.Pending();
        return new HealthResponse(collector.Enabled, Utc(writer.LastFlushAt), Utc(writer.LastRollupAt),
            Utc(writer.LastMaintenanceAt), writer.LastError, pendingSeries, pendingRows, tables, instanceHealth);
    }

    private async Task<List<AnalyticsBucket>> LoadBucketsAsync(string metric, short resolution, DateTime from,
        DateTime to)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        return await db.AnalyticsBuckets
            .Where(x => x.Resolution == resolution && x.Metric == metric && x.Bucket >= from && x.Bucket < to)
            .OrderBy(x => x.Bucket)
            .Take(MaxRowsPerQuery)
            .ToListAsync().ConfigureAwait(false);
    }

    private static bool Matches(Dictionary<string, string> labels, IReadOnlyDictionary<string, string> filters)
    {
        foreach (var (key, expected) in filters)
        {
            if (string.IsNullOrEmpty(expected)) continue;
            if (!labels.TryGetValue(key, out var actual) || !string.Equals(actual, expected, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static string SeriesName(SeriesQuery query, Dictionary<string, string> labels)
    {
        if (string.IsNullOrEmpty(query.GroupBy)) return query.Metric;
        return labels.TryGetValue(query.GroupBy, out var value) && value.Length > 0 ? value : "unknown";
    }

    private static double Total(SeriesItem series)
    {
        double total = 0;
        foreach (var point in series.Points)
            if (point.Value.HasValue)
                total += point.Value.Value;
        return total;
    }

    private static DateTime Align(DateTime at, TimeSpan width)
    {
        return new DateTime(at.Ticks - at.Ticks % width.Ticks, DateTimeKind.Utc);
    }

    private static long ToUnix(DateTime at)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc)).ToUnixTimeSeconds();
    }

    private static DateTime Utc(DateTime at)
    {
        return DateTime.SpecifyKind(at, DateTimeKind.Utc);
    }

    private static DateTime? Utc(DateTime? at)
    {
        return at is null ? null : DateTime.SpecifyKind(at.Value, DateTimeKind.Utc);
    }

    private static string CsvField(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static Dictionary<string, string[]> ParseRegistryLabels(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string[]>>(json) ?? new Dictionary<string, string[]>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string[]>();
        }
    }

    /// <summary>
    ///     Interpolates a percentile from summed histogram counts, log-linear inside a bucket.
    /// </summary>
    /// <param name="hist">Counts per bound plus the overflow bucket.</param>
    /// <param name="quantile">The quantile between 0 and 1.</param>
    /// <returns>The estimated value in milliseconds, or null without observations.</returns>
    public static double? Percentile(long[] hist, double quantile)
    {
        var bounds = AnalyticsCollector.HistogramBounds;
        long total = 0;
        foreach (var n in hist) total += n;
        if (total <= 0) return null;

        var target = quantile * total;
        double cumulative = 0;
        double lower = 0;
        for (var i = 0; i < hist.Length; i++)
        {
            var upper = i < bounds.Length ? bounds[i] : double.PositiveInfinity;
            if (cumulative + hist[i] >= target)
            {
                if (double.IsPositiveInfinity(upper)) return lower;
                var fraction = hist[i] <= 0 ? 1 : (target - cumulative) / hist[i];
                return lower > 0 ? lower * Math.Pow(upper / lower, fraction) : upper * fraction;
            }

            cumulative += hist[i];
            lower = upper;
        }

        return lower;
    }

    private sealed class Accumulator
    {
        private bool any;
        private double count;
        private long[]? hist;
        private DateTime lastBucket = DateTime.MinValue;
        private double lastSum;
        private double max = double.MinValue;
        private double min = double.MaxValue;
        private double sum;

        public void Add(AnalyticsBucket row)
        {
            any = true;
            count += row.Count;
            sum += row.Sum;
            if (row.Min is { } rowMin && rowMin < min) min = rowMin;
            if (row.Max is { } rowMax && rowMax > max) max = rowMax;
            if (row.Last is { } last)
            {
                if (row.Bucket > lastBucket)
                {
                    lastBucket = row.Bucket;
                    lastSum = last;
                }
                else if (row.Bucket == lastBucket)
                {
                    lastSum += last;
                }
            }

            if (string.IsNullOrEmpty(row.Hist)) return;
            var parts = row.Hist.Split(',');
            hist ??= new long[AnalyticsCollector.HistogramBounds.Length + 1];
            for (var i = 0; i < parts.Length && i < hist.Length; i++)
                if (long.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                    hist[i] += n;
        }

        public double? Value(string aggregation, double widthSeconds)
        {
            if (!any) return null;

            switch (aggregation)
            {
                case "p50":
                    return hist is null ? Average() : Percentile(hist, 0.50);
                case "p95":
                    return hist is null ? Average() : Percentile(hist, 0.95);
                case "p99":
                    return hist is null ? Average() : Percentile(hist, 0.99);
                case "count":
                    return count;
                case "avg":
                    return Average();
                case "min":
                    return min == double.MaxValue ? null : min;
                case "max":
                    return max == double.MinValue ? null : max;
                case "last":
                    return lastBucket == DateTime.MinValue ? sum : lastSum;
                case "rate":
                    return widthSeconds > 0 ? sum / widthSeconds : null;
                default:
                    return sum;
            }
        }

        private double? Average()
        {
            return count > 0 ? sum / count : null;
        }
    }
}