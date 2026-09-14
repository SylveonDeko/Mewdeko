using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;

namespace Mewdeko.Services.Analytics;

/// <summary>
///     Posts a weekly summary of the fleet to the configured digest webhook every Monday at 09:00 UTC.
/// </summary>
public sealed class AnalyticsDigestService : INService, IReadyExecutor, IDisposable
{
    private const string SentMarker = "digest.sent";
    private const int SendHourUtc = 9;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

    private readonly AnalyticsAlertService alerts;
    private readonly DiscordShardedClient client;
    private readonly AnalyticsCollector collector;
    private readonly BotConfigService config;
    private readonly BotCredentials credentials;
    private readonly IDataConnectionFactory dbFactory;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ILogger<AnalyticsDigestService> logger;
    private bool disposed;

    private Timer? timer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AnalyticsDigestService" /> class.
    /// </summary>
    /// <param name="alerts">The alert service, used for webhook delivery.</param>
    /// <param name="collector">The collector, read for the enabled flag.</param>
    /// <param name="config">The bot config, read for the digest webhook.</param>
    /// <param name="credentials">Bot credentials, for the master flag.</param>
    /// <param name="dbFactory">Creates database connections.</param>
    /// <param name="client">The Discord client, for the live guild count.</param>
    /// <param name="logger">Records failures.</param>
    public AnalyticsDigestService(AnalyticsAlertService alerts, AnalyticsCollector collector,
        BotConfigService config, BotCredentials credentials, IDataConnectionFactory dbFactory,
        DiscordShardedClient client, ILogger<AnalyticsDigestService> logger)
    {
        this.alerts = alerts;
        this.collector = collector;
        this.config = config;
        this.credentials = credentials;
        this.dbFactory = dbFactory;
        this.client = client;
        this.logger = logger;
    }

