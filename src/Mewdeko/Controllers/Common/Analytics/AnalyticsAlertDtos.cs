namespace Mewdeko.Controllers.Common.Analytics;

/// <summary>
///     Body of a create or update of an alert rule.
/// </summary>
/// <param name="Name">Display name, at most 100 characters.</param>
/// <param name="Description">Optional description shown in notifications, at most 500 characters.</param>
/// <param name="Enabled">Whether the rule is evaluated.</param>
/// <param name="Severity">info, warning or critical.</param>
/// <param name="Metric">The metric evaluated.</param>
/// <param name="Filters">Label filters as key to value.</param>
/// <param name="GroupBy">Label to evaluate per value, or null for one series.</param>
/// <param name="Aggregation">sum, avg, min, max, last, rate, count, p50, p95 or p99.</param>
/// <param name="WindowSeconds">Window the aggregation spans, 60 to 86400.</param>
/// <param name="Comparator">gt, gte, lt, lte, outside, pct_change, deviates or nodata.</param>
/// <param name="BaselineDays">Days of history for pct_change and deviates.</param>
/// <param name="Direction">both, up or down for baseline comparisons.</param>
/// <param name="Threshold">The threshold.</param>
/// <param name="ThresholdHigh">Upper threshold for outside.</param>
/// <param name="ForSeconds">How long the condition must hold before firing.</param>
/// <param name="CooldownSeconds">Minimum gap between firing notifications for the same group.</param>
/// <param name="RepeatSeconds">Re-notify while still firing after this many seconds, or null to notify once.</param>
/// <param name="WebhookUrl">Discord webhook notifications go to.</param>
/// <param name="MentionRoleId">Role to mention, as a string snowflake.</param>
/// <param name="ThreadId">Thread to post into, as a string snowflake.</param>
/// <param name="NotifyOnResolve">Whether to notify when the condition clears.</param>
/// <param name="QuietStartMinute">Start of the daily quiet window, minutes from midnight UTC.</param>
/// <param name="QuietEndMinute">End of the daily quiet window, minutes from midnight UTC.</param>
/// <param name="MinSamples">Minimum observations in the window before evaluating.</param>
public sealed record AlertRuleRequest(
    string Name,
    string? Description,
    bool Enabled = true,
    string Severity = "warning",
    string Metric = "",
    Dictionary<string, string>? Filters = null,
    string? GroupBy = null,
    string Aggregation = "sum",
    int WindowSeconds = 300,
    string Comparator = "gt",
    short? BaselineDays = null,
    string? Direction = null,
    double Threshold = 0,
    double? ThresholdHigh = null,
    int ForSeconds = 0,
    int CooldownSeconds = 900,
    int? RepeatSeconds = null,
    string WebhookUrl = "",
    string? MentionRoleId = null,
    string? ThreadId = null,
    bool NotifyOnResolve = true,
    short? QuietStartMinute = null,
    short? QuietEndMinute = null,
    int? MinSamples = null);

/// <summary>
///     Live state of one rule for one group value.
/// </summary>
/// <param name="GroupKey">The group value, or an empty string when ungrouped.</param>
/// <param name="State">ok, pending or firing.</param>
/// <param name="Since">When the state began.</param>
/// <param name="LastValue">The last evaluated value.</param>
/// <param name="LastNotifiedAt">When a notification was last sent.</param>
public sealed record AlertStateInfo(
    string GroupKey,
    string State,
    DateTime Since,
    double? LastValue,
    DateTime? LastNotifiedAt);

