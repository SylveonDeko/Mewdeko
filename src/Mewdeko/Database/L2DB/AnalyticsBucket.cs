using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     One time bucket of a metric at a given resolution, keyed by its canonical label string.
/// </summary>
[Table("AnalyticsBucket")]
public class AnalyticsBucket
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public long Id { get; set; }

    /// <summary>
    ///     Bucket width in minutes: 1, 5 or 60.
    /// </summary>
    [Column("Resolution")]
    public short Resolution { get; set; }

    /// <summary>
    ///     Start of the bucket in UTC, aligned to the resolution.
    /// </summary>
    [Column("Bucket")]
    public DateTime Bucket { get; set; }

    /// <summary>
    ///     The metric name.
    /// </summary>
    [Column("Metric")]
    public string Metric { get; set; } = string.Empty;

    /// <summary>
    ///     Canonical label string: sorted key=value pairs joined by a pipe.
    /// </summary>
    [Column("Labels")]
    public string Labels { get; set; } = string.Empty;

    /// <summary>
    ///     Number of observations in the bucket.
    /// </summary>
    [Column("Count")]
    public double Count { get; set; }

    /// <summary>
    ///     Sum of the observed values.
    /// </summary>
    [Column("Sum")]
    public double Sum { get; set; }

    /// <summary>
    ///     Smallest observed value.
    /// </summary>
    [Column("Min")]
    public double? Min { get; set; }

    /// <summary>
    ///     Largest observed value.
    /// </summary>
    [Column("Max")]
    public double? Max { get; set; }

    /// <summary>
    ///     Last observed value, used for gauges.
    /// </summary>
    [Column("Last")]
    public double? Last { get; set; }

    /// <summary>
    ///     Comma separated histogram bucket counts for duration metrics.
    /// </summary>
    [Column("Hist")]
    public string? Hist { get; set; }
}