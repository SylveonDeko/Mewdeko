using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Live state of one alert rule for one group value.
/// </summary>
[Table("AnalyticsAlertState")]
public class AnalyticsAlertState
{
    /// <summary>
    ///     The rule.
    /// </summary>
    [Column("RuleId", IsPrimaryKey = true, PrimaryKeyOrder = 0)]
    public int RuleId { get; set; }

    /// <summary>
    ///     The group value, or an empty string for ungrouped rules.
    /// </summary>
    [Column("GroupKey", IsPrimaryKey = true, PrimaryKeyOrder = 1)]
    public string GroupKey { get; set; } = string.Empty;

    /// <summary>
    ///     ok, pending, firing or nodata.
    /// </summary>
    [Column("State")]
    public string State { get; set; } = "ok";

    /// <summary>
    ///     When the current state began.
    /// </summary>
    [Column("Since")]
    public DateTime Since { get; set; }

    /// <summary>
    ///     The last evaluated value.
    /// </summary>
    [Column("LastValue")]
    public double? LastValue { get; set; }

    /// <summary>
    ///     When a notification was last sent for this group.
    /// </summary>
    [Column("LastNotifiedAt")]
    public DateTime? LastNotifiedAt { get; set; }

    /// <summary>
    ///     When the condition first started breaching, for the for-duration check.
    /// </summary>
    [Column("BreachingSince")]
    public DateTime? BreachingSince { get; set; }
}