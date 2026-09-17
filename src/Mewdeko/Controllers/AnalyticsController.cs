using System.Text;
using Mewdeko.AuthHandlers;
using Mewdeko.Controllers.Common.AuditLog;
using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Services.Analytics;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mewdeko.Controllers;

/// <summary>
///     Owner only read API over the analytics tables, plus the dashboard page view beacon.
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
[SkipDashboardAccess]
public class AnalyticsController(AnalyticsQueryService queries, IAnalyticsCollector collector, BotCredentials creds)
    : Controller
{
    private const int MaxPageBatch = 1000;

    /// <summary>
    ///     Rejects anyone who is not a bot owner, except for the page beacon which only carries the API key.
    /// </summary>
    /// <param name="context">The action context.</param>
    /// <param name="next">The next action in the pipeline.</param>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor { ActionName: nameof(PostPage) })
        {
            await next();
            return;
        }

        var authResult = await HttpContext.AuthenticateAsync(DashJwtConstants.SchemeName);
        var userIdClaim = authResult.Principal?.FindFirst(DashJwtConstants.UserIdClaim)?.Value;

        if (!ulong.TryParse(userIdClaim, out var userId) || !creds.IsOwner(userId))
        {
            context.Result = Forbid();
            return;
        }

        await next();
    }

    /// <summary>
    ///     Returns the metric registry.
    /// </summary>
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        return Ok(await queries.MetricsAsync());
    }

    /// <summary>
    ///     Returns a metric as one or more series over the range.
    /// </summary>
    /// <param name="metric">The metric name.</param>
    /// <param name="agg">sum, avg, min, max, last, rate, p50, p95, p99 or count.</param>
    /// <param name="groupBy">Label to split series on.</param>
    /// <param name="max">Series to keep before folding into Other.</param>
    /// <param name="res">Forced bucket width in minutes: 1, 5 or 60.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">15m, 1h, 6h, 24h, 7d or 30d.</param>
    [HttpGet("series")]
    public async Task<IActionResult> GetSeries([FromQuery] string metric, [FromQuery] string agg = "sum",
        [FromQuery] string? groupBy = null, [FromQuery] int max = 10, [FromQuery] short? res = null,
        [FromQuery] string? from = null, [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        if (string.IsNullOrWhiteSpace(metric)) return BadRequest("metric is required");
        var query = new SeriesQuery(metric, AnalyticsRange.Parse(from, to, range), Filters(), groupBy, agg,
            Math.Clamp(max, 1, 100), res);
        return Ok(await queries.SeriesAsync(query));
    }

    /// <summary>
    ///     Aggregates a metric over the range into one value.
    /// </summary>
    /// <param name="metric">The metric name.</param>
    /// <param name="agg">The aggregation.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("aggregate")]
    public async Task<IActionResult> GetAggregate([FromQuery] string metric, [FromQuery] string agg = "sum",
        [FromQuery] string? from = null, [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        if (string.IsNullOrWhiteSpace(metric)) return BadRequest("metric is required");
        return Ok(await queries.AggregateAsync(metric, AnalyticsRange.Parse(from, to, range), Filters(), agg));
    }

    /// <summary>
    ///     Aggregates a metric per value of one label.
    /// </summary>
    /// <param name="metric">The metric name.</param>
    /// <param name="label">The label to split on.</param>
    /// <param name="limit">Rows to return.</param>
    /// <param name="agg">The aggregation.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("breakdown")]
    public async Task<IActionResult> GetBreakdown([FromQuery] string metric, [FromQuery] string label,
        [FromQuery] int limit = 20, [FromQuery] string agg = "sum", [FromQuery] string? from = null,
        [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        if (string.IsNullOrWhiteSpace(metric) || string.IsNullOrWhiteSpace(label))
            return BadRequest("metric and label are required");
        return Ok(await queries.BreakdownAsync(metric, label, AnalyticsRange.Parse(from, to, range), Filters(), agg,
            limit));
    }

    /// <summary>
    ///     Returns the most used commands.
    /// </summary>
    /// <param name="limit">Rows to return.</param>
    /// <param name="kind">Invocation kind filter.</param>
    /// <param name="module">Module filter.</param>
    /// <param name="command">Command filter.</param>
    /// <param name="guild">Guild filter.</param>
    /// <param name="ok">Success filter.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("commands/top")]
    public async Task<IActionResult> GetTopCommands([FromQuery] int limit = 25, [FromQuery] string? kind = null,
        [FromQuery] string? module = null, [FromQuery] string? command = null, [FromQuery] ulong? guild = null,
        [FromQuery] bool? ok = null, [FromQuery] string? from = null, [FromQuery] string? to = null,
        [FromQuery] string? range = null)
    {
        return Ok(await queries.TopCommandsAsync(CommandFilter(from, to, range, kind, module, command, guild, ok),
            limit));
    }

    /// <summary>
    ///     Returns the commands that failed most, per error class.
    /// </summary>
    /// <param name="limit">Rows to return.</param>
    /// <param name="kind">Invocation kind filter.</param>
    /// <param name="module">Module filter.</param>
    /// <param name="command">Command filter.</param>
    /// <param name="guild">Guild filter.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("commands/failing")]
    public async Task<IActionResult> GetFailingCommands([FromQuery] int limit = 25, [FromQuery] string? kind = null,
        [FromQuery] string? module = null, [FromQuery] string? command = null, [FromQuery] ulong? guild = null,
        [FromQuery] string? from = null, [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        return Ok(await queries.FailingCommandsAsync(
            CommandFilter(from, to, range, kind, module, command, guild, false), limit));
    }

    /// <summary>
    ///     Returns one page of raw invocations, newest first.
    /// </summary>
    /// <param name="page">1-based page.</param>
    /// <param name="pageSize">Rows per page, at most 200.</param>
    /// <param name="kind">Invocation kind filter.</param>
    /// <param name="module">Module filter.</param>
    /// <param name="command">Command filter.</param>
    /// <param name="guild">Guild filter.</param>
    /// <param name="ok">Success filter.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("commands/invocations")]
    public async Task<IActionResult> GetInvocations([FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] string? kind = null, [FromQuery] string? module = null, [FromQuery] string? command = null,
        [FromQuery] ulong? guild = null, [FromQuery] bool? ok = null, [FromQuery] string? from = null,
        [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        return Ok(await queries.InvocationsAsync(CommandFilter(from, to, range, kind, module, command, guild, ok),
            page, pageSize));
    }

    /// <summary>
    ///     Returns the newest failures of one command with one error class.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="error">The error class.</param>
    /// <param name="limit">Rows to return.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("commands/error-samples")]
    public async Task<IActionResult> GetCommandErrorSamples([FromQuery] string command, [FromQuery] string error,
        [FromQuery] int limit = 50, [FromQuery] string? from = null, [FromQuery] string? to = null,
        [FromQuery] string? range = null)
    {
        if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(error))
            return BadRequest("command and error are required");
        return Ok(await queries.CommandErrorSamplesAsync(
            CommandFilter(from, to, range, null, null, command, null, false), error, limit));
    }

    /// <summary>
    ///     Counts invocations by hour of day and guild size bucket.
    /// </summary>
    /// <param name="kind">Invocation kind filter.</param>
    /// <param name="module">Module filter.</param>
    /// <param name="command">Command filter.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("commands/heatmap")]
    public async Task<IActionResult> GetHeatmap([FromQuery] string? kind = null, [FromQuery] string? module = null,
        [FromQuery] string? command = null, [FromQuery] string? from = null, [FromQuery] string? to = null,
        [FromQuery] string? range = null)
    {
        return Ok(await queries.HeatmapAsync(CommandFilter(from, to, range, kind, module, command, null, null)));
    }

    /// <summary>
    ///     Counts gateway events per type.
    /// </summary>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("events/counts")]
    public async Task<IActionResult> GetEventCounts([FromQuery] string? from = null, [FromQuery] string? to = null,
        [FromQuery] string? range = null)
    {
        return Ok(await queries.EventCountsAsync(AnalyticsRange.Parse(from, to, range), Filters()));
    }

    /// <summary>
    ///     Returns the guilds with the most gateway events.
    /// </summary>
    /// <param name="eventType">Event type filter.</param>
    /// <param name="limit">Rows to return.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("guilds/top")]
    public async Task<IActionResult> GetTopGuilds([FromQuery] string? eventType = null, [FromQuery] int limit = 25,
        [FromQuery] string? from = null, [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        return Ok(await queries.TopGuildsAsync(AnalyticsRange.Parse(from, to, range), Bot(), eventType, limit));
    }

    /// <summary>
    ///     Returns hours where a guild's activity was far above its own baseline.
    /// </summary>
    /// <param name="minCount">Smallest hourly count worth flagging.</param>
    /// <param name="limit">Rows to return.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("guilds/anomalies")]
    public async Task<IActionResult> GetAnomalies([FromQuery] int minCount = 50, [FromQuery] int limit = 50,
        [FromQuery] string? from = null, [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        return Ok(await queries.AnomaliesAsync(AnalyticsRange.Parse(from, to, range), Bot(), minCount, limit));
    }

    /// <summary>
    ///     Returns one guild's hourly activity by event type.
    /// </summary>
    /// <param name="id">The guild.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("guilds/{id}/timeline")]
    public async Task<IActionResult> GetGuildTimeline(ulong id, [FromQuery] string? from = null,
        [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        return Ok(await queries.GuildTimelineAsync(id, AnalyticsRange.Parse(from, to, range), Bot()));
    }

    /// <summary>
    ///     Returns one page of a guild's security relevant events.
    /// </summary>
    /// <param name="id">The guild.</param>
    /// <param name="eventType">Event type filter.</param>
    /// <param name="page">1-based page.</param>
    /// <param name="pageSize">Rows per page, at most 200.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("guilds/{id}/events")]
    public async Task<IActionResult> GetGuildEvents(ulong id, [FromQuery] string? eventType = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? from = null,
        [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        return Ok(await queries.GuildEventsAsync(id, AnalyticsRange.Parse(from, to, range), eventType, page,
            pageSize));
    }

    /// <summary>
    ///     Returns the summary card for one guild.
    /// </summary>
    /// <param name="id">The guild.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("guilds/{id}/card")]
    public async Task<IActionResult> GetGuildCard(ulong id, [FromQuery] string? from = null,
        [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        return Ok(await queries.GuildCardAsync(id, AnalyticsRange.Parse(from, to, range)));
    }

    /// <summary>
    ///     Returns the feature adoption table.
    /// </summary>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("features/adoption")]
    public async Task<IActionResult> GetFeatureAdoption([FromQuery] string? from = null,
        [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        return Ok(await queries.FeatureAdoptionAsync(AnalyticsRange.Parse(from, to, range), Bot()));
    }

    /// <summary>
    ///     Returns how many features each guild used.
    /// </summary>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("features/depth")]
    public async Task<IActionResult> GetFeatureDepth([FromQuery] string? from = null, [FromQuery] string? to = null,
        [FromQuery] string? range = null)
    {
        return Ok(await queries.FeatureDepthAsync(AnalyticsRange.Parse(from, to, range), Bot()));
    }

    /// <summary>
    ///     Returns settings usage from the latest census.
    /// </summary>
    /// <param name="feature">Feature key to filter on.</param>
    [HttpGet("features/settings")]
    public async Task<IActionResult> GetSettingsUsage([FromQuery] string? feature = null)
    {
        return Ok(await queries.SettingsUsageAsync(feature));
    }

    /// <summary>
    ///     Returns one census metric over the last N days.
    /// </summary>
    /// <param name="metric">The census metric name.</param>
    /// <param name="days">How many days back.</param>
    [HttpGet("features/census")]
    public async Task<IActionResult> GetCensus([FromQuery] string metric, [FromQuery] int days = 30)
    {
        if (string.IsNullOrWhiteSpace(metric)) return BadRequest("metric is required");
        return Ok(await queries.CensusSeriesAsync(metric, days));
    }

    /// <summary>
    ///     Returns the nightly snapshots for the last N days.
    /// </summary>
    /// <param name="days">How many days back.</param>
    [HttpGet("servers/snapshots")]
    public async Task<IActionResult> GetSnapshots([FromQuery] int days = 30)
    {
        return Ok(await queries.SnapshotsAsync(days, Bot()));
    }

    /// <summary>
    ///     Returns one row per server the bot is in with member makeup, activity in range and feature use.
    /// </summary>
    /// <param name="search">Optional match on server name or id.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("servers/overview")]
    public async Task<IActionResult> GetServerOverview([FromQuery] string? search = null,
        [FromQuery] string? from = null, [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        return Ok(await queries.GuildOverviewAsync(AnalyticsRange.Parse(from, to, range), Bot(), search));
    }

    /// <summary>
    ///     Returns joins, leaves and bounces over the range.
    /// </summary>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("growth/churn")]
    public async Task<IActionResult> GetChurn([FromQuery] string? from = null, [FromQuery] string? to = null,
        [FromQuery] string? range = null)
    {
        return Ok(await queries.ChurnAsync(AnalyticsRange.Parse(from, to, range ?? "7d"), Filters()));
    }

    /// <summary>
    ///     Returns per join day retention for the last N days.
    /// </summary>
    /// <param name="days">How many days back, at most 90.</param>
    [HttpGet("growth/retention")]
    public async Task<IActionResult> GetRetention([FromQuery] int days = 30)
    {
        return Ok(await queries.RetentionAsync(days, Filters()));
    }

    /// <summary>
    ///     Returns guilds that were active for at most a day and are gone.
    /// </summary>
    /// <param name="limit">Rows to return.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("growth/bounced")]
    public async Task<IActionResult> GetBounced([FromQuery] int limit = 50, [FromQuery] string? from = null,
        [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        return Ok(await queries.BouncedGuildsAsync(AnalyticsRange.Parse(from, to, range ?? "7d"), Bot(),
            Math.Clamp(limit, 1, 500)));
    }

    /// <summary>
    ///     Returns guilds that went quiet in the last week.
    /// </summary>
    /// <param name="limit">Rows to return.</param>
    [HttpGet("growth/silent")]
    public async Task<IActionResult> GetSilent([FromQuery] int limit = 50)
    {
        return Ok(await queries.SilentGuildsAsync(Bot(), limit));
    }

    /// <summary>
    ///     Returns errors grouped by type, module and message.
    /// </summary>
    /// <param name="limit">Rows to return.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("errors")]
    public async Task<IActionResult> GetErrors([FromQuery] int limit = 100, [FromQuery] string? from = null,
        [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        return Ok(await queries.ErrorGroupsAsync(AnalyticsRange.Parse(from, to, range), Bot(), limit));
    }

    /// <summary>
    ///     Returns the hourly samples of one error.
    /// </summary>
    /// <param name="type">Exception type.</param>
    /// <param name="module">Module filter.</param>
    /// <param name="hash">Message hash filter.</param>
    /// <param name="limit">Rows to return.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("errors/samples")]
    public async Task<IActionResult> GetErrorSamples([FromQuery] string type, [FromQuery] string? module = null,
        [FromQuery] string? hash = null, [FromQuery] int limit = 100, [FromQuery] string? from = null,
        [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        if (string.IsNullOrWhiteSpace(type)) return BadRequest("type is required");
        return Ok(await queries.ErrorSamplesAsync(AnalyticsRange.Parse(from, to, range), Bot(), type, module, hash,
            limit));
    }

    /// <summary>
    ///     Returns AI usage and estimated cost per model.
    /// </summary>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("ai/summary")]
    public async Task<IActionResult> GetAiSummary([FromQuery] string? from = null, [FromQuery] string? to = null,
        [FromQuery] string? range = null)
    {
        return Ok(await queries.AiSummaryAsync(AnalyticsRange.Parse(from, to, range), Filters()));
    }

    /// <summary>
    ///     Returns the guilds that used AI chat the most.
    /// </summary>
    /// <param name="limit">Rows to return.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("ai/guilds")]
    public async Task<IActionResult> GetAiGuilds([FromQuery] int limit = 25, [FromQuery] string? from = null,
        [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        return Ok(await queries.AiGuildsAsync(AnalyticsRange.Parse(from, to, range), Bot(), limit));
    }

    /// <summary>
    ///     Returns the busiest dashboard routes.
    /// </summary>
    /// <param name="limit">Rows to return.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("website/routes")]
    public async Task<IActionResult> GetRoutes([FromQuery] int limit = 50, [FromQuery] string? from = null,
        [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        return Ok(await queries.TopRoutesAsync(AnalyticsRange.Parse(from, to, range), limit));
    }

    /// <summary>
    ///     Returns dashboard routes that returned 5xx.
    /// </summary>
    /// <param name="limit">Rows to return.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("website/errors")]
    public async Task<IActionResult> GetRouteErrors([FromQuery] int limit = 50, [FromQuery] string? from = null,
        [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        return Ok(await queries.ErrorRoutesAsync(AnalyticsRange.Parse(from, to, range), limit));
    }

    /// <summary>
    ///     Returns the login funnel.
    /// </summary>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("website/funnel")]
    public async Task<IActionResult> GetFunnel([FromQuery] string? from = null, [FromQuery] string? to = null,
        [FromQuery] string? range = null)
    {
        return Ok(await queries.LoginFunnelAsync(AnalyticsRange.Parse(from, to, range)));
    }

    /// <summary>
    ///     Returns pipeline health.
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        return Ok(await queries.HealthAsync());
    }

    /// <summary>
    ///     Returns a metric's daily totals for the last N days.
    /// </summary>
    /// <param name="metric">The metric name.</param>
    /// <param name="days">How many days back.</param>
    [HttpGet("daily-totals")]
    public async Task<IActionResult> GetDailyTotals([FromQuery] string metric, [FromQuery] int days = 30)
    {
        if (string.IsNullOrWhiteSpace(metric)) return BadRequest("metric is required");
        return Ok(await queries.DailyTotalsAsync(metric, days, Bot()));
    }

    /// <summary>
    ///     Exports a series query as CSV.
    /// </summary>
    /// <param name="metric">The metric name.</param>
    /// <param name="agg">The aggregation.</param>
    /// <param name="groupBy">Label to split series on.</param>
    /// <param name="max">Series to keep before folding into Other.</param>
    /// <param name="res">Forced bucket width in minutes.</param>
    /// <param name="from">ISO start.</param>
    /// <param name="to">ISO end.</param>
    /// <param name="range">Named range.</param>
    [HttpGet("export")]
    public async Task<IActionResult> GetExport([FromQuery] string metric, [FromQuery] string agg = "sum",
        [FromQuery] string? groupBy = null, [FromQuery] int max = 20, [FromQuery] short? res = null,
        [FromQuery] string? from = null, [FromQuery] string? to = null, [FromQuery] string? range = null)
    {
        if (string.IsNullOrWhiteSpace(metric)) return BadRequest("metric is required");
        var query = new SeriesQuery(metric, AnalyticsRange.Parse(from, to, range), Filters(), groupBy, agg,
            Math.Clamp(max, 1, 100), res);
        var csv = await queries.ExportCsvAsync(query);
        var name = $"{metric.Replace('.', '-')}-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", name);
    }

    /// <summary>
    ///     Accepts a batch of dashboard page views from the beacon. API key only.
    /// </summary>
    /// <param name="samples">The page views.</param>
    [HttpPost("page")]
    [SkipAudit]
    public IActionResult PostPage([FromBody] List<PageViewSample> samples)
    {
        if (samples is null) return BadRequest("body must be an array of page views");

        var accepted = 0;
        foreach (var sample in samples.Take(MaxPageBatch))
        {
            if (string.IsNullOrWhiteSpace(sample.Route)) continue;
            var at = sample.At == default ? DateTime.UtcNow : DateTime.SpecifyKind(sample.At, DateTimeKind.Utc);
            collector.PageView(sample with
            {
                At = at, Method = string.IsNullOrEmpty(sample.Method) ? "GET" : sample.Method
            });
            accepted++;
        }

        return Ok(new
        {
            accepted
        });
    }

    private string? Bot()
    {
        var bot = Request.Query["bot"].ToString();
        return string.IsNullOrWhiteSpace(bot) ? null : bot;
    }

    private Dictionary<string, string> Filters()
    {
        var filters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, values) in Request.Query)
        {
            var value = values.ToString();
            if (string.IsNullOrEmpty(value)) continue;
            if (key is "bot" or "shard") filters[key] = value;
            else if (key.StartsWith("f.", StringComparison.Ordinal) && key.Length > 2) filters[key[2..]] = value;
        }

        return filters;
    }

    private CommandFilter CommandFilter(string? from, string? to, string? range, string? kind, string? module,
        string? command, ulong? guild, bool? ok)
    {
        var shardText = Request.Query["shard"].ToString();
        int? shard = int.TryParse(shardText, out var parsed) ? parsed : null;
        return new CommandFilter(AnalyticsRange.Parse(from, to, range), Bot(), shard, kind, module, command, guild, ok);
    }
}