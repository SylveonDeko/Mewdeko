using Mewdeko.AuthHandlers;
using Mewdeko.Database.Enums;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Controllers.Common.DashboardAccess;

/// <summary>
///     Enforces per-section restricted dashboard access before a guild-scoped controller action runs.
///     Guild-scoped requests must carry a dashboard user JWT and are evaluated against the per-guild cache
///     in <see cref="DashboardAccessService" />. The shared API key is not sufficient on its own, because the
///     dashboard proxy attaches it to anonymous requests too. Requests that name no guild, and actions marked
///     with <see cref="SkipDashboardAccessAttribute" />, are left alone.
/// </summary>
public sealed class DashboardAccessEnforcementFilter(
    DiscordShardedClient client,
    DashboardAccessService dashboardAccessService) : IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (ShouldSkip(context))
        {
            await next();
            return;
        }

        var http = context.HttpContext;

        if (!TryResolveGuildId(context, out var guildId))
        {
            await next();
            return;
        }

        if (await DashboardAccessRule.ReadUserIdAsync(http) is not { } userId)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var descriptor = (ControllerActionDescriptor)context.ActionDescriptor;
        if (descriptor.ControllerName == "Me")
        {
            if (context.RouteData.Values.TryGetValue("userId", out var routeUser) &&
                routeUser?.ToString() == userId.ToString())
            {
                await next();
                return;
            }

            context.Result = new ObjectResult("You can only see your own data.")
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        var verdict = await DashboardAccessRule.EvaluateGuildAsync(
            client, dashboardAccessService, userId, http.Request.Method, guildId, descriptor.ControllerName);

        if (DashboardAccessRule.ToResult(verdict) is { } refusal)
        {
            context.Result = refusal;
            return;
        }

        await next();
    }

    private static bool ShouldSkip(ActionExecutingContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return true;

        return descriptor.MethodInfo.GetCustomAttributes(typeof(SkipDashboardAccessAttribute), true).Length > 0 ||
               descriptor.ControllerTypeInfo.GetCustomAttributes(typeof(SkipDashboardAccessAttribute), true).Length > 0;
    }

    /// <summary>
    ///     Resolves a real guild ID from the route or query string. <c>0</c> is treated as "not a guild-scoped
    ///     request" rather than a literal guild lookup: several actions on guild-scoped controllers (e.g.
    ///     <c>AdministrationController.GetCommandsAndModules</c>) are actually guild-independent but still sit
    ///     under a <c>{guildId}</c> route template, and the dashboard fills that segment with <c>0</c>. Discord
    ///     never issues a real snowflake of <c>0</c>, so treating it as unresolved avoids denying those requests
    ///     with a "guild not found" 403.
    /// </summary>
    private static bool TryResolveGuildId(ActionExecutingContext context, out ulong guildId)
    {
        if (context.RouteData.Values.TryGetValue("guildId", out var routeValue) &&
            ulong.TryParse(routeValue?.ToString(), out guildId) && guildId != 0)
            return true;

        if (ulong.TryParse(context.HttpContext.Request.Query["guildId"], out guildId) && guildId != 0)
            return true;

        guildId = 0;
        return false;
    }
}

/// <summary>
///     Reads the dashboard user behind a request from the signed dashboard JWT and decides what that user may do.
///     The shared API key only proves the request came from the dashboard server, never which user it is for.
/// </summary>
public static class DashboardCaller
{
    /// <summary>
    ///     Gets the Discord user id from a valid dashboard JWT.
    /// </summary>
    /// <param name="http">The current request.</param>
    /// <returns>The user id, or null when the request carries no valid dashboard JWT.</returns>
    public static async Task<ulong?> GetUserIdAsync(HttpContext http)
    {
        var auth = await http.AuthenticateAsync(DashJwtConstants.SchemeName);
        if (!auth.Succeeded || auth.Principal is not { } principal)
            return null;

        if (!principal.HasClaim(DashJwtConstants.ScopeClaim, DashJwtConstants.BackendScope))
            return null;

        var claim = principal.FindFirst(DashJwtConstants.UserIdClaim)?.Value;
        return ulong.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    ///     Decides whether a caller may act for a user.
    /// </summary>
    /// <param name="caller">The JWT user id, or null when there is no valid JWT.</param>
    /// <param name="targetUserId">The user the request is for.</param>
    /// <param name="isOwner">Tells whether a user id belongs to a bot owner.</param>
    /// <returns>True for the user themselves and for bot owners, false otherwise and always false without a JWT.</returns>
    public static bool CanActFor(ulong? caller, ulong targetUserId, Func<ulong, bool> isOwner)
    {
        return caller is { } id && (id == targetUserId || isOwner(id));
    }

    /// <summary>
    ///     Decides whether a caller is a bot owner.
    /// </summary>
    /// <param name="caller">The JWT user id, or null when there is no valid JWT.</param>
    /// <param name="isOwner">Tells whether a user id belongs to a bot owner.</param>
    /// <returns>True only for a valid JWT whose user is an owner.</returns>
    public static bool IsOwner(ulong? caller, Func<ulong, bool> isOwner)
    {
        return caller is { } id && isOwner(id);
    }
}

/// <summary>
///     Limits a controller or action to bot owners, judged from the dashboard JWT. An action can opt out with
///     <see cref="SkipRequireOwnerAttribute" />.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireOwnerAttribute : Attribute, IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<SkipRequireOwnerAttribute>().Any())
        {
            await next();
            return;
        }

        var creds = context.HttpContext.RequestServices.GetRequiredService<BotCredentials>();
        var caller = await DashboardCaller.GetUserIdAsync(context.HttpContext);
        if (!DashboardCaller.IsOwner(caller, creds.IsOwner))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        await next();
    }
}

/// <summary>
///     Lets one action of a <see cref="RequireOwnerAttribute" /> controller be reached without an owner JWT.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipRequireOwnerAttribute : Attribute;

/// <summary>
///     Binds a <c>userId</c> route or query value to the dashboard JWT on routes that have no guild to check access
///     against. Without it anyone holding the shared API key could read or change any user's personal data.
///     Routes with a guild are covered by <see cref="DashboardAccessEnforcementFilter" />.
/// </summary>
public sealed class UserRouteBindingFilter(BotCredentials creds) : IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!TryGetUserId(context, out var userId) || HasGuild(context))
        {
            await next();
            return;
        }

        var caller = await DashboardCaller.GetUserIdAsync(context.HttpContext);
        if (caller is null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!DashboardCaller.CanActFor(caller, userId, creds.IsOwner))
        {
            context.Result = new ObjectResult("You can only act for your own account.")
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }

    private static bool HasGuild(ActionExecutingContext context)
    {
        if (context.RouteData.Values.TryGetValue("guildId", out var routeValue) &&
            ulong.TryParse(routeValue?.ToString(), out var routeGuild) && routeGuild != 0)
            return true;

        return ulong.TryParse(context.HttpContext.Request.Query["guildId"], out var queryGuild) && queryGuild != 0;
    }

    private static bool TryGetUserId(ActionExecutingContext context, out ulong userId)
    {
        if (context.RouteData.Values.TryGetValue("userId", out var routeValue) &&
            ulong.TryParse(routeValue?.ToString(), out userId))
            return true;

        return ulong.TryParse(context.HttpContext.Request.Query["userId"], out userId);
    }
}