using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Controllers.Common.Analytics;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services.Impl;

namespace Mewdeko.Services.Analytics;

/// <summary>
///     Stores alert rules, evaluates them against the metric buckets every minute and delivers Discord webhook
///     notifications on state changes.
/// </summary>
public sealed partial class AnalyticsAlertService : INService, IReadyExecutor, IDisposable
{
    private const int MaxRowsPerWindow = 100000;

    /// <summary>
    ///     The allowed severities.
    /// </summary>
    public static readonly string[] Severities = ["info", "warning", "critical"];

    /// <summary>
    ///     The allowed comparators.
    /// </summary>
    public static readonly string[] Comparators =
    [
        "gt", "gte", "lt", "lte", "outside", "pct_change", "deviates", "nodata"
    ];

    /// <summary>
    ///     The allowed baseline directions.
    /// </summary>
    public static readonly string[] Directions = ["both", "up", "down"];

    private static readonly TimeSpan EvaluationInterval = TimeSpan.FromSeconds(60);

    private readonly AnalyticsCollector collector;
    private readonly BotCredentials credentials;
    private readonly IDataConnectionFactory dbFactory;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly IHttpClientFactory httpFactory;
    private readonly ILogger<AnalyticsAlertService> logger;
    private bool disposed;

    private Timer? timer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AnalyticsAlertService" /> class.
    /// </summary>
    /// <param name="collector">The collector, read for the enabled flag.</param>
    /// <param name="credentials">Bot credentials, for the master flag and dashboard URL.</param>
    /// <param name="dbFactory">Creates database connections.</param>
    /// <param name="httpFactory">Creates the HTTP client used for webhooks.</param>
    /// <param name="logger">Records evaluation and delivery failures.</param>
    public AnalyticsAlertService(AnalyticsCollector collector, BotCredentials credentials,
        IDataConnectionFactory dbFactory, IHttpClientFactory httpFactory, ILogger<AnalyticsAlertService> logger)
    {
        this.collector = collector;
        this.credentials = credentials;
        this.dbFactory = dbFactory;
        this.httpFactory = httpFactory;
        this.logger = logger;
    }

