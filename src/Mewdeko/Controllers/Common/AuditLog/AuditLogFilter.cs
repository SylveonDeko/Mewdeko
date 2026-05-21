using System.Text.Json.Nodes;
using Mewdeko.AuthHandlers;
using Mewdeko.Database.Enums;
using Mewdeko.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mewdeko.Controllers.Common.AuditLog;

/// <summary>
///     Global action filter that records every dashboard request carrying a
///     verified user identity into the dashboard audit log. GET requests are
///     logged as views; mutations are logged with a before/after diff (when the
///     controller recorded a before snapshot) or the redacted request body.
///     Requests authenticated only by the shared API key, and infrastructure
///     endpoints, are ignored.
/// </summary>
public class AuditLogFilter(
    DashboardAuditService auditService,
    ILogger<AuditLogFilter> logger) : IAsyncActionFilter
{
    /// <summary>
    ///     Controllers that are infrastructure or high-frequency polling endpoints
    ///     and should never be audited. Matched against the controller name.
    /// </summary>
    private static readonly HashSet<string> ExcludedControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "AuditLog", "InstanceManagement", "SystemInfo", "Performance", "BotStatus"
    };

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        try
        {
            await RecordAsync(context, executed);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dashboard audit filter failed to record an entry");
        }
    }

    /// <summary>
    ///     Records the request. The dashboard JWT scheme is authenticated
    ///     explicitly here because controllers authorize with the shared API key
    ///     policy, so the JWT handler never runs on its own and the user identity
    ///     would otherwise be absent from <c>HttpContext.User</c>.
    /// </summary>
    private async Task RecordAsync(ActionExecutingContext context, ActionExecutedContext executed)
    {
        var http = context.HttpContext;

        var authResult = await http.AuthenticateAsync(DashJwtConstants.SchemeName);
        if (!authResult.Succeeded || authResult.Principal is not { } principal)
            return;

        var userIdClaim = principal.FindFirst(DashJwtConstants.UserIdClaim)?.Value;
        if (!ulong.TryParse(userIdClaim, out var userId))
            return;

        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return;

        var section = descriptor.ControllerName;
        if (ExcludedControllers.Contains(section))
            return;

        if (descriptor.MethodInfo.GetCustomAttributes(typeof(SkipAuditAttribute), true).Length > 0 ||
            descriptor.ControllerTypeInfo.GetCustomAttributes(typeof(SkipAuditAttribute), true).Length > 0)
            return;

        var method = http.Request.Method;
        var action = ClassifyAction(method);
        var auditContext = http.RequestServices.GetService(typeof(IDashboardAuditContext)) as IDashboardAuditContext;

        if (!TryResolveGuildId(context, auditContext, out var guildId))
            return;

        var userName = principal.FindFirst(DashJwtConstants.UserNameClaim)?.Value ?? "";
        var endpoint = $"{method} {http.Request.Path}";
        var userAgent = http.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent))
            userAgent = null;

        var changes = BuildChanges(context, action, auditContext);

        _ = auditService.LogAsync(
            guildId, userId, userName, action, section, endpoint, method, changes, userAgent);
    }

    /// <summary>
    ///     Resolves the guild the request is scoped to. Checks the route values
    ///     first, then a <c>guildId</c> query-string parameter, then a <c>GuildId</c>
    ///     property on the controller's before/after snapshot for endpoints that
    ///     identify the resource by some other id (form id, etc.).
    /// </summary>
    private static bool TryResolveGuildId(
        ActionExecutingContext context, IDashboardAuditContext? auditContext, out ulong guildId)
    {
        if (context.RouteData.Values.TryGetValue("guildId", out var routeValue) &&
            ulong.TryParse(routeValue?.ToString(), out guildId))
            return true;

        if (ulong.TryParse(context.HttpContext.Request.Query["guildId"], out guildId))
            return true;

        if (TryGuildIdFromSnapshot(auditContext?.Before, out guildId) ||
            TryGuildIdFromSnapshot(auditContext?.After, out guildId))
            return true;

        guildId = 0;
        return false;
    }

    private static bool TryGuildIdFromSnapshot(JsonNode? node, out ulong guildId)
    {
        guildId = 0;
        if (node is JsonObject obj &&
            obj.TryGetPropertyValue("GuildId", out var value) && value is not null)
            return ulong.TryParse(value.ToString(), out guildId);
        return false;
    }

    private static AuditAction ClassifyAction(string method) => method.ToUpperInvariant() switch
    {
        "GET" => AuditAction.View,
        "POST" => AuditAction.Create,
        "PUT" or "PATCH" => AuditAction.Update,
        "DELETE" => AuditAction.Delete,
        _ => AuditAction.Update
    };

    private static string? BuildChanges(
        ActionExecutingContext context,
        AuditAction action,
        IDashboardAuditContext? auditContext)
    {
        if (action == AuditAction.View)
            return null;

        if (auditContext is { HasBefore: true })
        {
            var after = auditContext.HasAfter
                ? auditContext.After
                : AuditChangeSerializer.Snapshot(BodyArguments(context));
            return AuditChangeSerializer.BuildDiff(auditContext.Before, after);
        }

        return AuditChangeSerializer.BuildRequestBody(BodyArguments(context));
    }

    /// <summary>
    ///     The action arguments that represent the request body, excluding pure
    ///     route values so the stored change is just the payload the user sent.
    /// </summary>
    private static object BodyArguments(ActionExecutingContext context)
    {
        var routeKeys = context.RouteData.Values.Keys;
        return context.ActionArguments
            .Where(kv => !routeKeys.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }
}
