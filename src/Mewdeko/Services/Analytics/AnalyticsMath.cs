using DataModel;

namespace Mewdeko.Services.Analytics;

/// <summary>
///     Aggregation and percentile arithmetic shared by the query service and the alert evaluator, so both
///     read a window of buckets the same way.
/// </summary>
public static class AnalyticsMath
{
    /// <summary>
    ///     The aggregations a series can be reduced with.
    /// </summary>
    public static readonly string[] Aggregations =
    [
        "sum", "avg", "min", "max", "last", "rate", "count", "p50", "p95", "p99"
    ];

    /// <summary>
    ///     Whether the aggregation name is one of <see cref="Aggregations" />.
    /// </summary>
    /// <param name="aggregation">The name.</param>
    public static bool IsAggregation(string? aggregation)
    {
        return aggregation is not null && Array.IndexOf(Aggregations, aggregation) >= 0;
    }

    /// <summary>
    ///     Parses the comma separated histogram column into bucket counts.
    /// </summary>
    /// <param name="hist">The stored histogram, or null.</param>
    /// <returns>The counts, one per bound plus the overflow bucket, or null when the text is empty.</returns>
    public static long[]? ParseHist(string? hist)
    {
        if (string.IsNullOrEmpty(hist)) return null;
        var parts = hist.Split(',');
        var result = new long[AnalyticsCollector.HistogramBounds.Length + 1];
        for (var i = 0; i < parts.Length && i < result.Length; i++)
            if (long.TryParse(parts[i], out var n))
                result[i] = n;
        return result;
    }

    /// <summary>
    ///     Adds one histogram into another element wise.
    /// </summary>
    /// <param name="into">The accumulator, or null to start from the source.</param>
    /// <param name="source">The histogram to add.</param>
    /// <returns>The accumulator.</returns>
    public static long[]? MergeHist(long[]? into, long[]? source)
    {
        if (source is null) return into;
        if (into is null) return (long[])source.Clone();
        for (var i = 0; i < into.Length && i < source.Length; i++) into[i] += source[i];
        return into;
    }

    /// <summary>
    ///     Interpolates a percentile from histogram bucket counts over <see cref="AnalyticsCollector.HistogramBounds" />.
    /// </summary>
    /// <param name="hist">The bucket counts.</param>
    /// <param name="p">The percentile, either as a fraction (0.95) or in percent (95).</param>
    /// <returns>The estimated value in the histogram's unit, or null when the histogram is empty.</returns>
    public static double? Percentile(long[]? hist, double p)
    {
        if (hist is null || hist.Length == 0) return null;
        if (p > 1) p /= 100;
        p = Math.Clamp(p, 0, 1);

        long total = 0;
        foreach (var n in hist) total += n;
        if (total <= 0) return null;

        var target = p * total;
        var bounds = AnalyticsCollector.HistogramBounds;
        double cumulative = 0;
        for (var i = 0; i < hist.Length; i++)
        {
            if (hist[i] <= 0) continue;
            var next = cumulative + hist[i];
            if (next < target && i < hist.Length - 1)
            {
                cumulative = next;
                continue;
            }

            var lower = i == 0 ? 0 : bounds[Math.Min(i - 1, bounds.Length - 1)];
            var upper = i < bounds.Length ? bounds[i] : bounds[^1] * 2;
            var fraction = Math.Clamp((target - cumulative) / hist[i], 0, 1);
            return lower + (upper - lower) * fraction;
        }

        return bounds[^1] * 2;
    }

    /// <summary>
    ///     Reduces a set of buckets belonging to one series to a single value.
    /// </summary>
    /// <param name="aggregation">One of <see cref="Aggregations" />.</param>
    /// <param name="rows">The buckets, in any order.</param>
    /// <param name="windowSeconds">
    ///     The span the buckets cover, used by <c>rate</c>; when null the summed bucket widths are used.
    /// </param>
    /// <returns>The value, or null when there is nothing to aggregate.</returns>
    public static double? Aggregate(string aggregation, IReadOnlyCollection<AnalyticsBucket> rows,
        double? windowSeconds = null)
    {
        if (rows.Count == 0) return aggregation == "count" ? 0 : null;

        switch (aggregation)
        {
            case "sum":
                return rows.Sum(r => r.Sum);
            case "count":
                return rows.Sum(r => r.Count);
            case "avg":
            {
                var count = rows.Sum(r => r.Count);
                return count > 0 ? rows.Sum(r => r.Sum) / count : null;
            }
            case "min":
            {
                var values = rows.Where(r => r.Min is not null).Select(r => r.Min!.Value).ToList();
                return values.Count > 0 ? values.Min() : null;
            }
            case "max":
            {
                var values = rows.Where(r => r.Max is not null).Select(r => r.Max!.Value).ToList();
                return values.Count > 0 ? values.Max() : null;
            }
            case "last":
            {
                var latest = rows.Where(r => r.Last is not null).OrderByDescending(r => r.Bucket).FirstOrDefault();
                return latest?.Last;
            }
            case "rate":
            {
                var seconds = windowSeconds ?? rows.Sum(r => r.Resolution * 60d);
                return seconds > 0 ? rows.Sum(r => r.Sum) / seconds : null;
            }
            case "p50":
                return Percentile(MergedHist(rows), 0.50);
            case "p95":
                return Percentile(MergedHist(rows), 0.95);
            case "p99":
                return Percentile(MergedHist(rows), 0.99);
            default:
                return null;
        }
    }

    /// <summary>
    ///     Merges the histograms of a set of buckets.
    /// </summary>
    /// <param name="rows">The buckets.</param>
    /// <returns>The summed histogram, or null when none of the buckets carry one.</returns>
    public static long[]? MergedHist(IEnumerable<AnalyticsBucket> rows)
    {
        long[]? merged = null;
        foreach (var row in rows) merged = MergeHist(merged, ParseHist(row.Hist));
        return merged;
    }

    /// <summary>
    ///     Picks the coarsest bucket resolution that still gives a useful number of points over a span.
    /// </summary>
    /// <param name="span">The span queried.</param>
    /// <returns>1, 5 or 60.</returns>
    public static short ResolutionFor(TimeSpan span)
    {
        if (span <= TimeSpan.FromHours(6)) return 1;
        return span <= TimeSpan.FromDays(3) ? (short)5 : (short)60;
    }
}