    /// <summary>
    ///     When the last evaluation pass finished.
    /// </summary>
    public DateTime? LastEvaluatedAt { get; private set; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timer?.Dispose();
        gate.Dispose();
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        if (credentials.IsMasterInstance)
            timer = new Timer(_ => _ = EvaluateAsync(), null, TimeSpan.FromSeconds(90), EvaluationInterval);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns every rule, newest first.
    /// </summary>
    public async Task<List<AnalyticsAlertRule>> ListRulesAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        return await db.AnalyticsAlertRules.OrderByDescending(r => r.Id).ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Returns one rule.
    /// </summary>
    /// <param name="id">The rule id.</param>
    public async Task<AnalyticsAlertRule?> GetRuleAsync(int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        return await db.AnalyticsAlertRules.FirstOrDefaultAsync(r => r.Id == id).ConfigureAwait(false);
    }

    /// <summary>
    ///     Returns the live states of every rule, keyed by rule id, firing first.
    /// </summary>
    public async Task<Dictionary<int, List<AlertStateInfo>>> StatesAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var states = await db.AnalyticsAlertStates.ToListAsync().ConfigureAwait(false);
        return states
            .GroupBy(s => s.RuleId)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(s => StateOrder(s.State))
                .ThenBy(s => s.GroupKey, StringComparer.Ordinal)
                .Select(s => new AlertStateInfo(s.GroupKey, s.State, s.Since, s.LastValue, s.LastNotifiedAt))
                .ToList());
    }

    /// <summary>
    ///     Counts firing transitions per rule over the last <paramref name="days" /> days.
    /// </summary>
    /// <param name="days">How far back to count.</param>
    public async Task<Dictionary<int, int>> FiredCountsAsync(int days = 30)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var since = DateTime.UtcNow.AddDays(-days);
        var counts = await db.AnalyticsAlertEvents
            .Where(e => e.ToState == "firing" && e.At >= since)
            .GroupBy(e => e.RuleId)
            .Select(g => new
            {
                g.Key, Count = g.Count()
            })
            .ToListAsync().ConfigureAwait(false);
        return counts.ToDictionary(c => c.Key, c => c.Count);
    }

    /// <summary>
    ///     Inserts a rule.
    /// </summary>
    /// <param name="rule">The rule; Id, CreatedAt and UpdatedAt are set here.</param>
    /// <returns>The rule with its id.</returns>
    public async Task<AnalyticsAlertRule> CreateRuleAsync(AnalyticsAlertRule rule)
    {
        Normalize(rule);
        rule.CreatedAt = DateTime.UtcNow;
        rule.UpdatedAt = rule.CreatedAt;
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        rule.Id = await db.InsertWithInt32IdentityAsync(rule).ConfigureAwait(false);
        return rule;
    }

    /// <summary>
    ///     Applies the editable fields of <paramref name="changes" /> to an existing rule.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="changes">The new values.</param>
    /// <returns>The updated rule, or null when it does not exist.</returns>
    public async Task<AnalyticsAlertRule?> UpdateRuleAsync(int id, AnalyticsAlertRule changes)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var existing = await db.AnalyticsAlertRules.FirstOrDefaultAsync(r => r.Id == id).ConfigureAwait(false);
        if (existing is null) return null;

        Normalize(changes);
        existing.Name = changes.Name;
        existing.Description = changes.Description;
        existing.Enabled = changes.Enabled;
        existing.Severity = changes.Severity;
        existing.Metric = changes.Metric;
        existing.FiltersJson = changes.FiltersJson;
        existing.GroupBy = changes.GroupBy;
        existing.Aggregation = changes.Aggregation;
        existing.WindowSeconds = changes.WindowSeconds;
        existing.Comparator = changes.Comparator;
        existing.BaselineDays = changes.BaselineDays;
        existing.Direction = changes.Direction;
        existing.Threshold = changes.Threshold;
        existing.ThresholdHigh = changes.ThresholdHigh;
        existing.ForSeconds = changes.ForSeconds;
        existing.CooldownSeconds = changes.CooldownSeconds;
        existing.RepeatSeconds = changes.RepeatSeconds;
        existing.WebhookUrl = changes.WebhookUrl;
        existing.MentionRoleId = changes.MentionRoleId;
        existing.ThreadId = changes.ThreadId;
        existing.NotifyOnResolve = changes.NotifyOnResolve;
        existing.QuietStartMinute = changes.QuietStartMinute;
        existing.QuietEndMinute = changes.QuietEndMinute;
        existing.MinSamples = changes.MinSamples;
        existing.UpdatedAt = DateTime.UtcNow;
        await db.UpdateAsync(existing).ConfigureAwait(false);

        if (!existing.Enabled)
            await db.AnalyticsAlertStates.Where(s => s.RuleId == id).DeleteAsync().ConfigureAwait(false);

        return existing;
    }

    /// <summary>
    ///     Deletes a rule with its states.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <returns>Whether a rule was deleted.</returns>
    public async Task<bool> DeleteRuleAsync(int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        await db.AnalyticsAlertStates.Where(s => s.RuleId == id).DeleteAsync().ConfigureAwait(false);
        var removed = await db.AnalyticsAlertRules.Where(r => r.Id == id).DeleteAsync().ConfigureAwait(false);
        return removed > 0;
    }

    /// <summary>
    ///     Suppresses notifications for a rule.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="minutes">How long to mute; zero or less unmutes. Capped at 30 days.</param>
    /// <returns>Whether the rule exists and when the mute ends.</returns>
    public async Task<(bool Found, DateTime? MutedUntil)> MuteAsync(int id, int minutes)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var rule = await db.AnalyticsAlertRules.FirstOrDefaultAsync(r => r.Id == id).ConfigureAwait(false);
        if (rule is null) return (false, null);

        rule.MutedUntil = minutes <= 0 ? null : DateTime.UtcNow.AddMinutes(Math.Min(minutes, 43200));
        rule.UpdatedAt = DateTime.UtcNow;
        await db.UpdateAsync(rule).ConfigureAwait(false);
        return (true, rule.MutedUntil);
    }

    /// <summary>
    ///     Sends a sample notification for a rule to its webhook.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>Whether Discord accepted it.</returns>
    public Task<bool> TestAsync(AnalyticsAlertRule rule)
    {
        var notification = new Notification(rule, string.Empty, "test", rule.Threshold, DateTime.UtcNow, null);
        return SendNotificationAsync(notification);
    }

    /// <summary>
    ///     Returns every rule group currently firing.
    /// </summary>
    public async Task<List<AlertFiringResponse>> FiringAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var rows = await (from s in db.AnalyticsAlertStates
                join r in db.AnalyticsAlertRules on s.RuleId equals r.Id
                where s.State == "firing"
                orderby s.Since
                select new AlertFiringResponse(r.Id, r.Name, r.Severity, r.Metric, s.GroupKey, s.Since,
                    s.LastValue, r.Threshold))
            .ToListAsync().ConfigureAwait(false);
        return rows;
    }

    /// <summary>
    ///     Returns a page of alert events, newest first.
    /// </summary>
    /// <param name="page">The 1-based page.</param>
    /// <param name="pageSize">The page size, 1 to 200.</param>
    /// <param name="ruleId">Optional rule filter.</param>
    public async Task<AlertEventsPageResponse> EventsAsync(int page, int pageSize, int? ruleId)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var query = db.AnalyticsAlertEvents.AsQueryable();
        if (ruleId is { } rid) query = query.Where(e => e.RuleId == rid);

        var total = await query.CountAsync().ConfigureAwait(false);
        var items = await (from e in query
                join r in db.AnalyticsAlertRules on e.RuleId equals r.Id into rules
                from r in rules.DefaultIfEmpty()
                orderby e.At descending, e.Id descending
                select new AlertEventResponse(e.Id, e.RuleId, r != null ? r.Name : "(deleted rule)",
                    r != null ? r.Severity : "info", e.GroupKey, e.At, e.FromState, e.ToState, e.Value, e.Threshold,
                    e.Notified))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync().ConfigureAwait(false);

        return new AlertEventsPageResponse(items, total, page, pageSize);
    }

    /// <summary>
    ///     Returns the fixed thresholds of enabled rules on a metric, for drawing on charts.
    /// </summary>
    /// <param name="metric">The metric.</param>
    public async Task<List<AlertBandResponse>> BandsAsync(string metric)
    {
        await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
        var rules = await db.AnalyticsAlertRules
            .Where(r => r.Enabled && r.Metric == metric &&
                        (r.Comparator == "gt" || r.Comparator == "gte" || r.Comparator == "lt" ||
                         r.Comparator == "lte" || r.Comparator == "outside"))
            .OrderBy(r => r.Id)
            .ToListAsync().ConfigureAwait(false);
        return rules.Select(r =>
                new AlertBandResponse(r.Id, r.Name, r.Threshold, r.ThresholdHigh, r.Comparator, r.Severity))
            .ToList();
    }

    /// <summary>
    ///     Checks a rule for invalid values.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The first problem found, or null when the rule is valid.</returns>
    public static string? Validate(AnalyticsAlertRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Name) || rule.Name.Trim().Length > 100)
            return "A rule needs a name of at most 100 characters.";
        if (rule.Description is { Length: > 500 })
            return "The description is limited to 500 characters.";
        if (string.IsNullOrWhiteSpace(rule.Metric) || rule.Metric.Trim().Length > 64)
            return "Pick a metric.";
        if (Array.IndexOf(Severities, rule.Severity) < 0)
            return "Severity must be info, warning or critical.";
        if (!AnalyticsMath.IsAggregation(rule.Aggregation))
            return "Unknown aggregation.";
        if (Array.IndexOf(Comparators, rule.Comparator) < 0)
            return "Unknown comparator.";
        if (rule.WindowSeconds is < 60 or > 86400)
            return "The window must be between 60 and 86400 seconds.";
        if (double.IsNaN(rule.Threshold) || double.IsInfinity(rule.Threshold))
            return "The threshold must be a number.";
        if (rule.Comparator == "outside" && (rule.ThresholdHigh is null || rule.ThresholdHigh <= rule.Threshold))
            return "outside needs an upper threshold above the lower one.";
        if (rule.Comparator is "pct_change" or "deviates")
        {
            if (rule.BaselineDays is < 1 or > 30)
                return "Baseline days must be between 1 and 30.";
            if (rule.Direction is not null && Array.IndexOf(Directions, rule.Direction) < 0)
                return "Direction must be both, up or down.";
            if (rule.Threshold <= 0)
                return "Baseline comparisons need a threshold above zero.";
        }

        if (rule.ForSeconds is < 0 or > 86400 || rule.CooldownSeconds is < 0 or > 86400)
            return "For and cooldown must be between 0 and 86400 seconds.";
        if (rule.RepeatSeconds is < 0 or > 86400)
            return "Repeat must be between 0 and 86400 seconds.";
        if (rule.MinSamples is < 0)
            return "Minimum samples cannot be negative.";
        if (rule.GroupBy is { Length: > 32 })
            return "Group by is limited to 32 characters.";
        if (!IsDiscordWebhook(rule.WebhookUrl))
            return "The webhook must be a Discord webhook URL.";
        if (rule.QuietStartMinute is < 0 or > 1439 || rule.QuietEndMinute is < 0 or > 1439)
            return "Quiet window minutes must be between 0 and 1439.";
        if (rule.QuietStartMinute is null != rule.QuietEndMinute is null)
            return "The quiet window needs both a start and an end.";

        return null;
    }

    /// <summary>
    ///     Whether a URL points at a Discord webhook.
    /// </summary>
    /// <param name="url">The URL.</param>
    public static bool IsDiscordWebhook(string? url)
    {
        return !string.IsNullOrWhiteSpace(url) && WebhookPattern().IsMatch(url.Trim());
    }

    /// <summary>
    ///     Posts a JSON payload to a Discord webhook.
    /// </summary>
    /// <param name="webhookUrl">The webhook.</param>
    /// <param name="payloadJson">The JSON body.</param>
    /// <param name="threadId">Optional thread to post into.</param>
    /// <returns>Whether Discord accepted the message.</returns>
    public async Task<bool> PostWebhookAsync(string webhookUrl, string payloadJson, ulong? threadId = null)
    {
        if (!IsDiscordWebhook(webhookUrl)) return false;

        var url = webhookUrl.Trim() + "?wait=true";
        if (threadId is { } thread) url += "&thread_id=" + thread;

        try
        {
            using var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(url, content).ConfigureAwait(false);
            if (response.IsSuccessStatusCode) return true;

            logger.LogWarning("Analytics webhook returned {Status}", (int)response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Analytics webhook delivery failed");
            return false;
        }
    }

    /// <summary>
    ///     Evaluates every enabled rule once.
    /// </summary>
    public async Task EvaluateAsync()
    {
        if (!collector.Enabled) return;
        if (!await gate.WaitAsync(0).ConfigureAwait(false)) return;

        try
        {
            await using var db = await dbFactory.CreateConnectionAsync().ConfigureAwait(false);
            var rules = await db.AnalyticsAlertRules.Where(r => r.Enabled).ToListAsync().ConfigureAwait(false);
            var now = AnalyticsCollector.MinuteOf(DateTime.UtcNow);

            foreach (var rule in rules)
            {
                try
                {
                    await EvaluateRuleAsync(db, rule, now).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Analytics alert rule {RuleId} failed to evaluate", rule.Id);
                }
            }

            LastEvaluatedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Analytics alert evaluation failed");
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EvaluateRuleAsync(MewdekoDb db, AnalyticsAlertRule rule, DateTime now)
    {
        var window = TimeSpan.FromSeconds(rule.WindowSeconds);
        var filters = ParseFilters(rule.FiltersJson);
        var baselineDays = rule.BaselineDays ?? (rule.Comparator == "deviates" ? 7 : 1);
        var oldest = rule.Comparator is "pct_change" or "deviates"
            ? now - window - TimeSpan.FromDays(baselineDays)
            : now - window;
        var resolution = ResolutionForAge(now - oldest);

        var current = await WindowValuesAsync(db, rule, filters, resolution, now - window, now).ConfigureAwait(false);

        Dictionary<string, WindowValue>? previous = null;
        List<Dictionary<string, WindowValue>>? baselines = null;
        if (rule.Comparator == "pct_change")
        {
            var shift = TimeSpan.FromDays(baselineDays);
            previous = await WindowValuesAsync(db, rule, filters, resolution, now - window - shift, now - shift)
                .ConfigureAwait(false);
        }
        else if (rule.Comparator == "deviates")
        {
            baselines = new List<Dictionary<string, WindowValue>>(baselineDays);
            for (var day = 1; day <= baselineDays; day++)
            {
                var shift = TimeSpan.FromDays(day);
                baselines.Add(await WindowValuesAsync(db, rule, filters, resolution, now - window - shift,
                    now - shift).ConfigureAwait(false));
            }
        }

        var states = await db.AnalyticsAlertStates.Where(s => s.RuleId == rule.Id).ToListAsync()
            .ConfigureAwait(false);
        var groups = new HashSet<string>(current.Keys, StringComparer.Ordinal);
        foreach (var state in states) groups.Add(state.GroupKey);
        if (groups.Count == 0 && rule.Comparator == "nodata") groups.Add(string.Empty);

        foreach (var group in groups)
        {
            current.TryGetValue(group, out var measured);
            var value = measured?.Value;
            var enoughSamples = rule.MinSamples is not > 0 || (measured?.Samples ?? 0) >= rule.MinSamples;
            var baseline = Baseline(rule, group, value, previous, baselines);
            var breaching = enoughSamples && IsBreaching(rule, value, baseline);

            var state = states.FirstOrDefault(s => s.GroupKey == group);
            await ApplyStateAsync(db, rule, group, state, value, baseline, breaching, now).ConfigureAwait(false);
        }
    }

    private async Task<Dictionary<string, WindowValue>> WindowValuesAsync(MewdekoDb db, AnalyticsAlertRule rule,
        Dictionary<string, string> filters, short resolution, DateTime from, DateTime to)
    {
        var width = TimeSpan.FromMinutes(resolution).Ticks;
        var alignedFrom = new DateTime(from.Ticks - from.Ticks % width, DateTimeKind.Utc);
        var metric = rule.Metric;

        var query = db.AnalyticsBuckets.Where(b =>
            b.Metric == metric && b.Resolution == resolution && b.Bucket >= alignedFrom && b.Bucket < to);
        foreach (var (key, value) in filters)
        {
            var needle = key + "=" + value;
            query = query.Where(b => b.Labels.Contains(needle));
        }

        var rows = await query.Take(MaxRowsPerWindow).ToListAsync().ConfigureAwait(false);
        var grouped = new Dictionary<string, List<AnalyticsBucket>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var labels = AnalyticsCollector.ParseLabels(row.Labels);
            var matches = true;
            foreach (var (key, value) in filters)
            {
                if (labels.GetValueOrDefault(key) == value) continue;
                matches = false;
                break;
            }

            if (!matches) continue;

            var group = rule.GroupBy is null ? string.Empty : labels.GetValueOrDefault(rule.GroupBy, string.Empty);
            if (!grouped.TryGetValue(group, out var list))
            {
                list = [];
                grouped[group] = list;
            }

            list.Add(row);
        }

        var result = new Dictionary<string, WindowValue>(StringComparer.Ordinal);
        foreach (var (group, list) in grouped)
        {
            result[group] = new WindowValue(AnalyticsMath.Aggregate(rule.Aggregation, list, rule.WindowSeconds),
                list.Sum(b => b.Count));
        }

        return result;
    }

    private static short ResolutionForAge(TimeSpan age)
    {
        if (age <= TimeSpan.FromHours(46)) return 1;
        return age <= TimeSpan.FromDays(13) ? (short)5 : (short)60;
    }

    private static BaselineInfo? Baseline(AnalyticsAlertRule rule, string group, double? value,
        Dictionary<string, WindowValue>? previous, List<Dictionary<string, WindowValue>>? baselines)
    {
        if (value is not { } v) return null;

        if (rule.Comparator == "pct_change" && previous is not null)
        {
            if (!previous.TryGetValue(group, out var prior) || prior.Value is not { } p ||
                Math.Abs(p) <= double.Epsilon)
                return null;
            return new BaselineInfo(p, (v - p) / Math.Abs(p) * 100, null);
        }

        if (rule.Comparator == "deviates" && baselines is not null)
        {
            var samples = new List<double>();
            foreach (var day in baselines)
            {
                if (day.TryGetValue(group, out var sample) && sample.Value is { } s) samples.Add(s);
            }

            if (samples.Count < 2) return null;
            var mean = samples.Average();
            var std = Math.Sqrt(samples.Sum(s => (s - mean) * (s - mean)) / samples.Count);
            if (std <= double.Epsilon) return null;
            return new BaselineInfo(mean, (v - mean) / std, std);
        }

        return null;
    }

    private static bool IsBreaching(AnalyticsAlertRule rule, double? value, BaselineInfo? baseline)
    {
        if (rule.Comparator == "nodata") return value is null;
        if (value is not { } v) return false;

        switch (rule.Comparator)
        {
            case "gt":
                return v > rule.Threshold;
            case "gte":
                return v >= rule.Threshold;
            case "lt":
                return v < rule.Threshold;
            case "lte":
                return v <= rule.Threshold;
            case "outside":
                return rule.ThresholdHigh is { } high && (v < rule.Threshold || v > high);
            case "pct_change":
            case "deviates":
            {
                if (baseline is null) return false;
                return rule.Direction switch
                {
                    "up" => baseline.Change > rule.Threshold,
                    "down" => -baseline.Change > rule.Threshold,
                    _ => Math.Abs(baseline.Change) > rule.Threshold
                };
            }
            default:
                return false;
        }
    }

    private async Task ApplyStateAsync(MewdekoDb db, AnalyticsAlertRule rule, string group,
        AnalyticsAlertState? existing, double? value, BaselineInfo? baseline, bool breaching, DateTime now)
    {
        var state = existing ?? new AnalyticsAlertState
        {
            RuleId = rule.Id, GroupKey = group, State = "ok", Since = now
        };
        var from = state.State;
        string? eventTo = null;
        string? notifyKind = null;

        if (breaching)
        {
            state.BreachingSince ??= now;
            switch (from)
            {
                case "ok":
                    state.State = rule.ForSeconds > 0 ? "pending" : "firing";
                    state.Since = now;
                    eventTo = state.State;
                    if (state.State == "firing") notifyKind = "firing";
                    break;
                case "pending":
                    if ((now - state.BreachingSince.Value).TotalSeconds >= rule.ForSeconds)
                    {
                        state.State = "firing";
                        eventTo = "firing";
                        notifyKind = "firing";
                    }

                    break;
                case "firing":
                    if (rule.RepeatSeconds is > 0 && state.LastNotifiedAt is { } last &&
                        (now - last).TotalSeconds >= rule.RepeatSeconds)
                        notifyKind = "firing";
                    break;
            }
        }
        else
        {
            state.BreachingSince = null;
            switch (from)
            {
                case "pending":
                    state.State = "ok";
                    state.Since = now;
                    eventTo = "ok";
                    break;
                case "firing":
                    state.State = "ok";
                    state.Since = now;
                    eventTo = "resolved";
                    if (rule.NotifyOnResolve) notifyKind = "resolved";
                    break;
            }
        }

        state.LastValue = value;

        var sent = false;
        if (notifyKind is not null && MayNotify(rule, state, notifyKind, now))
        {
            var since = state.BreachingSince ?? state.Since;
            sent = await SendNotificationAsync(new Notification(rule, group, notifyKind, value, since, baseline))
                .ConfigureAwait(false);
            if (sent) state.LastNotifiedAt = now;
        }

        if (eventTo is not null)
        {
            await db.InsertAsync(new AnalyticsAlertEvent
            {
                RuleId = rule.Id,
                GroupKey = group.Length <= 128 ? group : group[..128],
                At = now,
                FromState = from,
                ToState = eventTo,
                Value = value,
                Threshold = rule.Threshold,
                Notified = sent
            }).ConfigureAwait(false);
        }

        if (state.State == "ok" && value is null)
        {
            if (existing is not null)
                await db.AnalyticsAlertStates.Where(s => s.RuleId == rule.Id && s.GroupKey == group).DeleteAsync()
                    .ConfigureAwait(false);
            return;
        }

        if (state.State == "ok" && existing is null) return;

        await db.InsertOrReplaceAsync(state).ConfigureAwait(false);
    }

    private static bool MayNotify(AnalyticsAlertRule rule, AnalyticsAlertState state, string kind, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(rule.WebhookUrl)) return false;
        if (rule.MutedUntil is { } muted && muted > now) return false;
        if (IsQuiet(rule, now)) return false;
        if (kind != "firing" || rule.CooldownSeconds <= 0 || state.LastNotifiedAt is not { } last) return true;
        return (now - last).TotalSeconds >= rule.CooldownSeconds;
    }

    private static bool IsQuiet(AnalyticsAlertRule rule, DateTime now)
    {
        if (rule.QuietStartMinute is not { } start || rule.QuietEndMinute is not { } end || start == end)
            return false;

        var minute = now.Hour * 60 + now.Minute;
        return start < end ? minute >= start && minute < end : minute >= start || minute < end;
    }

    private Task<bool> SendNotificationAsync(Notification notification)
    {
        var rule = notification.Rule;
        var payload = BuildPayload(notification);
        return PostWebhookAsync(rule.WebhookUrl, JsonSerializer.Serialize(payload), rule.ThreadId);
    }

    private object BuildPayload(Notification notification)
    {
        var rule = notification.Rule;
        var resolved = notification.Kind == "resolved";
        var mention = !resolved && rule.MentionRoleId is { } role ? role.ToString() : null;

        var title = notification.Kind switch
        {
            "test" => $"{SeverityEmoji(rule.Severity)} TEST · {rule.Name}",
            "resolved" => $"✅ RESOLVED · {rule.Name}",
            _ => $"{SeverityEmoji(rule.Severity)} {rule.Severity.ToUpperInvariant()} · {rule.Name}"
        };
        var color = resolved
            ? 0x4ADE80
            : rule.Severity switch
            {
                "critical" => 0xF87171,
                "warning" => 0xFDAC41,
                _ => 0x3987E5
            };

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(rule.Description)) lines.Add(rule.Description);
        lines.Add($"**Group:** {(notification.Group.Length == 0 ? "fleet wide" : notification.Group)}");
        lines.Add($"**{rule.Aggregation} over {Duration(rule.WindowSeconds)}:** {FormatValue(notification.Value)}");
        if (notification.Baseline is { } baseline)
        {
            lines.Add(rule.Comparator == "deviates"
                ? $"**Baseline:** mean {FormatValue(baseline.Reference)} over {rule.BaselineDays ?? 7} days, " +
                  $"z {baseline.Change:0.##}"
                : $"**Previous window:** {FormatValue(baseline.Reference)} ({baseline.Change:+0.#;-0.#;0}%)");
        }

        var forText = rule.ForSeconds > 0 ? $" for {Duration(rule.ForSeconds)}" : string.Empty;
        lines.Add($"**Condition:** {Condition(rule)}{forText}");
        lines.Add($"**Started:** <t:{new DateTimeOffset(notification.Since, TimeSpan.Zero).ToUnixTimeSeconds()}:R>");

        var dashboard = credentials.DashboardUrl;
        if (!string.IsNullOrWhiteSpace(dashboard))
            lines.Add($"[Open alerts]({dashboard.TrimEnd('/')}/dashboard/analytics?page=alerts)");

        return new
        {
            content = mention is null ? null : $"<@&{mention}>",
            allowed_mentions = new
            {
                parse = Array.Empty<string>(),
                roles = mention is null
                    ? Array.Empty<string>()
                    : new[]
                    {
                        mention
                    }
            },
            embeds = new[]
            {
                new
                {
                    title,
                    description = string.Join('\n', lines),
                    color,
                    footer = new
                    {
                        text = $"{rule.Metric} · rule #{rule.Id}"
                    },
                    timestamp = DateTime.UtcNow.ToString("o")
                }
            }
        };
    }

    private static string Condition(AnalyticsAlertRule rule)
    {
        var days = rule.BaselineDays ?? (rule.Comparator == "deviates" ? 7 : 1);
        return rule.Comparator switch
        {
            "gt" => $"> {FormatValue(rule.Threshold)}",
            "gte" => $"≥ {FormatValue(rule.Threshold)}",
            "lt" => $"< {FormatValue(rule.Threshold)}",
            "lte" => $"≤ {FormatValue(rule.Threshold)}",
            "outside" => $"outside {FormatValue(rule.Threshold)} to {FormatValue(rule.ThresholdHigh)}",
            "pct_change" => $"{DirectionWord(rule.Direction)} more than {FormatValue(rule.Threshold)}% " +
                            $"vs {days} day{(days == 1 ? string.Empty : "s")} ago",
            "deviates" => $"{DirectionWord(rule.Direction)} more than {FormatValue(rule.Threshold)} " +
                          $"standard deviations from the {days} day mean",
            "nodata" => "no data reported",
            _ => rule.Comparator
        };
    }

    private static string DirectionWord(string? direction)
    {
        return direction switch
        {
            "up" => "rose by",
            "down" => "fell by",
            _ => "deviates by"
        };
    }

    private static string SeverityEmoji(string severity)
    {
        return severity switch
        {
            "critical" => "🔴",
            "warning" => "🟠",
            _ => "🔵"
        };
    }

    private static string Duration(int seconds)
    {
        if (seconds < 60) return $"{seconds}s";
        if (seconds < 3600) return $"{seconds / 60}m";
        return seconds < 86400 ? $"{seconds / 3600}h" : $"{seconds / 86400}d";
    }

    private static string FormatValue(double? value)
    {
        if (value is not { } v) return "no data";
        return Math.Abs(v) >= 100
            ? v.ToString("N0", CultureInfo.InvariantCulture)
            : v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, string> ParseFilters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ??
                   new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static void Normalize(AnalyticsAlertRule rule)
    {
        rule.Name = rule.Name.Trim();
        rule.Metric = rule.Metric.Trim();
        rule.Description = string.IsNullOrWhiteSpace(rule.Description) ? null : rule.Description.Trim();
        rule.WebhookUrl = rule.WebhookUrl.Trim();
        rule.GroupBy = string.IsNullOrWhiteSpace(rule.GroupBy) || rule.GroupBy == "none" ? null : rule.GroupBy.Trim();
        rule.RepeatSeconds = rule.RepeatSeconds is > 0 ? rule.RepeatSeconds : null;
        rule.MinSamples = rule.MinSamples is > 0 ? rule.MinSamples : null;
        if (rule.Comparator is not ("pct_change" or "deviates"))
        {
            rule.BaselineDays = null;
            rule.Direction = null;
        }
        else
        {
            rule.BaselineDays ??= (short)(rule.Comparator == "deviates" ? 7 : 1);
            rule.Direction ??= "both";
        }

        if (rule.Comparator != "outside") rule.ThresholdHigh = null;
    }

    private static int StateOrder(string state)
    {
        return state switch
        {
            "firing" => 0,
            "pending" => 1,
            _ => 2
        };
    }

    [GeneratedRegex(@"^https://(canary\.|ptb\.)?discord(app)?\.com/api/webhooks/\d+/[A-Za-z0-9_\-]+$")]
    private static partial Regex WebhookPattern();

    private sealed record WindowValue(double? Value, double Samples);

    private sealed record BaselineInfo(double Reference, double Change, double? StdDev);

    private sealed record Notification(
        AnalyticsAlertRule Rule,
        string Group,
        string Kind,
        double? Value,
        DateTime Since,
        BaselineInfo? Baseline);
}