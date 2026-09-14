using System.Text.Json;
using DataModel;
using Mewdeko.AuthHandlers;
using Mewdeko.Controllers.Common.Analytics;
using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Services.Analytics;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mewdeko.Controllers;

/// <summary>
///     Owner only management of analytics alert rules, their history and the weekly digest.
/// </summary>
[ApiController]
[Route("botapi/Analytics/alerts")]
[Authorize("ApiKeyPolicy")]
[SkipDashboardAccess]
public class AnalyticsAlertsController(
    AnalyticsAlertService alerts,
    AnalyticsDigestService digest,
    BotCredentials creds) : Controller
{
    private ulong userId;

    /// <summary>
    ///     Rejects anyone who is not a bot owner, reading the dashboard user out of the dashboard JWT.
    /// </summary>
    /// <param name="context">The action context.</param>
    /// <param name="next">The next action in the pipeline.</param>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var authResult = await HttpContext.AuthenticateAsync(DashJwtConstants.SchemeName);
        var userIdClaim = authResult.Principal?.FindFirst(DashJwtConstants.UserIdClaim)?.Value;

        if (!ulong.TryParse(userIdClaim, out userId) || !creds.IsOwner(userId))
        {
            context.Result = Forbid();
            return;
        }

        await next();
    }

    /// <summary>
    ///     Lists every rule with its live states, newest first.
    /// </summary>
    [HttpGet("rules")]
    public async Task<IActionResult> GetRules()
    {
        var rules = await alerts.ListRulesAsync();
        var states = await alerts.StatesAsync();
        var fired = await alerts.FiredCountsAsync();
        return Ok(rules.Select(r => Map(r, states, fired)).ToList());
    }

    /// <summary>
    ///     Creates a rule.
    /// </summary>
    /// <param name="request">The rule.</param>
    [HttpPost("rules")]
    public async Task<IActionResult> CreateRule([FromBody] AlertRuleRequest request)
    {
        var rule = ToEntity(request);
        rule.CreatedBy = userId;
        var error = AnalyticsAlertService.Validate(rule);
        if (error is not null)
            return BadRequest(new
            {
                error
            });

        var created = await alerts.CreateRuleAsync(rule);
        return Ok(Map(created, new Dictionary<int, List<AlertStateInfo>>(), new Dictionary<int, int>()));
    }

    /// <summary>
    ///     Updates a rule.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="request">The new values.</param>
    [HttpPut("rules/{id:int}")]
    public async Task<IActionResult> UpdateRule(int id, [FromBody] AlertRuleRequest request)
    {
        var rule = ToEntity(request);
        var error = AnalyticsAlertService.Validate(rule);
        if (error is not null)
            return BadRequest(new
            {
                error
            });

        var updated = await alerts.UpdateRuleAsync(id, rule);
        if (updated is null)
            return NotFound(new
            {
                error = "That rule no longer exists."
            });

        var states = await alerts.StatesAsync();
        var fired = await alerts.FiredCountsAsync();
        return Ok(Map(updated, states, fired));
    }

    /// <summary>
    ///     Deletes a rule with its states.
    /// </summary>
    /// <param name="id">The rule id.</param>
    [HttpDelete("rules/{id:int}")]
    public async Task<IActionResult> DeleteRule(int id)
    {
        return await alerts.DeleteRuleAsync(id)
            ? Ok()
            : NotFound(new
            {
                error = "That rule no longer exists."
            });
    }

    /// <summary>
    ///     Sends a sample notification for a rule to its webhook.
    /// </summary>
    /// <param name="id">The rule id.</param>
    [HttpPost("rules/{id:int}/test")]
    public async Task<IActionResult> TestRule(int id)
    {
        var rule = await alerts.GetRuleAsync(id);
        if (rule is null)
            return NotFound(new
            {
                error = "That rule no longer exists."
            });
        return Ok(new AlertSendResponse(await alerts.TestAsync(rule)));
    }

    /// <summary>
    ///     Mutes a rule's notifications, or unmutes it when minutes is zero.
    /// </summary>
    /// <param name="id">The rule id.</param>
    /// <param name="minutes">How long to mute, capped at 30 days.</param>
    [HttpPost("rules/{id:int}/mute")]
    public async Task<IActionResult> MuteRule(int id, [FromQuery] int minutes = 60)
    {
        var (found, mutedUntil) = await alerts.MuteAsync(id, minutes);
        return found
            ? Ok(new AlertMuteResponse(mutedUntil))
            : NotFound(new
            {
                error = "That rule no longer exists."
            });
    }

    /// <summary>
    ///     Returns a page of alert events, newest first.
    /// </summary>
    /// <param name="page">The 1-based page.</param>
    /// <param name="pageSize">The page size, at most 200.</param>
    /// <param name="ruleId">Optional rule filter.</param>
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents([FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        [FromQuery] int? ruleId = null)
    {
        return Ok(await alerts.EventsAsync(page, pageSize, ruleId));
    }

    /// <summary>
    ///     Returns every rule group currently firing.
    /// </summary>
    [HttpGet("firing")]
    public async Task<IActionResult> GetFiring()
    {
        return Ok(await alerts.FiringAsync());
    }

    /// <summary>
    ///     Returns the fixed thresholds of enabled rules on a metric.
    /// </summary>
    /// <param name="metric">The metric.</param>
    [HttpGet("bands")]
    public async Task<IActionResult> GetBands([FromQuery] string metric)
    {
        if (string.IsNullOrWhiteSpace(metric))
            return BadRequest(new
            {
                error = "A metric is required."
            });
        return Ok(await alerts.BandsAsync(metric.Trim()));
    }

    /// <summary>
    ///     Sends the weekly digest now.
    /// </summary>
    [HttpPost("digest")]
    public async Task<IActionResult> SendDigest()
    {
        var sent = await digest.SendNowAsync();
        return sent
            ? Ok(new AlertSendResponse(true))
            : BadRequest(new
            {
                error = "No digest webhook is configured or Discord rejected it."
            });
    }

    private static AnalyticsAlertRule ToEntity(AlertRuleRequest request)
    {
        return new AnalyticsAlertRule
        {
            Name = request.Name ?? string.Empty,
            Description = request.Description,
            Enabled = request.Enabled,
            Severity = request.Severity ?? "warning",
            Metric = request.Metric ?? string.Empty,
            FiltersJson = request.Filters is { Count: > 0 } ? JsonSerializer.Serialize(request.Filters) : null,
            GroupBy = request.GroupBy,
            Aggregation = request.Aggregation ?? "sum",
            WindowSeconds = request.WindowSeconds,
            Comparator = request.Comparator ?? "gt",
            BaselineDays = request.BaselineDays,
            Direction = request.Direction,
            Threshold = request.Threshold,
            ThresholdHigh = request.ThresholdHigh,
            ForSeconds = request.ForSeconds,
            CooldownSeconds = request.CooldownSeconds,
            RepeatSeconds = request.RepeatSeconds,
            WebhookUrl = request.WebhookUrl ?? string.Empty,
            MentionRoleId = ulong.TryParse(request.MentionRoleId, out var role) && role != 0 ? role : null,
            ThreadId = ulong.TryParse(request.ThreadId, out var thread) && thread != 0 ? thread : null,
            NotifyOnResolve = request.NotifyOnResolve,
            QuietStartMinute = request.QuietStartMinute,
            QuietEndMinute = request.QuietEndMinute,
            MinSamples = request.MinSamples
        };
    }

    private static AlertRuleResponse Map(AnalyticsAlertRule rule, Dictionary<int, List<AlertStateInfo>> states,
        Dictionary<int, int> fired)
    {
        Dictionary<string, string> filters;
        try
        {
            filters = string.IsNullOrWhiteSpace(rule.FiltersJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(rule.FiltersJson) ??
                  new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            filters = new Dictionary<string, string>();
        }

        return new AlertRuleResponse(rule.Id, rule.Name, rule.Description, rule.Enabled, rule.Severity, rule.Metric,
            filters, rule.GroupBy, rule.Aggregation, rule.WindowSeconds, rule.Comparator, rule.BaselineDays,
            rule.Direction, rule.Threshold, rule.ThresholdHigh, rule.ForSeconds, rule.CooldownSeconds,
            rule.RepeatSeconds, rule.WebhookUrl, rule.MentionRoleId?.ToString(), rule.ThreadId?.ToString(),
            rule.NotifyOnResolve, rule.QuietStartMinute, rule.QuietEndMinute, rule.MutedUntil, rule.MinSamples,
            rule.CreatedAt, rule.UpdatedAt, rule.CreatedBy.ToString(),
            states.GetValueOrDefault(rule.Id) ?? [], fired.GetValueOrDefault(rule.Id));
    }
}