/// <summary>
///     An alert rule as returned to the dashboard.
/// </summary>
/// <param name="Id">The rule id.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Enabled">Whether the rule is evaluated.</param>
/// <param name="Severity">info, warning or critical.</param>
/// <param name="Metric">The metric evaluated.</param>
/// <param name="Filters">Label filters as key to value.</param>
/// <param name="GroupBy">Label evaluated per value, or null.</param>
/// <param name="Aggregation">The aggregation.</param>
/// <param name="WindowSeconds">Window the aggregation spans.</param>
/// <param name="Comparator">The comparator.</param>
/// <param name="BaselineDays">Days of history for baseline comparisons.</param>
/// <param name="Direction">both, up or down.</param>
/// <param name="Threshold">The threshold.</param>
/// <param name="ThresholdHigh">Upper threshold for outside.</param>
/// <param name="ForSeconds">How long the condition must hold before firing.</param>
/// <param name="CooldownSeconds">Minimum gap between firing notifications.</param>
/// <param name="RepeatSeconds">Repeat interval while firing, or null.</param>
/// <param name="WebhookUrl">Discord webhook notifications go to.</param>
/// <param name="MentionRoleId">Role to mention, as a string snowflake.</param>
/// <param name="ThreadId">Thread to post into, as a string snowflake.</param>
/// <param name="NotifyOnResolve">Whether the resolve is notified.</param>
/// <param name="QuietStartMinute">Start of the quiet window, minutes from midnight UTC.</param>
/// <param name="QuietEndMinute">End of the quiet window, minutes from midnight UTC.</param>
/// <param name="MutedUntil">Notifications are suppressed until this time.</param>
/// <param name="MinSamples">Minimum observations before evaluating.</param>
/// <param name="CreatedAt">When the rule was created.</param>
/// <param name="UpdatedAt">When the rule was last changed.</param>
/// <param name="CreatedBy">The owner who created it, as a string snowflake.</param>
/// <param name="States">Live states per group, firing first.</param>
/// <param name="FiredLast30Days">Firing transitions in the last 30 days.</param>
public sealed record AlertRuleResponse(
    int Id,
    string Name,
    string? Description,
    bool Enabled,
    string Severity,
    string Metric,
    Dictionary<string, string> Filters,
    string? GroupBy,
    string Aggregation,
    int WindowSeconds,
    string Comparator,
    short? BaselineDays,
    string? Direction,
    double Threshold,
    double? ThresholdHigh,
    int ForSeconds,
    int CooldownSeconds,
    int? RepeatSeconds,
    string WebhookUrl,
    string? MentionRoleId,
    string? ThreadId,
    bool NotifyOnResolve,
    short? QuietStartMinute,
    short? QuietEndMinute,
    DateTime? MutedUntil,
    int? MinSamples,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string CreatedBy,
    List<AlertStateInfo> States,
    int FiredLast30Days);

/// <summary>
///     One alert state transition.
/// </summary>
/// <param name="Id">The event id.</param>
/// <param name="RuleId">The rule.</param>
/// <param name="RuleName">The rule's name, or "(deleted rule)".</param>
/// <param name="Severity">The rule's severity.</param>
/// <param name="GroupKey">The group value.</param>
/// <param name="At">When the transition happened.</param>
/// <param name="FromState">The previous state.</param>
/// <param name="ToState">The new state: pending, firing, ok or resolved.</param>
/// <param name="Value">The value that caused the transition.</param>
/// <param name="Threshold">The rule's threshold at the time.</param>
/// <param name="Notified">Whether a webhook notification was delivered.</param>
public sealed record AlertEventResponse(
    long Id,
    int RuleId,
    string RuleName,
    string Severity,
    string GroupKey,
    DateTime At,
    string FromState,
    string ToState,
    double? Value,
    double Threshold,
    bool Notified);

/// <summary>
///     A page of alert events, newest first.
/// </summary>
/// <param name="Items">The events on this page.</param>
/// <param name="Total">Total events matching the filter.</param>
/// <param name="Page">The 1-based page.</param>
/// <param name="PageSize">The page size.</param>
public sealed record AlertEventsPageResponse(List<AlertEventResponse> Items, int Total, int Page, int PageSize);

/// <summary>
///     One rule group currently firing.
/// </summary>
/// <param name="RuleId">The rule.</param>
/// <param name="RuleName">The rule's name.</param>
/// <param name="Severity">The rule's severity.</param>
/// <param name="Metric">The metric evaluated.</param>
/// <param name="GroupKey">The group value.</param>
/// <param name="Since">When it started firing.</param>
/// <param name="LastValue">The last evaluated value.</param>
/// <param name="Threshold">The rule's threshold.</param>
public sealed record AlertFiringResponse(
    int RuleId,
    string RuleName,
    string Severity,
    string Metric,
    string GroupKey,
    DateTime Since,
    double? LastValue,
    double Threshold);

/// <summary>
///     A fixed threshold drawn on a chart of the metric.
/// </summary>
/// <param name="Id">The rule.</param>
/// <param name="Name">The rule's name.</param>
/// <param name="Threshold">The threshold.</param>
/// <param name="ThresholdHigh">The upper threshold for outside.</param>
/// <param name="Comparator">The comparator.</param>
/// <param name="Severity">The rule's severity.</param>
public sealed record AlertBandResponse(
    int Id,
    string Name,
    double Threshold,
    double? ThresholdHigh,
    string Comparator,
    string Severity);

/// <summary>
///     Result of a mute.
/// </summary>
/// <param name="MutedUntil">When notifications resume, or null when unmuted.</param>
public sealed record AlertMuteResponse(DateTime? MutedUntil);

/// <summary>
///     Result of sending a test notification or the digest.
/// </summary>
/// <param name="Success">Whether Discord accepted the message.</param>
public sealed record AlertSendResponse(bool Success);