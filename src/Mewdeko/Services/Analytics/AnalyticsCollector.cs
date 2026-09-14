using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using DataModel;
using Mewdeko.Services.Settings;

namespace Mewdeko.Services.Analytics;

/// <summary>
///     In-memory aggregation of analytics observations for the current minute, drained by the writer.
/// </summary>
public sealed class AnalyticsCollector : IAnalyticsCollector, INService
{
    /// <summary>
    ///     What a metric records.
    /// </summary>
    public enum MetricKind
    {
        /// <summary>Additive count.</summary>
        Counter,

        /// <summary>Point in time value.</summary>
        Gauge,

        /// <summary>Distribution of durations.</summary>
        Histogram
    }

    private const int MaxSeriesPerMinute = 20000;
    private const int MaxRawRows = 50000;
    private const long EnabledRefreshMs = 5000;
    private long enabledCheckedAt = long.MinValue / 2;
    private volatile bool enabled;

    /// <summary>
    ///     Upper bounds in milliseconds of the histogram buckets; the last bucket is the overflow.
    /// </summary>
    public static readonly double[] HistogramBounds =
    [
        1, 2, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000, 30000, 60000
    ];

    private static readonly HashSet<string> SecurityEvents = new(StringComparer.Ordinal)
    {
        "ChannelCreated",
        "ChannelDestroyed",
        "ChannelUpdated",
        "RoleCreated",
        "RoleDeleted",
        "RoleUpdated",
        "UserBanned",
        "UserUnbanned",
        "UserJoined",
        "UserLeft",
        "WebhooksUpdated",
        "GuildUpdated",
        "MessagesBulkDeleted",
        "InviteCreated",
        "InviteDeleted",
        "GuildScheduledEventCreated",
        "GuildScheduledEventCancelled",
        "IntegrationCreated",
        "IntegrationDeleted",
        "IntegrationUpdated",
        "AuditLogCreated",
        "GuildStickerCreated",
        "GuildStickerDeleted",
        "GuildStickerUpdated",
        "GuildEmojiCreated",
        "GuildEmojiDeleted",
        "GuildEmojiUpdated"
    };

    private readonly DiscordShardedClient client;

    private readonly BotConfigService config;
    private readonly object swapLock = new();
    private ConcurrentQueue<AnalyticsCommandInvocation> commandRows = new();

    private NonBlocking.ConcurrentDictionary<(DateTime Hour, string Type, string Module, string Hash), ErrorSample>
        errorSamples = new();

    private NonBlocking.ConcurrentDictionary<(DateTime Hour, ulong GuildId, string Feature), (int Count, int Errors)>
        featureActivity = new();

    private NonBlocking.ConcurrentDictionary<(DateTime Hour, ulong GuildId, string EventType), int> guildActivity =
        new();

    private ConcurrentQueue<AnalyticsGuildEventLog> guildEventRows = new();
    private ConcurrentQueue<AnalyticsPageView> pageViewRows = new();

    private NonBlocking.ConcurrentDictionary<SeriesKey, Series> series = new();

    private readonly NonBlocking.ConcurrentDictionary<(string Type, int? Shard), string> eventLabels = new();
    private readonly NonBlocking.ConcurrentDictionary<(string Type, string Module), string> handlerLabels = new();
    private string? botLabel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AnalyticsCollector" /> class.
    /// </summary>
    /// <param name="config">The bot config, read for the enabled flag.</param>
    /// <param name="client">The Discord client, used for the instance identifier.</param>
    public AnalyticsCollector(BotConfigService config, DiscordShardedClient client)
    {
        this.config = config;
        this.client = client;
    }

    /// <inheritdoc />
    public bool Enabled
    {
        get
        {
            var now = Environment.TickCount64;
            if (now - Volatile.Read(ref enabledCheckedAt) < EnabledRefreshMs)
                return enabled;

            Volatile.Write(ref enabledCheckedAt, now);
            enabled = config.Data.AnalyticsEnabled;
            return enabled;
        }
    }

    /// <inheritdoc />
    public string Bot
    {
        get
        {
            var cached = botLabel;
            if (cached is not null) return cached;
            var id = client.CurrentUser?.Id;
            if (id is null) return "starting";
            cached = id.Value.ToString();
            botLabel = cached;
            return cached;
        }
    }

