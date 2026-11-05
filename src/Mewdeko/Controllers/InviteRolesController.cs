using DataModel;
using Mewdeko.Modules.Utility.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for roles members earn at a number of invites.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class InviteRolesController : Controller
{
    private readonly IDashboardAuditContext auditContext;
    private readonly DiscordShardedClient client;
    private readonly InviteRoleService service;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InviteRolesController" /> class.
    /// </summary>
    /// <param name="service">The invite role service.</param>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
    public InviteRolesController(InviteRoleService service, DiscordShardedClient client,
        IDashboardAuditContext auditContext)
    {
        this.service = service;
        this.client = client;
        this.auditContext = auditContext;
    }

    /// <summary>
    ///     Gets the milestone roles and how they are handed out.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The settings and roles.</returns>
    [HttpGet]
    public async Task<IActionResult> Get(ulong guildId)
    {
        return Ok(await BuildAsync(guildId));
    }

    /// <summary>
    ///     Changes how milestone roles are handed out.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The changes. Null fields stay as they are.</param>
    /// <returns>The settings and roles.</returns>
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(ulong guildId, [FromBody] InviteRoleSettingsRequest request)
    {
        if (request.Mode is { } mode && !Enum.IsDefined(mode))
            return BadRequest("Unknown mode.");

        auditContext.RecordBefore(await BuildAsync(guildId));
        await service.UpdateSettingsAsync(guildId, request.Mode, request.RemoveOnDrop);
        var response = await BuildAsync(guildId);
        auditContext.RecordAfter(response);
        return Ok(response);
    }

    /// <summary>
    ///     Adds a milestone role or moves it to another milestone.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="roleId">The role.</param>
    /// <param name="request">The milestone.</param>
    /// <returns>The settings and roles.</returns>
    [HttpPut("roles/{roleId}")]
    public async Task<IActionResult> SetRole(ulong guildId, ulong roleId, [FromBody] InviteRoleRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");
        if (request.RequiredInvites < 1)
            return BadRequest("The milestone has to be at least 1 invite.");
        if (!InviteRoleService.CanAssign(guild, roleId))
            return BadRequest("The bot can't give out that role. Move its highest role above it.");

        auditContext.RecordBefore(await BuildAsync(guildId));
        await service.SetRoleAsync(guildId, roleId, request.RequiredInvites);
        var response = await BuildAsync(guildId);
        auditContext.RecordAfter(response);
        return Ok(response);
    }

    /// <summary>
    ///     Removes a milestone role. Members keep the role.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="roleId">The role.</param>
    /// <returns>The settings and roles.</returns>
    [HttpDelete("roles/{roleId}")]
    public async Task<IActionResult> RemoveRole(ulong guildId, ulong roleId)
    {
        auditContext.RecordBefore(await BuildAsync(guildId));
        if (!await service.RemoveRoleAsync(guildId, roleId))
            return NotFound("That role is not a milestone role.");

        var response = await BuildAsync(guildId);
        auditContext.RecordAfter(response);
        return Ok(response);
    }

    /// <summary>
    ///     Gives or takes milestone roles for every member to match their invites.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>How many members had their roles changed.</returns>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return NotFound("Guild not found.");

        auditContext.RecordBefore(null);
        var changed = await service.SyncAsync(guild);
        return Ok(new
        {
            changed
        });
    }

    private async Task<InviteRolesResponse> BuildAsync(ulong guildId)
    {
        var settings = await service.GetSettingsAsync(guildId);
        var roles = await service.GetRolesAsync(guildId);
        var guild = client.GetGuild(guildId);
        return new InviteRolesResponse
        {
            Mode = (int)settings.Mode,
            RemoveOnDrop = settings.RemoveOnDrop,
            Roles = roles.Select(r => new InviteRoleResponse
            {
                RoleId = r.RoleId,
                RoleName = guild?.GetRole(r.RoleId)?.Name,
                RequiredInvites = r.RequiredInvites
            }).ToList()
        };
    }

    /// <summary>
    ///     Changes to how milestone roles are handed out.
    /// </summary>
    public class InviteRoleSettingsRequest
    {
        /// <summary>
        ///     0 keep every role, 1 highest role only.
        /// </summary>
        public InviteRoleMode? Mode { get; set; }

        /// <summary>
        ///     Whether members lose a role when their invites fall below it.
        /// </summary>
        public bool? RemoveOnDrop { get; set; }
    }

    /// <summary>
    ///     A milestone for a role.
    /// </summary>
    public class InviteRoleRequest
    {
        /// <summary>
        ///     The net invites needed.
        /// </summary>
        public int RequiredInvites { get; set; }
    }

    /// <summary>
    ///     The milestone roles and how they are handed out.
    /// </summary>
    public class InviteRolesResponse
    {
        /// <summary>
        ///     0 keep every role, 1 highest role only.
        /// </summary>
        public int Mode { get; set; }

        /// <summary>
        ///     Whether members lose a role when their invites fall below it.
        /// </summary>
        public bool RemoveOnDrop { get; set; }

        /// <summary>
        ///     The milestone roles from the lowest milestone up.
        /// </summary>
        public List<InviteRoleResponse> Roles { get; set; } = [];
    }

    /// <summary>
    ///     One milestone role.
    /// </summary>
    public class InviteRoleResponse
    {
        /// <summary>
        ///     The role ID.
        /// </summary>
        public ulong RoleId { get; set; }

        /// <summary>
        ///     The role's name, or null when it no longer exists.
        /// </summary>
        public string? RoleName { get; set; }

        /// <summary>
        ///     The net invites needed.
        /// </summary>
        public int RequiredInvites { get; set; }
    }
}
