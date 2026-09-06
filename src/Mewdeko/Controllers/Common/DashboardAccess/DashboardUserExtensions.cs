using Mewdeko.AuthHandlers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Mewdeko.Controllers.Common.DashboardAccess;

/// <summary>
///     Reads the Discord user behind a dashboard request from its signed JWT.
/// </summary>
public static class DashboardUserExtensions
{
    /// <summary>
    ///     Resolves the authenticated dashboard user's Discord ID.
    ///     Endpoints that act on behalf of a user must use this rather than a user ID taken
    ///     from the request body: the shared API key is attached by the dashboard proxy to
    ///     anonymous requests too, so a body-supplied ID proves nothing about who is calling.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The user's Discord ID, or null when the request carries no valid dashboard token.</returns>
    public static async Task<ulong?> GetDashboardUserIdAsync(this HttpContext context)
    {
        var authResult = await context.AuthenticateAsync(DashJwtConstants.SchemeName);
        if (!authResult.Succeeded || authResult.Principal is not { } principal)
            return null;

        return ulong.TryParse(principal.FindFirst(DashJwtConstants.UserIdClaim)?.Value, out var userId)
            ? userId
            : null;
    }
}