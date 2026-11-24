using Mewdeko.AuthHandlers;
using Mewdeko.Database.Enums;
using Mewdeko.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers.Common.DashboardAccess;

/// <summary>
///     The outcome of checking a dashboard caller against a guild.
/// </summary>
public enum DashboardAccessVerdict
{
    /// <summary>
    ///     The caller may go ahead.
    /// </summary>
    Allowed,

    /// <summary>
    ///     The request carries no valid dashboard user token.
    /// </summary>
    Unauthorized,

    /// <summary>
    ///     The caller is not a member of the guild.
    /// </summary>
    NotMember,

    /// <summary>
    ///     The caller is a member but lacks the access level the request needs for the section.
    /// </summary>
    InsufficientAccess
}

/// <summary>
///     What the access rule needs to know about a guild member.
/// </summary>
/// <param name="IsOwner">Whether the member owns the guild.</param>
/// <param name="IsAdministrator">Whether the member holds the Administrator permission.</param>
/// <param name="RoleIds">The roles the member holds.</param>
public sealed record DashboardGuildMember(bool IsOwner, bool IsAdministrator, IReadOnlyCollection<ulong> RoleIds);

/// <summary>
///     The one rule that decides whether a dashboard user may use a guild-scoped section. Both
///     <see cref="DashboardAccessEnforcementFilter" /> and the actions that only know a record id use it, so the
///     two can never disagree about who gets in.
/// </summary>
public static class DashboardAccessRule
{
    /// <summary>
    ///     Reads the dashboard user from the request's signed JWT.
    /// </summary>
    /// <param name="http">The current request.</param>
    /// <returns>The user id, or null when the request carries no valid dashboard JWT.</returns>
    public static async Task<ulong?> ReadUserIdAsync(HttpContext http)
    {
        var authResult = await http.AuthenticateAsync(DashJwtConstants.SchemeName);
        if (!authResult.Succeeded || authResult.Principal is not { } principal ||
            !ulong.TryParse(principal.FindFirst(DashJwtConstants.UserIdClaim)?.Value, out var userId))
            return null;

        return userId;
    }

    /// <summary>
    ///     Gets the access level a request needs: View for reads, Manage for everything else.
    /// </summary>
    /// <param name="httpMethod">The request's HTTP method.</param>
    /// <returns>The required level.</returns>
    public static DashboardAccessLevel RequiredLevel(string httpMethod)
    {
        return HttpMethods.IsGet(httpMethod) || HttpMethods.IsHead(httpMethod)
            ? DashboardAccessLevel.View
            : DashboardAccessLevel.Manage;
    }

    /// <summary>
    ///     Applies the rule: a token is required, the caller must be a member, owners and administrators pass, and
    ///     everyone else needs the required level on the section.
    /// </summary>
    /// <param name="userId">The user from the token, or null when there is none.</param>
    /// <param name="member">The caller's membership in the guild, or null when they are not a member.</param>
    /// <param name="required">The level the request needs.</param>
    /// <param name="sectionAccess">Looks up the level granted to the caller by their roles.</param>
    /// <returns>The verdict.</returns>
    public static async Task<DashboardAccessVerdict> EvaluateAsync(
        ulong? userId,
        DashboardGuildMember? member,
        DashboardAccessLevel required,
        Func<IReadOnlyCollection<ulong>, Task<DashboardAccessLevel>> sectionAccess)
    {
        if (userId is null)
            return DashboardAccessVerdict.Unauthorized;

        if (member is null)
            return DashboardAccessVerdict.NotMember;

        if (member.IsOwner || member.IsAdministrator)
            return DashboardAccessVerdict.Allowed;

        var level = await sectionAccess(member.RoleIds);
        return level >= required ? DashboardAccessVerdict.Allowed : DashboardAccessVerdict.InsufficientAccess;
    }

    /// <summary>
    ///     Applies the rule to a live guild.
    /// </summary>
    /// <param name="client">The Discord client the guild is read from.</param>
    /// <param name="access">The per-section grants.</param>
    /// <param name="userId">The user from the token, or null when there is none.</param>
    /// <param name="httpMethod">The request's HTTP method.</param>
    /// <param name="guildId">The guild.</param>
    /// <param name="section">The dashboard section, which is the controller name.</param>
    /// <returns>The verdict.</returns>
    public static Task<DashboardAccessVerdict> EvaluateGuildAsync(
        DiscordShardedClient client,
        DashboardAccessService access,
        ulong? userId,
        string httpMethod,
        ulong guildId,
        string section)
    {
        DashboardGuildMember? member = null;

        if (userId is { } id && client.GetGuild(guildId) is { } guild && guild.GetUser(id) is { } guildUser)
        {
            member = new DashboardGuildMember(
                guild.OwnerId == id,
                guildUser.GuildPermissions.Has(GuildPermission.Administrator),
                guildUser.Roles.Select(role => role.Id).ToList());
        }

        return EvaluateAsync(userId, member, RequiredLevel(httpMethod),
            roleIds => access.GetSectionAccessAsync(guildId, userId!.Value, roleIds, section));
    }

    /// <summary>
    ///     Turns a refusal into the response sent back. The text never names the guild.
    /// </summary>
    /// <param name="verdict">The verdict.</param>
    /// <returns>The response, or null when the caller is allowed.</returns>
    public static IActionResult? ToResult(DashboardAccessVerdict verdict)
    {
        return verdict switch
        {
            DashboardAccessVerdict.Allowed => null,
            DashboardAccessVerdict.Unauthorized => new UnauthorizedResult(),
            DashboardAccessVerdict.NotMember => new ObjectResult("You are not a member of this server.")
            {
                StatusCode = StatusCodes.Status403Forbidden
            },
            _ => new ObjectResult("You do not have access to this dashboard section.")
            {
                StatusCode = StatusCodes.Status403Forbidden
            }
        };
    }
}
