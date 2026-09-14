using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     One alert state transition.
/// </summary>
[Table("AnalyticsAlertEvent")]
public class AnalyticsAlertEvent
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public long Id { get; set; }

    /// <summary>
    ///     The rule.
    /// </summary>
    [Column("RuleId")]
    public int RuleId { get; set; }

    /// <summary>
    ///     The group value, or an empty string for ungrouped rules.
    /// </summary>
    [Column("GroupKey")]
    public string GroupKey { get; set; } = string.Empty;

    /// <summary>
    ///     When the transition happened.
    /// </summary>
    [Column("At")]
    public DateTime At { get; set; }

    /// <summary>
    ///     The previous state.
    /// </summary>
    [Column("FromState")]
    public string FromState { get; set; } = string.Empty;

    /// <summary>
    ///     The new state.
    /// </summary>
    [Column("ToState")]
    public string ToState { get; set; } = string.Empty;

    /// <summary>
    ///     The value that caused the transition.
    /// </summary>
    [Column("Value")]
    public double? Value { get; set; }

    /// <summary>
    ///     The threshold at the time.
    /// </summary>
    [Column("Threshold")]
    public double Threshold { get; set; }

    /// <summary>
    ///     Whether a webhook notification was sent.
    /// </summary>
    [Column("Notified")]
    public bool Notified { get; set; }
}