    /// <inheritdoc />
    public void Counter(string metric, double value = 1, params (string Key, string Value)[] labels)
    {
        if (!Enabled) return;
        Observe(metric, MetricKind.Counter, value, labels);
    }

    /// <inheritdoc />
    public void Gauge(string metric, double value, params (string Key, string Value)[] labels)
    {
        if (!Enabled) return;
        Observe(metric, MetricKind.Gauge, value, labels);
    }

    /// <inheritdoc />
    public void Duration(string metric, double milliseconds, params (string Key, string Value)[] labels)
    {
        if (!Enabled) return;
        Observe(metric, MetricKind.Histogram, milliseconds, labels);
    }

    /// <inheritdoc />
    public void Command(CommandSample sample)
    {
        if (!Enabled) return;

        var okLabel = sample.Ok ? "1" : "0";
        var shard = sample.Shard?.ToString() ?? string.Empty;
        var module = sample.Module ?? string.Empty;
        Counter("cmd.count", 1, ("shard", shard), ("kind", sample.Kind), ("module", module),
            ("command", sample.Command), ("ok", okLabel));
        Duration("cmd.duration", sample.DurationMs, ("kind", sample.Kind), ("command", sample.Command));
        if (sample.AckMs is { } ack)
            Duration("cmd.ack", ack, ("kind", sample.Kind), ("command", sample.Command));
        if (!sample.Ok)
            Counter("cmd.errors", 1, ("kind", sample.Kind), ("command", sample.Command),
                ("error", sample.ErrorClass ?? "unknown"));

        if (commandRows.Count >= MaxRawRows) return;

        commandRows.Enqueue(new AnalyticsCommandInvocation
        {
            At = DateTime.UtcNow,
            Bot = Bot,
            Shard = sample.Shard,
            GuildId = sample.OptedOut ? null : sample.GuildId,
            GuildSize = sample.OptedOut ? null : sample.GuildSize,
            Kind = Truncate(sample.Kind, 16),
            Module = string.IsNullOrEmpty(sample.Module) ? null : Truncate(sample.Module, 64),
            Command = Truncate(sample.Command, 128),
            Ok = sample.Ok,
            ErrorClass = sample.Ok || string.IsNullOrEmpty(sample.ErrorClass) ? null : Truncate(sample.ErrorClass, 64),
            ErrorMessage = sample.Ok || string.IsNullOrWhiteSpace(sample.ErrorMessage)
                ? null
                : Truncate(sample.ErrorMessage.Replace('\r', ' ').Replace('\n', ' ').Trim(), 200),
            DurationMs = (int)Math.Min(int.MaxValue, sample.DurationMs),
            AckMs = sample.AckMs is null ? null : (int)Math.Min(int.MaxValue, sample.AckMs.Value),
            Language = string.IsNullOrWhiteSpace(sample.Language) ? null : Truncate(sample.Language, 16)
        });
    }

    /// <inheritdoc />
    public void Event(string eventType, int? shard, int count = 1)
    {
        if (!Enabled || count <= 0) return;
        var bot = Bot;
        if (bot == "starting")
        {
            Counter("ev.count", count, ("shard", shard?.ToString() ?? string.Empty), ("type", eventType));
            return;
        }

        var labels = eventLabels.GetOrAdd((eventType, shard), static (key, bot) =>
            CanonicalLabels([("shard", key.Shard?.ToString() ?? string.Empty), ("type", key.Type)], bot), bot);
        Observe("ev.count", MetricKind.Counter, count, labels);
    }

    /// <inheritdoc />
    public void HandlerDuration(string eventType, string module, double milliseconds)
    {
        if (!Enabled) return;
        var bot = Bot;
        if (bot == "starting")
        {
            Duration("ev.duration", milliseconds, ("type", eventType), ("module", module));
            return;
        }

        var labels = handlerLabels.GetOrAdd((eventType, module), static (key, bot) =>
            CanonicalLabels([("type", key.Type), ("module", key.Module)], bot), bot);
        Observe("ev.duration", MetricKind.Histogram, milliseconds, labels);
    }

