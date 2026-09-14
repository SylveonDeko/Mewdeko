using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     One alert rule evaluated against a metric series and delivered to a Discord webhook.
/// </summary>
[Table("AnalyticsAlertRule")]
public class AnalyticsAlertRule
{
    /// <summary>
    ///     Auto-generated primary key.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Display name.
    /// </summary>
    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Optional description shown in notifications.
    /// </summary>
    [Column("Description")]
    public string? Description { get; set; }

    /// <summary>
    ///     Whether the rule is evaluated.
    /// </summary>
    [Column("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     info, warning or critical.
    /// </summary>
    [Column("Severity")]
    public string Severity { get; set; } = "warning";

    /// <summary>
    ///     The metric evaluated.
    /// </summary>
    [Column("Metric")]
    public string Metric { get; set; } = string.Empty;

    /// <summary>
    ///     JSON object of label filters.
    /// </summary>
    [Column("FiltersJson")]
    public string? FiltersJson { get; set; }

    /// <summary>
    ///     Label to evaluate per value, or null for one series.
    /// </summary>
    [Column("GroupBy")]
    public string? GroupBy { get; set; }

    /// <summary>
    ///     sum, avg, min, max, last, rate, p50, p95 or p99.
    /// </summary>
    [Column("Aggregation")]
    public string Aggregation { get; set; } = "sum";

    /// <summary>
    ///     Window the aggregation spans.
    /// </summary>
    [Column("WindowSeconds")]
    public int WindowSeconds { get; set; }

    /// <summary>
    ///     gt, gte, lt, lte, outside, pct_change, deviates or nodata.
    /// </summary>
    [Column("Comparator")]
    public string Comparator { get; set; } = "gt";

    /// <summary>
    ///     Days of history for pct_change and deviates comparisons.
    /// </summary>
    [Column("BaselineDays")]
    public short? BaselineDays { get; set; }

    /// <summary>
    ///     both, up or down for baseline comparisons.
    /// </summary>
    [Column("Direction")]
    public string? Direction { get; set; }

    /// <summary>
    ///     The threshold.
    /// </summary>
    [Column("Threshold")]
    public double Threshold { get; set; }

    /// <summary>
    ///     Upper threshold for the outside comparator.
    /// </summary>
    [Column("ThresholdHigh")]
    public double? ThresholdHigh { get; set; }

    /// <summary>
    ///     How long the condition must hold before firing.
    /// </summary>
    [Column("ForSeconds")]
    public int ForSeconds { get; set; }

    /// <summary>
    ///     Minimum gap between notifications for the same group.
    /// </summary>
    [Column("CooldownSeconds")]
    public int CooldownSeconds { get; set; } = 900;

    /// <summary>
    ///     Re-notify while still firing after this many seconds, or null to notify once.
    /// </summary>
    [Column("RepeatSeconds")]
    public int? RepeatSeconds { get; set; }

    /// <summary>
    ///     Discord webhook notifications go to.
    /// </summary>
    [Column("WebhookUrl")]
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    ///     Role to mention in notifications.
    /// </summary>
    [Column("MentionRoleId")]
    public ulong? MentionRoleId { get; set; }

    /// <summary>
    ///     Thread to post into.
    /// </summary>
    [Column("ThreadId")]
    public ulong? ThreadId { get; set; }

    /// <summary>
    ///     Whether to notify when the condition clears.
    /// </summary>
    [Column("NotifyOnResolve")]
    public bool NotifyOnResolve { get; set; } = true;

    /// <summary>
    ///     Start of the daily quiet window, minutes from midnight UTC.
    /// </summary>
    [Column("QuietStartMinute")]
    public short? QuietStartMinute { get; set; }

    /// <summary>
    ///     End of the daily quiet window, minutes from midnight UTC.
    /// </summary>
    [Column("QuietEndMinute")]
    public short? QuietEndMinute { get; set; }

    /// <summary>
    ///     Suppress notifications until this time.
    /// </summary>
    [Column("MutedUntil")]
    public DateTime? MutedUntil { get; set; }

    /// <summary>
    ///     Minimum observations in the window before evaluating.
    /// </summary>
    [Column("MinSamples")]
    public int? MinSamples { get; set; }

    /// <summary>
    ///     When the rule was created.
    /// </summary>
    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     When the rule was last changed.
    /// </summary>
    [Column("UpdatedAt")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    ///     The owner who created it.
    /// </summary>
    [Column("CreatedBy")]
    public ulong CreatedBy { get; set; }
}