using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     A metric's total for one day, kept for long range charts.
/// </summary>
[Table("AnalyticsDailyTotal")]
public class AnalyticsDailyTotal
{
    /// <summary>
    ///     The day, UTC.
    /// </summary>
    [Column("Day", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public DateTime Day { get; set; }

    /// <summary>
    ///     The bot instance.
    /// </summary>
    [Column("Bot", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public string Bot { get; set; } = string.Empty;

    /// <summary>
    ///     The metric name.
    /// </summary>
    [Column("Metric", IsPrimaryKey = true, PrimaryKeyOrder = 2)]
    public string Metric { get; set; } = string.Empty;

    /// <summary>
    ///     The day's total.
    /// </summary>
    [Column("Value")]
    public double Value { get; set; }
}