    /// <inheritdoc />
    public void GuildEvent(ulong guildId, string eventType, int? shard, bool securityRelevant)
    {
        if (!Enabled) return;

        var hour = HourOf(DateTime.UtcNow);
        guildActivity.AddOrUpdate((hour, guildId, eventType), 1, static (_, current) => current + 1);

        if (!securityRelevant && !SecurityEvents.Contains(eventType)) return;
        if (guildEventRows.Count >= MaxRawRows) return;

        guildEventRows.Enqueue(new AnalyticsGuildEventLog
        {
            At = DateTime.UtcNow,
            GuildId = guildId,
            EventType = Truncate(eventType, 48),
            Bot = Bot,
            Shard = shard
        });
    }

    /// <inheritdoc />
    public void Feature(string feature, ulong? guildId, bool ok = true, string? error = null)
    {
        if (!Enabled) return;

        Counter("feat.count", 1, ("feature", feature), ("ok", ok ? "1" : "0"),
            ("error", ok ? string.Empty : error ?? "unknown"));

        if (guildId is not { } guild) return;

        var key = (HourOf(DateTime.UtcNow), guild, feature);
        featureActivity.AddOrUpdate(key, ok ? (1, 0) : (0, 1),
            (_, current) => ok ? (current.Count + 1, current.Errors) : (current.Count, current.Errors + 1));
    }

