using System.Text.Json;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services.Impl;

namespace Mewdeko.Services.Analytics;

/// <summary>
///     Nightly snapshot of the fleet, census of configured features and settings, and long range daily totals.
/// </summary>
public sealed class AnalyticsCensusService : INService, IReadyExecutor, IDisposable
{
    /// <summary>
    ///     Metrics whose daily sums are kept in <c>AnalyticsDailyTotal</c>.
    /// </summary>
    public static readonly string[] TotalMetrics =
    [
        "cmd.count", "ev.count", "guild.join", "guild.leave", "err.count", "page.view", "ai.tokens",
        "music.track_start"
    ];

    private static readonly TimeSpan RunAt = new(0, 5, 0);
    private readonly DiscordShardedClient client;

    private readonly AnalyticsCollector collector;
    private readonly BotCredentials credentials;
    private readonly IDataConnectionFactory dbFactory;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ILogger<AnalyticsCensusService> logger;
    private Timer? dailyTimer;
    private bool disposed;

    private Timer? startupTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AnalyticsCensusService" /> class.
    /// </summary>
    /// <param name="collector">The collector, read for the enabled flag and instance id.</param>
    /// <param name="client">The Discord client, for guild and member counts.</param>
    /// <param name="credentials">Bot credentials, for the master instance flag.</param>
    /// <param name="dbFactory">Creates database connections.</param>
    /// <param name="logger">Records failures.</param>
    public AnalyticsCensusService(AnalyticsCollector collector, DiscordShardedClient client,
        BotCredentials credentials, IDataConnectionFactory dbFactory, ILogger<AnalyticsCensusService> logger)
    {
        this.collector = collector;
        this.client = client;
        this.credentials = credentials;
        this.dbFactory = dbFactory;
        this.logger = logger;
    }

