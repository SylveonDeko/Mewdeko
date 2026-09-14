using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A metric the pipeline has seen, with the labels and sample values it carries.
/// </summary>
[Table("AnalyticsMetricRegistry")]
public class AnalyticsMetricRegistry
{
    /// <summary>
    ///     The metric name.
    /// </summary>
    [Column("Metric", IsPrimaryKey = true)]
    public string Metric { get; set; } = string.Empty;

    /// <summary>
    ///     counter, gauge or histogram.
    /// </summary>
    [Column("Kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    ///     JSON object of label key to an array of sample values.
    /// </summary>
    [Column("LabelsJson")]
    public string LabelsJson { get; set; } = "{}";

    /// <summary>
    ///     When the metric was last written.
    /// </summary>
    [Column("LastSeen")]
    public DateTime LastSeen { get; set; }
}