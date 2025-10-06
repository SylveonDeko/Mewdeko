using Mewdeko.Modules.StatusRoles.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API Controller for managing status-based role assignments.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class StatusRolesController(
    StatusRolesService service,
    DiscordShardedClient client)
    : Controller
{
    /// <summary>
    ///     Gets all status role configurations for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>List of status role configurations</returns>
    [HttpGet]
    public async Task<IActionResult> GetStatusRoles(ulong guildId)
    {
        var statusRoles = await service.GetStatusRoleConfig(guildId);
        return Ok(statusRoles);
    }

    /// <summary>
    ///     Adds a new status role configuration
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="status">The status text to watch for</param>
    /// <returns>Success or error response</returns>
    [HttpPost]
    public async Task<IActionResult> AddStatusRole(ulong guildId, [FromBody] string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return BadRequest("Status cannot be empty");

        var success = await service.AddStatusRoleConfig(status, guildId);
        if (!success)
            return BadRequest("Status role already exists");

        return Ok();
    }

    /// <summary>
    ///     Removes a status role configuration by ID
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="id">The ID of the status role configuration</param>
    /// <returns>Success response</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveStatusRole(ulong guildId, int id)
    {
        await service.RemoveStatusRoleConfig(id);
        return Ok();
    }

    /// <summary>
    ///     Sets the roles to add when status is detected
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="id">The ID of the status role configuration</param>
    /// <param name="roleIds">Space-separated list of role IDs</param>
    /// <returns>Success or error response</returns>
    [HttpPost("{id}/addRoles")]
    public async Task<IActionResult> SetAddRoles(ulong guildId, int id, [FromBody] string roleIds)
    {
        var statusRoles = await service.GetStatusRoleConfig(guildId);
        var statusRole = statusRoles.FirstOrDefault(x => x.Id == id);

        if (statusRole == null)
            return NotFound("Status role configuration not found");

        var success = await service.SetAddRoles(statusRole, roleIds);
        if (!success)
            return BadRequest("Failed to update add roles");

        return Ok();
    }

    /// <summary>
    ///     Sets the roles to remove when status is detected
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="id">The ID of the status role configuration</param>
    /// <param name="roleIds">Space-separated list of role IDs</param>
    /// <returns>Success or error response</returns>
    [HttpPost("{id}/removeRoles")]
    public async Task<IActionResult> SetRemoveRoles(ulong guildId, int id, [FromBody] string roleIds)
    {
        var statusRoles = await service.GetStatusRoleConfig(guildId);
        var statusRole = statusRoles.FirstOrDefault(x => x.Id == id);

        if (statusRole == null)
            return NotFound("Status role configuration not found");

        var success = await service.SetRemoveRoles(statusRole, roleIds);
        if (!success)
            return BadRequest("Failed to update remove roles");

        return Ok();
    }

    /// <summary>
    ///     Sets the channel where status messages should be sent
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="id">The ID of the status role configuration</param>
    /// <param name="channelId">The ID of the channel</param>
    /// <returns>Success or error response</returns>
    [HttpPost("{id}/channel")]
    public async Task<IActionResult> SetStatusChannel(ulong guildId, int id, [FromBody] ulong channelId)
    {
        var statusRoles = await service.GetStatusRoleConfig(guildId);
        var statusRole = statusRoles.FirstOrDefault(x => x.Id == id);

        if (statusRole == null)
            return NotFound("Status role configuration not found");

        var success = await service.SetStatusChannel(statusRole, channelId);
        if (!success)
            return BadRequest("Failed to update status channel");

        return Ok();
    }

    /// <summary>
    ///     Sets the embed text for status messages
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="id">The ID of the status role configuration</param>
    /// <param name="embedText">The embed text</param>
    /// <returns>Success or error response</returns>
    [HttpPost("{id}/embed")]
    public async Task<IActionResult> SetStatusEmbed(ulong guildId, int id, [FromBody] string embedText)
    {
        var statusRoles = await service.GetStatusRoleConfig(guildId);
        var statusRole = statusRoles.FirstOrDefault(x => x.Id == id);

        if (statusRole == null)
            return NotFound("Status role configuration not found");

        var success = await service.SetStatusEmbed(statusRole, embedText);
        if (!success)
            return BadRequest("Failed to update status embed");

        return Ok();
    }

    /// <summary>
    ///     Toggles whether to remove roles that were added
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="id">The ID of the status role configuration</param>
    /// <returns>Success or error response</returns>
    [HttpPost("{id}/toggleRemoveAdded")]
    public async Task<IActionResult> ToggleRemoveAdded(ulong guildId, int id)
    {
        var statusRoles = await service.GetStatusRoleConfig(guildId);
        var statusRole = statusRoles.FirstOrDefault(x => x.Id == id);

        if (statusRole == null)
            return NotFound("Status role configuration not found");

        var success = await service.ToggleRemoveAdded(statusRole);
        if (!success)
            return BadRequest("Failed to toggle remove added setting");

        return Ok(new
        {
            removeAdded = !statusRole.RemoveAdded
        });
    }

    /// <summary>
    ///     Toggles whether to re-add roles that were removed
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="id">The ID of the status role configuration</param>
    /// <returns>Success or error response</returns>
    [HttpPost("{id}/toggleReaddRemoved")]
    public async Task<IActionResult> ToggleReaddRemoved(ulong guildId, int id)
    {
        var statusRoles = await service.GetStatusRoleConfig(guildId);
        var statusRole = statusRoles.FirstOrDefault(x => x.Id == id);

        if (statusRole == null)
            return NotFound("Status role configuration not found");

        var success = await service.ToggleAddRemoved(statusRole);
        if (!success)
            return BadRequest("Failed to toggle readd removed setting");

        return Ok(new
        {
            readdRemoved = !statusRole.ReaddRemoved
        });
    }
}