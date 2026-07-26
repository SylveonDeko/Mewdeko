using Mewdeko.AuthHandlers;
using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Database.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Manages restricted dashboard access for a guild. The guild owner controls delegation settings and
///     access-list managers; delegated managers can grant or revoke individual section permissions.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
[SkipDashboardAccess]
public sealed class DashboardAccessController(
    DiscordShardedClient client,
    DashboardAccessService dashboardAccessService) : ControllerBase
{
    /// <summary>
    ///     Gets the access-management settings and the current user's authority for this guild.
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(ulong guildId)
    {
        var actor = await GetActorAsync(guildId);
        if (actor == null)
            return Forbid();

        var settings = await dashboardAccessService.GetCacheFor(guildId);
        return Ok(new DashboardAccessSettingsResponse
        {
            AdminsCanManageAccess = settings.AdminsCanManageAccess,
            CanManageAccess = await CanManageAccessAsync(guildId, actor.Value),
            IsGuildOwner = actor.Value.Guild.OwnerId == actor.Value.UserId
        });
    }

    /// <summary>
    ///     Updates the owner-controlled delegation setting.
    /// </summary>
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(ulong guildId,
        [FromBody] UpdateDashboardAccessSettingsRequest request)
    {
        var actor = await GetActorAsync(guildId);
        if (actor == null || actor.Value.Guild.OwnerId != actor.Value.UserId)
            return Forbid();

        await dashboardAccessService.SetAdminsCanManageAccessAsync(guildId, request.AdminsCanManageAccess);
        return NoContent();
    }

    /// <summary>
    ///     Lists explicit access-list managers. Only the owner may inspect or alter this owner-controlled list.
    /// </summary>
    [HttpGet("managers")]
    public async Task<IActionResult> GetManagers(ulong guildId)
    {
        var actor = await GetActorAsync(guildId);
        if (actor == null || actor.Value.Guild.OwnerId != actor.Value.UserId)
            return Forbid();

        var managers = await dashboardAccessService.GetManagersAsync(guildId);
        return Ok(managers.Select(manager => new DashboardAccessManagerResponse
        {
            Id = manager.Id,
            TargetType = (DashboardAccessTargetType)manager.TargetType,
            TargetId = manager.TargetId,
            GrantedBy = manager.GrantedBy,
            DateAdded = manager.DateAdded
        }));
    }

    /// <summary>
    ///     Appoints a user or role as an explicit access-list manager. Guild-owner only.
    /// </summary>
    [HttpPost("managers")]
    public async Task<IActionResult> AddManager(ulong guildId, [FromBody] DashboardAccessTargetRequest request)
    {
        if (!IsValidTarget(request))
            return BadRequest("A valid user or role target is required.");

        var actor = await GetActorAsync(guildId);
        if (actor == null || actor.Value.Guild.OwnerId != actor.Value.UserId)
            return Forbid();

        var manager = await dashboardAccessService.AddManagerAsync(
            guildId, request.TargetType, request.TargetId, actor.Value.UserId);
        return Ok(new DashboardAccessManagerResponse
        {
            Id = manager.Id,
            TargetType = (DashboardAccessTargetType)manager.TargetType,
            TargetId = manager.TargetId,
            GrantedBy = manager.GrantedBy,
            DateAdded = manager.DateAdded
        });
    }

    /// <summary>
    ///     Removes an explicit access-list manager. Guild-owner only.
    /// </summary>
    [HttpDelete("managers/{id:int}")]
    public async Task<IActionResult> RemoveManager(ulong guildId, int id)
    {
        var actor = await GetActorAsync(guildId);
        if (actor == null || actor.Value.Guild.OwnerId != actor.Value.UserId)
            return Forbid();

        return await dashboardAccessService.RemoveManagerAsync(guildId, id) ? NoContent() : NotFound();
    }

    /// <summary>
    ///     Lists restricted dashboard access grants. The owner and delegated managers may inspect grants.
    /// </summary>
    [HttpGet("grants")]
    public async Task<IActionResult> GetGrants(ulong guildId)
    {
        var actor = await GetActorAsync(guildId);
        if (actor == null || !await CanManageAccessAsync(guildId, actor.Value))
            return Forbid();

        var grants = await dashboardAccessService.GetGrantsAsync(guildId);
        return Ok(grants.Select(grant => new DashboardAccessGrantResponse
        {
            Id = grant.Id, TargetType = grant.TargetType, TargetId = grant.TargetId, Sections = grant.Sections
        }));
    }

    /// <summary>
    ///     Creates or replaces a user/role's section grants.
    /// </summary>
    [HttpPut("grants")]
    public async Task<IActionResult> UpsertGrant(ulong guildId, [FromBody] UpsertDashboardAccessGrantRequest request)
    {
        if (!IsValidTarget(request) || request.Sections.Any(s => string.IsNullOrWhiteSpace(s.Section) ||
                                                                 s.Level is DashboardAccessLevel.None
                                                                     or > DashboardAccessLevel.Manage))
            return BadRequest("The access target and section permissions are invalid.");

        var actor = await GetActorAsync(guildId);
        if (actor == null || !await CanManageAccessAsync(guildId, actor.Value))
            return Forbid();

        var sections = request.Sections
            .GroupBy(section => section.Section, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Max(section => section.Level),
                StringComparer.OrdinalIgnoreCase);
        var id = await dashboardAccessService.UpsertGrantAsync(
            guildId, request.TargetType, request.TargetId, actor.Value.UserId, sections);
        return Ok(new
        {
            id
        });
    }

    /// <summary>
    ///     Removes a user/role's restricted dashboard access grant.
    /// </summary>
    [HttpDelete("grants/{id:int}")]
    public async Task<IActionResult> RemoveGrant(ulong guildId, int id)
    {
        var actor = await GetActorAsync(guildId);
        if (actor == null || !await CanManageAccessAsync(guildId, actor.Value))
            return Forbid();

        return await dashboardAccessService.RemoveGrantAsync(guildId, id) ? NoContent() : NotFound();
    }

    private async Task<(SocketGuild Guild, SocketGuildUser User, ulong UserId)?> GetActorAsync(ulong guildId)
    {
        var authResult = await HttpContext.AuthenticateAsync(DashJwtConstants.SchemeName);
        if (!authResult.Succeeded || !ulong.TryParse(
                authResult.Principal?.FindFirst(DashJwtConstants.UserIdClaim)?.Value, out var userId))
            return null;

        var guild = client.GetGuild(guildId);
        var user = guild?.GetUser(userId);
        return guild != null && user != null ? (guild, user, userId) : null;
    }

    private async Task<bool> CanManageAccessAsync(ulong guildId,
        (SocketGuild Guild, SocketGuildUser User, ulong UserId) actor)
    {
        if (actor.Guild.OwnerId == actor.UserId)
            return true;

        if (await dashboardAccessService.IsExplicitManagerAsync(
                guildId, actor.UserId, actor.User.Roles.Select(role => role.Id).ToList()))
            return true;

        return await dashboardAccessService.AdminsCanManageAccessAsync(guildId) &&
               (actor.User.GuildPermissions.Has(GuildPermission.Administrator) ||
                actor.User.GuildPermissions.Has(GuildPermission.ManageGuild));
    }

    private static bool IsValidTarget(DashboardAccessTargetRequest request)
    {
        return request.TargetId != 0 &&
               request.TargetType is DashboardAccessTargetType.User or DashboardAccessTargetType.Role;
    }
}