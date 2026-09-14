using System.Globalization;

namespace Mewdeko.Services.Analytics;

/// <summary>
///     A UTC time range for an analytics query.
/// </summary>
/// <param name="From">Inclusive start.</param>
/// <param name="To">Exclusive end.</param>
public readonly record struct AnalyticsRange(DateTime From, DateTime To)
{
    /// <summary>
    ///     Length of the range.
    /// </summary>
    public TimeSpan Span
    {
        get
        {
            return To - From;
        }
    }

    /// <summary>
    ///     Builds a range from explicit bounds or a named range, defaulting to the last 24 hours.
    /// </summary>
    /// <param name="from">ISO start, or null.</param>
    /// <param name="to">ISO end, or null for now.</param>
    /// <param name="range">15m, 1h, 6h, 24h, 7d or 30d.</param>
    /// <returns>The range.</returns>
    public static AnalyticsRange Parse(string? from, string? to, string? range)
    {
        var now = DateTime.UtcNow;
        var end = ParseTime(to) ?? now;
        var start = ParseTime(from);
        if (start is null)
        {
            var span = range switch
            {
                "15m" => TimeSpan.FromMinutes(15),
                "1h" => TimeSpan.FromHours(1),
                "6h" => TimeSpan.FromHours(6),
                "7d" => TimeSpan.FromDays(7),
                "30d" => TimeSpan.FromDays(30),
                _ => TimeSpan.FromHours(24)
            };
            start = end - span;
        }

        if (start >= end) start = end.AddMinutes(-15);
        if (end - start > TimeSpan.FromDays(400)) start = end.AddDays(-400);
        return new AnalyticsRange(start.Value, end);
    }

    private static DateTime? ParseTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
            return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
        return null;
    }
}

/// <summary>
///     A query over the bucket table.
/// </summary>
/// <param name="Metric">The metric name.</param>
/// <param name="Range">The time range.</param>
/// <param name="Filters">Exact label matches, including bot and shard.</param>
/// <param name="GroupBy">Label to split series on, or null for one series.</param>
/// <param name="Aggregation">sum, avg, min, max, last, rate, p50, p95, p99 or count.</param>
/// <param name="MaxSeries">Series to keep before folding into Other.</param>
/// <param name="Resolution">Forced bucket width in minutes, or null to pick from the range.</param>
public sealed record SeriesQuery(
    string Metric,
    AnalyticsRange Range,
    IReadOnlyDictionary<string, string> Filters,
    string? GroupBy,
    string Aggregation,
    int MaxSeries,
    short? Resolution);

/// <summary>
///     Filters for the raw command invocation table.
/// </summary>
/// <param name="Range">The time range.</param>
/// <param name="Bot">Instance filter.</param>
/// <param name="Shard">Shard filter.</param>
/// <param name="Kind">Invocation kind filter.</param>
/// <param name="Module">Module filter.</param>
/// <param name="Command">Command filter.</param>
/// <param name="GuildId">Guild filter.</param>
/// <param name="Ok">Success filter.</param>
public sealed record CommandFilter(
    AnalyticsRange Range,
    string? Bot,
    int? Shard,
    string? Kind,
    string? Module,
    string? Command,
    ulong? GuildId,
    bool? Ok);