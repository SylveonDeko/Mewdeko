using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Nightly count of guilds per configured feature or setting value.
/// </summary>
[Table("AnalyticsConfigCensus")]
public class AnalyticsConfigCensus
{
    /// <summary>
    ///     The day, UTC.
    /// </summary>
    [Column("Day", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public DateTime Day { get; set; }

    /// <summary>
    ///     The census metric name.
    /// </summary>
    [Column("Metric", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public string Metric { get; set; } = string.Empty;

    /// <summary>
    ///     The count.
    /// </summary>
    [Column("Value")]
    public double Value { get; set; }
}