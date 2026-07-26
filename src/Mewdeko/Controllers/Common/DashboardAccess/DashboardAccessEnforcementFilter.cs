using Mewdeko.AuthHandlers;
using Mewdeko.Database.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mewdeko.Controllers.Common.DashboardAccess;

/// <summary>
///     Enforces per-section restricted dashboard access before a guild-scoped controller action runs.
///     Requests made with only the shared API key retain their existing behavior; dashboard requests carry
///     a user JWT and are evaluated against the per-guild cache in <see cref="DashboardAccessService" />.
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
        if (!http.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        var authResult = await http.AuthenticateAsync(DashJwtConstants.SchemeName);
        if (!authResult.Succeeded || authResult.Principal is not { } principal ||
            !ulong.TryParse(principal.FindFirst(DashJwtConstants.UserIdClaim)?.Value, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!TryResolveGuildId(context, out var guildId))
        {
            await next();
            return;
        }

        var guild = client.GetGuild(guildId);
        var guildUser = guild?.GetUser(userId);
        if (guild == null || guildUser == null)
        {
            context.Result = new ObjectResult("You are not a member of this server.")
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        if (guild.OwnerId == userId || guildUser.GuildPermissions.Has(GuildPermission.Administrator))
        {
            await next();
            return;
        }

        var descriptor = (ControllerActionDescriptor)context.ActionDescriptor;
        var requiredLevel = HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)
            ? DashboardAccessLevel.View
            : DashboardAccessLevel.Manage;
        var accessLevel = await dashboardAccessService.GetSectionAccessAsync(
            guildId, userId, guildUser.Roles.Select(role => role.Id).ToList(), descriptor.ControllerName);

        if (accessLevel < requiredLevel)
        {
            context.Result = new ObjectResult("You do not have access to this dashboard section.")
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
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