    /// <summary>
    ///     When the digest was last accepted by Discord.
    /// </summary>
    public DateTime? LastSentAt { get; private set; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timer?.Dispose();
        gate.Dispose();
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        if (credentials.IsMasterInstance)
            timer = new Timer(_ => _ = TickAsync(), null, TimeSpan.FromMinutes(3), PollInterval);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Builds and sends the digest for the last seven days regardless of the schedule.
    /// </summary>
    /// <returns>Whether Discord accepted it.</returns>
    public async Task<bool> SendNowAsync()
    {
        var webhook = config.Data.AnalyticsDigestWebhook;
        if (!AnalyticsAlertService.IsDiscordWebhook(webhook)) return false;

        if (!await gate.WaitAsync(0).ConfigureAwait(false)) return false;
        try
        {
            var content = await BuildAsync(DateTime.UtcNow.Date).ConfigureAwait(false);
            var payload = JsonSerializer.Serialize(new
            {
                content,
                allowed_mentions = new
                {
                    parse = Array.Empty<string>()
                }
            });
            var sent = await alerts.PostWebhookAsync(webhook!, payload).ConfigureAwait(false);
            if (sent) LastSentAt = DateTime.UtcNow;
            return sent;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Analytics digest failed");
            return false;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task TickAsync()
    {
        if (!collector.Enabled) return;
        if (!AnalyticsAlertService.IsDiscordWebhook(config.Data.AnalyticsDigestWebhook)) return;

        var now = DateTime.UtcNow;
        if (now.DayOfWeek != DayOfWeek.Monday || now.Hour < SendHourUtc) return;

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
            var today = now.Date;
            var already = await db.AnalyticsConfigCensuses.AnyAsync(c => c.Day == today && c.Metric == SentMarker)
                .ConfigureAwait(false);
            if (already) return;

            if (!await SendNowAsync().ConfigureAwait(false)) return;

            await db.InsertOrReplaceAsync(new AnalyticsConfigCensus
            {
                Day = today, Metric = SentMarker, Value = 1
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Analytics digest schedule failed");
        }
    }

    private async Task<string> BuildAsync(DateTime today)
    {
        var from = today.AddDays(-7);
        var previousFrom = today.AddDays(-14);

        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);

        var guildsNow = await SnapshotGuildsAsync(db, today).ConfigureAwait(false) ?? client.Guilds.Count;
        var guildsThen = await SnapshotGuildsAsync(db, from).ConfigureAwait(false);

        var thisWeek = await TotalsAsync(db, from, today).ConfigureAwait(false);
        var lastWeek = await TotalsAsync(db, previousFrom, from).ConfigureAwait(false);

        var topCommands = await db.AnalyticsCommandInvocations
            .Where(c => c.At >= from && c.At < today)
            .GroupBy(c => c.Command)
            .Select(g => new
            {
                Name = g.Key, Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync().ConfigureAwait(false);

        var topErrors = await db.AnalyticsErrorSamples
            .Where(e => e.Hour >= from && e.Hour < today)
            .GroupBy(e => e.Type)
            .Select(g => new
            {
                Name = g.Key, Count = g.Sum(e => e.Count)
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync().ConfigureAwait(false);

        var topFeatures = await db.AnalyticsFeatureActivities
            .Where(f => f.HourUtc >= from && f.HourUtc < today)
            .GroupBy(f => f.Feature)
            .Select(g => new
            {
                Name = g.Key, Count = g.Sum(f => f.Count), Guilds = g.Select(f => f.GuildId).Distinct().Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync().ConfigureAwait(false);

        var builder = new StringBuilder();
        builder.Append("## Weekly analytics digest · ").Append(from.ToString("yyyy-MM-dd")).Append(" to ")
            .Append(today.ToString("yyyy-MM-dd")).Append(" UTC\n```\n");

        Line(builder, "Guilds", guildsNow, guildsThen);
        Line(builder, "Commands", thisWeek.GetValueOrDefault("cmd.count"), lastWeek.GetValueOrDefault("cmd.count"));
        Line(builder, "Errors", thisWeek.GetValueOrDefault("err.count"), lastWeek.GetValueOrDefault("err.count"));
        Line(builder, "Events", thisWeek.GetValueOrDefault("ev.count"), lastWeek.GetValueOrDefault("ev.count"));
        Line(builder, "New guilds", thisWeek.GetValueOrDefault("guild.join"), lastWeek.GetValueOrDefault("guild.join"));
        Line(builder, "Lost guilds", thisWeek.GetValueOrDefault("guild.leave"),
            lastWeek.GetValueOrDefault("guild.leave"));
        Line(builder, "Page views", thisWeek.GetValueOrDefault("page.view"), lastWeek.GetValueOrDefault("page.view"));
        Line(builder, "AI tokens", thisWeek.GetValueOrDefault("ai.tokens"), lastWeek.GetValueOrDefault("ai.tokens"));
        Line(builder, "Music tracks", thisWeek.GetValueOrDefault("music.track_start"),
            lastWeek.GetValueOrDefault("music.track_start"));
        builder.Append("```\n");

        builder.Append("**Top commands:** ");
        builder.Append(topCommands.Count == 0
            ? "none"
            : string.Join(", ", topCommands.Select(c => $"{c.Name} ({Num(c.Count)})")));
        builder.Append('\n');

        builder.Append("**Top errors:** ");
        builder.Append(topErrors.Count == 0
            ? "none"
            : string.Join(", ", topErrors.Select(e => $"{e.Name} ({Num(e.Count)})")));
        builder.Append('\n');

        builder.Append("**Top features:** ");
        builder.Append(topFeatures.Count == 0
            ? "none"
            : string.Join(", ", topFeatures.Select(f => $"{f.Name} ({Num(f.Count)} in {Num(f.Guilds)} guilds)")));

        return builder.ToString();
    }

    private static async Task<double?> SnapshotGuildsAsync(MewdekoDb db, DateTime day)
    {
        var rows = await db.AnalyticsDailySnapshots.Where(s => s.Day == day).ToListAsync().ConfigureAwait(false);
        return rows.Count == 0 ? null : rows.Sum(s => (double)s.Guilds);
    }

    private static async Task<Dictionary<string, double>> TotalsAsync(MewdekoDb db, DateTime from, DateTime to)
    {
        var rows = await db.AnalyticsDailyTotals
            .Where(t => t.Day >= from && t.Day < to)
            .GroupBy(t => t.Metric)
            .Select(g => new
            {
                Metric = g.Key, Value = g.Sum(t => t.Value)
            })
            .ToListAsync().ConfigureAwait(false);
        return rows.ToDictionary(r => r.Metric, r => r.Value, StringComparer.Ordinal);
    }

    private static void Line(StringBuilder builder, string label, double? current, double? previous)
    {
        builder.Append(label.PadRight(14)).Append(Num(current).PadLeft(12)).Append("  ")
            .Append(Delta(current, previous))
            .Append('\n');
    }

    private static string Delta(double? current, double? previous)
    {
        if (current is not { } c || previous is not { } p) return "▬ no comparison";
        var difference = c - p;
        var arrow = difference > 0 ? "▲" : difference < 0 ? "▼" : "▬";
        if (Math.Abs(p) <= double.Epsilon) return $"{arrow} {Num(Math.Abs(difference))}";
        var percent = difference / Math.Abs(p) * 100;
        return
            $"{arrow} {Num(Math.Abs(difference))} ({percent.ToString("+0.#;-0.#;0", CultureInfo.InvariantCulture)}%)";
    }

    private static string Num(double? value)
    {
        return value is { } v ? v.ToString("N0", CultureInfo.InvariantCulture) : "—";
    }
}