    /// <summary>
    ///     When the last successful run finished.
    /// </summary>
    public DateTime? LastRunAt { get; private set; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        startupTimer?.Dispose();
        dailyTimer?.Dispose();
        gate.Dispose();
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        startupTimer = new Timer(_ => _ = RunIfMissingAsync(), null, TimeSpan.FromMinutes(5),
            Timeout.InfiniteTimeSpan);
        dailyTimer = new Timer(_ => _ = RunAsync(), null, UntilNextRun(), TimeSpan.FromDays(1));
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Writes today's snapshot, census and totals.
    /// </summary>
    public async Task RunAsync()
    {
        if (!collector.Enabled) return;
        if (!await gate.WaitAsync(0).ConfigureAwait(false)) return;

        try
        {
            var today = DateTime.UtcNow.Date;
            await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

            Dictionary<string, int>? configured = null;
            if (credentials.IsMasterInstance)
            {
                configured = await WriteCensusAsync(db, today).ConfigureAwait(false);
                await WriteTotalsAsync(db, today.AddDays(-1)).ConfigureAwait(false);
                await WriteTotalsAsync(db, today).ConfigureAwait(false);
            }

            await WriteSnapshotAsync(db, today, configured).ConfigureAwait(false);
            LastRunAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Analytics census failed");
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RunIfMissingAsync()
    {
        if (!collector.Enabled) return;

        try
        {
            var today = DateTime.UtcNow.Date;
            var bot = collector.Bot;
            await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
            var exists = await db.AnalyticsDailySnapshots.AnyAsync(s => s.Day == today && s.Bot == bot)
                .ConfigureAwait(false);
            if (exists) return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Analytics census check failed");
            return;
        }

        await RunAsync().ConfigureAwait(false);
    }

    private static TimeSpan UntilNextRun()
    {
        var now = DateTime.UtcNow;
        var next = now.Date + RunAt;
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }

    private async Task WriteSnapshotAsync(MewdekoDb db, DateTime day, Dictionary<string, int>? configured)
    {
        var guilds = client.Guilds;
        var snapshot = new AnalyticsDailySnapshot
        {
            Day = day,
            Bot = collector.Bot,
            Guilds = guilds.Count,
            Users = guilds.Sum(g => (long)g.MemberCount),
            FeaturesJson = configured is null ? null : JsonSerializer.Serialize(configured)
        };

        await db.InsertOrReplaceAsync(snapshot).ConfigureAwait(false);
    }

    private async Task<Dictionary<string, int>> WriteCensusAsync(MewdekoDb db, DateTime day)
    {
        var configured = new Dictionary<string, int>(StringComparer.Ordinal);
        var rows = new List<AnalyticsConfigCensus>();

        foreach (var feature in AnalyticsFeatureDefinitions.Features)
        {
            try
            {
                var set = await feature.Configured(db).Distinct().CountAsync().ConfigureAwait(false);
                var on = await feature.Enabled(db).Distinct().CountAsync().ConfigureAwait(false);
                configured[feature.Key] = set;
                rows.Add(Row(day, $"feature.{feature.Key}.configured", set));
                rows.Add(Row(day, $"feature.{feature.Key}.enabled", on));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Analytics census failed for feature {Feature}", feature.Key);
            }
        }

        foreach (var setting in AnalyticsFeatureDefinitions.Settings)
        {
            try
            {
                var counts = await setting.Values(db)
                    .GroupBy(v => v)
                    .Select(g => new
                    {
                        Value = g.Key, Count = g.Count()
                    })
                    .ToListAsync().ConfigureAwait(false);

                var labelled = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var entry in counts)
                {
                    var label = setting.Label(entry.Value);
                    labelled[label] = labelled.GetValueOrDefault(label) + entry.Count;
                }

                foreach (var (label, count) in labelled)
                    rows.Add(Row(day, $"setting.{setting.Table}.{setting.Column}.{label}", count));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Analytics census failed for setting {Table}.{Column}", setting.Table,
                    setting.Column);
            }
        }

        foreach (var row in rows)
            await db.InsertOrReplaceAsync(row).ConfigureAwait(false);

        return configured;
    }

    private static AnalyticsConfigCensus Row(DateTime day, string metric, double value)
    {
        return new AnalyticsConfigCensus
        {
            Day = day, Metric = metric.Length <= 128 ? metric : metric[..128], Value = value
        };
    }

    private async Task WriteTotalsAsync(MewdekoDb db, DateTime day)
    {
        var now = DateTime.UtcNow;
        var dayEnd = day.AddDays(1);
        var hourCut = AnalyticsCollector.HourOf(now).AddHours(-2);
        var fiveCut = AnalyticsCollector.MinuteOf(now).AddMinutes(-(now.Minute % 5)).AddMinutes(-10);

        foreach (var metric in TotalMetrics)
        {
            var totals = new Dictionary<string, double>(StringComparer.Ordinal);

            await FoldAsync(db, metric, 60, day, Min(dayEnd, hourCut), totals).ConfigureAwait(false);
            await FoldAsync(db, metric, 5, Max(day, hourCut), Min(dayEnd, fiveCut), totals).ConfigureAwait(false);
            await FoldAsync(db, metric, 1, Max(day, fiveCut), dayEnd, totals).ConfigureAwait(false);

            foreach (var (bot, value) in totals)
                await db.InsertOrReplaceAsync(new AnalyticsDailyTotal
                {
                    Day = day, Bot = bot, Metric = metric, Value = value
                }).ConfigureAwait(false);
        }
    }

    private static async Task FoldAsync(MewdekoDb db, string metric, short resolution, DateTime from, DateTime to,
        Dictionary<string, double> totals)
    {
        if (from >= to) return;

        var groups = await db.AnalyticsBuckets
            .Where(b => b.Metric == metric && b.Resolution == resolution && b.Bucket >= from && b.Bucket < to)
            .GroupBy(b => b.Labels)
            .Select(g => new
            {
                Labels = g.Key, Sum = g.Sum(b => b.Sum)
            })
            .ToListAsync().ConfigureAwait(false);

        foreach (var group in groups)
        {
            var bot = AnalyticsCollector.ParseLabels(group.Labels)
                .GetValueOrDefault("bot", metric == "page.view" ? "web" : "unknown");
            totals[bot] = totals.GetValueOrDefault(bot) + group.Sum;
        }
    }

    private static DateTime Min(DateTime a, DateTime b)
    {
        return a < b ? a : b;
    }

    private static DateTime Max(DateTime a, DateTime b)
    {
        return a > b ? a : b;
    }
}