    /// <inheritdoc />
    public void Error(Exception exception, string location, string? module = null, ulong? guildId = null,
        int? shard = null)
    {
        if (!Enabled || exception is null) return;

        var inner = Innermost(exception);
        var type = inner.GetType().Name;
        var loc = ErrorLocation(inner) ?? location;
        Counter("err.count", 1, ("shard", shard?.ToString() ?? string.Empty), ("type", type),
            ("module", module ?? string.Empty), ("loc", Truncate(loc, 64)));

        var message = (inner.Message ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        var hash = Hash(message);
        var now = DateTime.UtcNow;
        var key = (HourOf(now), type, module ?? string.Empty, hash);

        errorSamples.AddOrUpdate(key,
            _ => new ErrorSample(shard, Truncate(loc, 120), Truncate(message, 300), 1, now, now, guildId),
            (_, current) => current with
            {
                Count = current.Count + 1, LastSeen = now, LastGuildId = guildId ?? current.LastGuildId
            });
    }

    /// <inheritdoc />
    public void PageView(PageViewSample sample)
    {
        if (!Enabled) return;

        var status = sample.Status / 100 + "xx";
        Counter("page.view", 1, ("route", sample.Route), ("status", status), ("dev", sample.Device ?? string.Empty),
            ("loc", sample.Locale ?? string.Empty));
        Duration("page.duration", sample.DurationMs, ("route", sample.Route));

        if (pageViewRows.Count >= MaxRawRows) return;

        pageViewRows.Enqueue(new AnalyticsPageView
        {
            At = sample.At,
            Route = Truncate(sample.Route, 160),
            Method = Truncate(sample.Method, 8),
            Status = (short)sample.Status,
            DurationMs = sample.DurationMs,
            VisitorHash = string.IsNullOrEmpty(sample.VisitorHash) ? null : Truncate(sample.VisitorHash, 16),
            Locale = string.IsNullOrEmpty(sample.Locale) ? null : Truncate(sample.Locale, 12),
            Device = string.IsNullOrEmpty(sample.Device) ? null : Truncate(sample.Device, 8),
            GuildSize = sample.GuildSize,
            IsOwner = sample.IsOwner
        });
    }

    /// <summary>
    ///     Takes everything collected so far and starts a fresh collection.
    /// </summary>
    /// <returns>The drained observations.</returns>
    public Drained Drain()
    {
        lock (swapLock)
        {
            var drained = new Drained(
                Interlocked.Exchange(ref series, new NonBlocking.ConcurrentDictionary<SeriesKey, Series>()),
                Interlocked.Exchange(ref commandRows, new ConcurrentQueue<AnalyticsCommandInvocation>()),
                Interlocked.Exchange(ref guildEventRows, new ConcurrentQueue<AnalyticsGuildEventLog>()),
                Interlocked.Exchange(ref pageViewRows, new ConcurrentQueue<AnalyticsPageView>()),
                Interlocked.Exchange(ref featureActivity,
                    new NonBlocking.ConcurrentDictionary<(DateTime, ulong, string), (int, int)>()),
                Interlocked.Exchange(ref guildActivity,
                    new NonBlocking.ConcurrentDictionary<(DateTime, ulong, string), int>()),
                Interlocked.Exchange(ref errorSamples,
                    new NonBlocking.ConcurrentDictionary<(DateTime, string, string, string), ErrorSample>()));
            return drained;
        }
    }

    /// <summary>
    ///     How many series and raw rows are waiting to be written.
    /// </summary>
    public (int Series, int Rows) Pending()
    {
        return (series.Count, commandRows.Count + guildEventRows.Count + pageViewRows.Count);
    }

    /// <summary>
    ///     Builds the canonical label string: sorted key=value pairs joined by a pipe, with the bot label added.
    /// </summary>
    /// <param name="labels">The labels.</param>
    /// <param name="bot">The instance identifier.</param>
    /// <returns>The canonical string.</returns>
    public static string CanonicalLabels((string Key, string Value)[] labels, string bot)
    {
        var pairs = new List<(string Key, string Value)>(labels.Length + 1)
        {
            ("bot", bot)
        };
        foreach (var (key, value) in labels)
        {
            if (string.IsNullOrEmpty(key) || key == "bot") continue;
            pairs.Add((key, Sanitize(value)));
        }

        pairs.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));
        var builder = new StringBuilder();
        foreach (var (key, value) in pairs)
        {
            if (builder.Length > 0) builder.Append('|');
            builder.Append(key).Append('=').Append(value);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Parses a canonical label string back into a dictionary.
    /// </summary>
    /// <param name="labels">The canonical string.</param>
    /// <returns>The labels.</returns>
    public static Dictionary<string, string> ParseLabels(string labels)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(labels)) return result;
        foreach (var pair in labels.Split('|'))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            result[pair[..eq]] = pair[(eq + 1)..];
        }

        return result;
    }

    /// <summary>
    ///     Rounds a time down to the start of its minute.
    /// </summary>
    public static DateTime MinuteOf(DateTime at)
    {
        return new DateTime(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0, DateTimeKind.Utc);
    }

    /// <summary>
    ///     Rounds a time down to the start of its hour.
    /// </summary>
    public static DateTime HourOf(DateTime at)
    {
        return new DateTime(at.Year, at.Month, at.Day, at.Hour, 0, 0, DateTimeKind.Utc);
    }

    private void Observe(string metric, MetricKind kind, double value, (string Key, string Value)[] labels)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return;

        Observe(metric, kind, value, CanonicalLabels(labels, Bot));
    }

    private void Observe(string metric, MetricKind kind, double value, string canonicalLabels)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return;

        var key = new SeriesKey(MinuteOf(DateTime.UtcNow), metric, canonicalLabels);
        var current = series;
        if (current.Count >= MaxSeriesPerMinute && !current.ContainsKey(key)) return;

        var entry = current.GetOrAdd(key, static (k, kind) => new Series(kind), kind);
        entry.Add(value);
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var cleaned = value.Replace("|", string.Empty).Replace("=", string.Empty).Trim();
        return Truncate(cleaned, 64);
    }

    private static string Truncate(string value, int max)
    {
        return value.Length <= max ? value : value[..max];
    }

    private static Exception Innermost(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null && current is not AggregateException)
            current = current.InnerException;
        if (current is AggregateException { InnerExceptions.Count: > 0 } aggregate)
            return Innermost(aggregate.InnerExceptions[0]);
        return current;
    }

    private static string? ErrorLocation(Exception exception)
    {
        var trace = new StackTrace(exception, false);
        foreach (var frame in trace.GetFrames())
        {
            var method = frame.GetMethod();
            var type = method?.DeclaringType;
            if (type is null) continue;

            var typeName = type.FullName ?? type.Name;
            if (!typeName.StartsWith("Mewdeko", StringComparison.Ordinal)) continue;

            if (type.IsNested && typeName.Contains('<'))
            {
                var outer = type.DeclaringType;
                var start = typeName.IndexOf('<');
                var end = typeName.IndexOf('>', start);
                var name = end > start ? typeName[(start + 1)..end] : method!.Name;
                return $"{outer?.Name ?? type.Name}.{name}";
            }

            return $"{type.Name}.{method!.Name}";
        }

        return null;
    }

    private static string Hash(string message)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    /// <summary>
    ///     Identifies one series within a minute.
    /// </summary>
    /// <param name="Minute">Start of the minute, UTC.</param>
    /// <param name="Metric">The metric name.</param>
    /// <param name="Labels">The canonical label string.</param>
    public sealed record SeriesKey(DateTime Minute, string Metric, string Labels);

    /// <summary>
    ///     Running aggregate of one series within a minute.
    /// </summary>
    public sealed class Series
    {
        private long count;
        private double sum;
        private double min = double.MaxValue;
        private double max = double.MinValue;
        private double last;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Series" /> class.
        /// </summary>
        /// <param name="kind">What the metric records.</param>
        public Series(MetricKind kind)
        {
            Kind = kind;
            if (kind == MetricKind.Histogram) Hist = new long[HistogramBounds.Length + 1];
        }

        /// <summary>What the metric records.</summary>
        public MetricKind Kind { get; }

        /// <summary>Number of observations.</summary>
        public double Count
        {
            get
            {
                return Interlocked.Read(ref count);
            }
        }

        /// <summary>Sum of the observations.</summary>
        public double Sum
        {
            get
            {
                return Volatile.Read(ref sum);
            }
        }

        /// <summary>Smallest observation.</summary>
        public double Min
        {
            get
            {
                return Volatile.Read(ref min);
            }
        }

        /// <summary>Largest observation.</summary>
        public double Max
        {
            get
            {
                return Volatile.Read(ref max);
            }
        }

        /// <summary>Latest observation.</summary>
        public double Last
        {
            get
            {
                return Volatile.Read(ref last);
            }
        }

        /// <summary>Histogram bucket counts, or null for counters and gauges.</summary>
        public long[]? Hist { get; }

        /// <summary>
        ///     Folds one observation in without taking a lock, so gateway threads never block on it.
        /// </summary>
        /// <param name="value">The value.</param>
        public void Add(double value)
        {
            Interlocked.Increment(ref count);
            AddTo(ref sum, value);
            LowerTo(ref min, value);
            RaiseTo(ref max, value);
            Volatile.Write(ref last, value);
            if (Hist is null) return;

            var index = -1;
            for (var i = 0; i < HistogramBounds.Length; i++)
            {
                if (value > HistogramBounds[i]) continue;
                index = i;
                break;
            }

            Interlocked.Increment(ref Hist[index < 0 ? Hist.Length - 1 : index]);
        }

        private static void AddTo(ref double target, double value)
        {
            var seen = Volatile.Read(ref target);
            while (true)
            {
                var updated = seen + value;
                var previous = Interlocked.CompareExchange(ref target, updated, seen);
                if (previous.Equals(seen)) return;
                seen = previous;
            }
        }

        private static void LowerTo(ref double target, double value)
        {
            var seen = Volatile.Read(ref target);
            while (value < seen)
            {
                var previous = Interlocked.CompareExchange(ref target, value, seen);
                if (previous.Equals(seen)) return;
                seen = previous;
            }
        }

        private static void RaiseTo(ref double target, double value)
        {
            var seen = Volatile.Read(ref target);
            while (value > seen)
            {
                var previous = Interlocked.CompareExchange(ref target, value, seen);
                if (previous.Equals(seen)) return;
                seen = previous;
            }
        }
    }

    /// <summary>
    ///     One distinct error within an hour.
    /// </summary>
    public sealed record ErrorSample(
        int? Shard,
        string Location,
        string Message,
        int Count,
        DateTime FirstSeen,
        DateTime LastSeen,
        ulong? LastGuildId);

    /// <summary>
    ///     Everything drained from the collector in one pass.
    /// </summary>
    public sealed record Drained(
        NonBlocking.ConcurrentDictionary<SeriesKey, Series> Series,
        ConcurrentQueue<AnalyticsCommandInvocation> CommandRows,
        ConcurrentQueue<AnalyticsGuildEventLog> GuildEventRows,
        ConcurrentQueue<AnalyticsPageView> PageViewRows,
        NonBlocking.ConcurrentDictionary<(DateTime Hour, ulong GuildId, string Feature), (int Count, int Errors)>
            FeatureActivity,
        NonBlocking.ConcurrentDictionary<(DateTime Hour, ulong GuildId, string EventType), int> GuildActivity,
        NonBlocking.ConcurrentDictionary<(DateTime Hour, string Type, string Module, string Hash), ErrorSample>
            ErrorSamples);
}