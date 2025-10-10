using System.Security.Claims;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller providing API endpoints for accessing performance monitoring data.
///     Only accessible to bot owners.
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class PerformanceController : ControllerBase
{
    private readonly BotCredentials credentials;
    private readonly EventHandler eventHandler;
    private readonly PerformanceMonitorService performanceService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PerformanceController" /> class.
    /// </summary>
    /// <param name="performanceService">The performance monitoring service.</param>
    /// <param name="eventHandler">The event handler for accessing event metrics.</param>
    /// <param name="credentials">The bot credentials for owner verification.</param>
    public PerformanceController(
        PerformanceMonitorService performanceService,
        EventHandler eventHandler,
        BotCredentials credentials)
    {
        this.performanceService = performanceService;
        this.eventHandler = eventHandler;
        this.credentials = credentials;
    }

    /// <summary>
    ///     Gets the performance data for methods tracked by the performance monitoring service.
    /// </summary>
    /// <returns>An array of performance data for the top CPU-intensive methods.</returns>
    [HttpGet("methods")]
    public IActionResult GetPerformanceData()
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null || !credentials.IsOwner(userId.Value))
        {
            return Forbid();
        }

        var topMethods = performanceService.GetTopCpuMethods();

        var result = topMethods.Select(m => new
        {
            methodName = m.MethodName,
            callCount = m.CallCount,
            totalTime = m.TotalExecutionTime.TotalMilliseconds,
            avgExecutionTime = m.AvgExecutionTime,
            lastExecuted = m.LastExecuted
        }).ToArray();

        return Ok(result);
    }

    /// <summary>
    ///     Gets event metrics from the EventHandler.
    /// </summary>
    /// <returns>Event metrics data including processing counts, execution times, and error rates.</returns>
    [HttpGet("events")]
    public IActionResult GetEventMetrics()
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null || !credentials.IsOwner(userId.Value))
        {
            return Forbid();
        }

        var eventMetrics = eventHandler.GetEventMetrics();

        var result = eventMetrics.Select(kvp => new
        {
            eventType = kvp.Key,
            totalProcessed = kvp.Value.TotalProcessed,
            totalErrors = kvp.Value.TotalErrors,
            totalExecutionTime = kvp.Value.TotalExecutionTime,
            averageExecutionTime = kvp.Value.AverageExecutionTime,
            errorRate = kvp.Value.ErrorRate
        }).OrderByDescending(x => x.totalProcessed).ToArray();

        return Ok(result);
    }

    /// <summary>
    ///     Gets module metrics from the EventHandler.
    /// </summary>
    /// <returns>Module metrics data including events processed, execution times, and error rates.</returns>
    [HttpGet("modules")]
    public IActionResult GetModuleMetrics()
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null || !credentials.IsOwner(userId.Value))
        {
            return Forbid();
        }

        var moduleMetrics = eventHandler.GetModuleMetrics();

        var result = moduleMetrics.Select(kvp => new
        {
            moduleName = kvp.Key,
            eventsProcessed = kvp.Value.EventsProcessed,
            errors = kvp.Value.Errors,
            totalExecutionTime = kvp.Value.TotalExecutionTime,
            averageExecutionTime = kvp.Value.AverageExecutionTime,
            errorRate = kvp.Value.ErrorRate
        }).OrderByDescending(x => x.eventsProcessed).ToArray();

        return Ok(result);
    }

    /// <summary>
    ///     Gets comprehensive performance overview including methods, events, and modules.
    /// </summary>
    /// <returns>A comprehensive performance overview.</returns>
    [HttpGet("overview")]
    public IActionResult GetPerformanceOverview()
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null || !credentials.IsOwner(userId.Value))
        {
            return Forbid();
        }

        var topMethods = performanceService.GetTopCpuMethods();
        var eventMetrics = eventHandler.GetEventMetrics();
        var moduleMetrics = eventHandler.GetModuleMetrics();

        // Get top 10 for overview
        var topEvents = eventMetrics
            .OrderByDescending(x => x.Value.TotalProcessed)
            .Take(10)
            .Select(kvp => new
            {
                eventType = kvp.Key,
                totalProcessed = kvp.Value.TotalProcessed,
                averageExecutionTime = kvp.Value.AverageExecutionTime,
                errorRate = kvp.Value.ErrorRate
            });

        var topModules = moduleMetrics
            .OrderByDescending(x => x.Value.EventsProcessed)
            .Take(10)
            .Select(kvp => new
            {
                moduleName = kvp.Key,
                eventsProcessed = kvp.Value.EventsProcessed,
                averageExecutionTime = kvp.Value.AverageExecutionTime,
                errorRate = kvp.Value.ErrorRate
            });

        var result = new
        {
            summary = new
            {
                totalEvents = eventMetrics.Sum(x => x.Value.TotalProcessed),
                totalEventErrors = eventMetrics.Sum(x => x.Value.TotalErrors),
                totalModules = moduleMetrics.Count,
                activeEventTypes = eventMetrics.Count
            },
            topMethods = topMethods.Take(10).Select(m => new
            {
                methodName = m.MethodName, callCount = m.CallCount, avgExecutionTime = m.AvgExecutionTime
            }),
            topEvents,
            topModules
        };

        return Ok(result);
    }

    /// <summary>
    ///     Gets metrics for a specific event type.
    /// </summary>
    /// <param name="eventType">The event type to get metrics for.</param>
    /// <returns>Detailed metrics for the specified event type.</returns>
    [HttpGet("events/{eventType}")]
    public IActionResult GetEventMetric(string eventType)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null || !credentials.IsOwner(userId.Value))
        {
            return Forbid();
        }

        var eventMetrics = eventHandler.GetEventMetrics();

        if (!eventMetrics.TryGetValue(eventType, out var metrics))
        {
            return NotFound($"Event type '{eventType}' not found");
        }

        var result = new
        {
            eventType,
            totalProcessed = metrics.TotalProcessed,
            totalErrors = metrics.TotalErrors,
            totalExecutionTime = metrics.TotalExecutionTime,
            averageExecutionTime = metrics.AverageExecutionTime,
            errorRate = metrics.ErrorRate
        };

        return Ok(result);
    }

    /// <summary>
    ///     Gets metrics for a specific module.
    /// </summary>
    /// <param name="moduleName">The module name to get metrics for.</param>
    /// <returns>Detailed metrics for the specified module.</returns>
    [HttpGet("modules/{moduleName}")]
    public IActionResult GetModuleMetric(string moduleName)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null || !credentials.IsOwner(userId.Value))
        {
            return Forbid();
        }

        var moduleMetrics = eventHandler.GetModuleMetrics();

        if (!moduleMetrics.TryGetValue(moduleName, out var metrics))
        {
            return NotFound($"Module '{moduleName}' not found");
        }

        var result = new
        {
            moduleName,
            eventsProcessed = metrics.EventsProcessed,
            errors = metrics.Errors,
            totalExecutionTime = metrics.TotalExecutionTime,
            averageExecutionTime = metrics.AverageExecutionTime,
            errorRate = metrics.ErrorRate
        };

        return Ok(result);
    }

    /// <summary>
    ///     Clears all performance monitoring data.
    /// </summary>
    /// <returns>An action result indicating success or failure.</returns>
    [HttpPost("clear")]
    public IActionResult ClearPerformanceData()
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null || !credentials.IsOwner(userId.Value))
        {
            return Forbid();
        }

        performanceService.ClearPerformanceData();

        return Ok(new
        {
            message = "Performance data cleared. Event metrics reset automatically every hour."
        });
    }

    /// <summary>
    ///     Gets the authenticated user ID from either JWT or API key with dashboard header
    /// </summary>
    private ulong? GetAuthenticatedUserId()
    {
        // Check JWT authentication
        var jwtUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (jwtUserId != null && ulong.TryParse(jwtUserId, out var parsedJwtUserId))
        {
            return parsedJwtUserId;
        }

        // Check dashboard proxy header (API key auth)
        if (Request.Headers.TryGetValue("x-user-id", out var userIdHeader))
        {
            if (ulong.TryParse(userIdHeader.ToString(), out var parsedHeaderUserId))
            {
                return parsedHeaderUserId;
            }
        }

        return null;
    }
}