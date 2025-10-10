using DataModel;
using Mewdeko.Modules.RoleStates.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing role states (saved roles when users leave/join)
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class RoleStatesController : Controller
{
    private readonly DiscordShardedClient client;
    private readonly RoleStatesService roleStatesService;

    /// <summary>
    ///     Initializes a new instance of the RoleStatesController
    /// </summary>
    /// <param name="roleStatesService">The rolestatesservice service.</param>
    /// <param name="client">The Discord client instance.</param>
    public RoleStatesController(RoleStatesService roleStatesService, DiscordShardedClient client)
    {
        this.roleStatesService = roleStatesService;
        this.client = client;
    }

    /// <summary>
    ///     Gets role state settings for a guild
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(ulong guildId)
    {
        var settings = await roleStatesService.GetRoleStateSettings(guildId);
        return Ok(settings);
    }

    /// <summary>
    ///     Toggles role state functionality for a guild
    /// </summary>
    [HttpPost("toggle")]
    public async Task<IActionResult> ToggleRoleStates(ulong guildId)
    {
        var result = await roleStatesService.ToggleRoleStates(guildId);
        return Ok(result);
    }

    /// <summary>
    ///     Gets role state for a specific user
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserRoleState(ulong guildId, ulong userId)
    {
        var roleState = await roleStatesService.GetUserRoleState(guildId, userId);
        if (roleState == null)
            return NotFound("No role state found for user");

        return Ok(roleState);
    }

    /// <summary>
    ///     Gets all role states in a guild
    /// </summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAllRoleStates(ulong guildId)
    {
        var roleStates = await roleStatesService.GetAllUserRoleStates(guildId);
        return Ok(roleStates);
    }

    /// <summary>
    ///     Adds roles to a user's role state
    /// </summary>
    [HttpPost("user/{userId}/roles")]
    public async Task<IActionResult> AddRolesToUser(ulong guildId, ulong userId, [FromBody] List<ulong> roleIds)
    {
        var (success, message) = await roleStatesService.AddRolesToUserRoleState(guildId, userId, roleIds);
        if (!success)
            return BadRequest(message);

        return Ok();
    }

    /// <summary>
    ///     Removes roles from a user's role state
    /// </summary>
    [HttpDelete("user/{userId}/roles")]
    public async Task<IActionResult> RemoveRolesFromUser(ulong guildId, ulong userId, [FromBody] List<ulong> roleIds)
    {
        var (success, message) = await roleStatesService.RemoveRolesFromUserRoleState(guildId, userId, roleIds);
        if (!success)
            return BadRequest(message);

        return Ok();
    }

    /// <summary>
    ///     Deletes a user's role state
    /// </summary>
    [HttpDelete("user/{userId}")]
    public async Task<IActionResult> DeleteUserRoleState(ulong guildId, ulong userId)
    {
        var success = await roleStatesService.DeleteUserRoleState(userId, guildId);
        if (!success)
            return NotFound("No role state found for user");

        return Ok();
    }

    /// <summary>
    ///     Applies one user's role state to another user
    /// </summary>
    [HttpPost("user/{sourceUserId}/apply/{targetUserId}")]
    public async Task<IActionResult> ApplyRoleState(ulong guildId, ulong sourceUserId, ulong targetUserId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var targetUser = guild.Users.FirstOrDefault(x => x.Id == targetUserId);
        if (targetUser == null)
            return NotFound("Target user not found");

        var success = await roleStatesService.ApplyUserRoleStateToAnotherUser(sourceUserId, targetUser, guildId);
        if (!success)
            return BadRequest("Failed to apply role state");

        return Ok();
    }

    /// <summary>
    ///     Sets roles for a user manually
    /// </summary>
    [HttpPost("user/{userId}/set-roles")]
    public async Task<IActionResult> SetUserRoles(ulong guildId, ulong userId, [FromBody] List<ulong> roleIds)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var user = guild.Users.FirstOrDefault(x => x.Id == userId);
        if (user == null)
            return NotFound("User not found");

        await roleStatesService.SetRoleStateManually(user, guildId, roleIds);
        return Ok();
    }

    /// <summary>
    ///     Toggles the option to clear saved roles upon a user's ban
    /// </summary>
    [HttpPost("clear-on-ban")]
    public async Task<IActionResult> ToggleClearOnBan(ulong guildId, [FromBody] object data)
    {
        var settings = await roleStatesService.GetRoleStateSettings(guildId);
        if (settings == null)
            return NotFound("Role state settings not found");

        var result = await roleStatesService.ToggleClearOnBan(settings);
        return Ok(result);
    }

    /// <summary>
    ///     Toggles the option to ignore bots when saving and restoring roles
    /// </summary>
    [HttpPost("ignore-bots")]
    public async Task<IActionResult> ToggleIgnoreBots(ulong guildId, [FromBody] object data)
    {
        var settings = await roleStatesService.GetRoleStateSettings(guildId);
        if (settings == null)
            return NotFound("Role state settings not found");

        var result = await roleStatesService.ToggleIgnoreBots(settings);
        return Ok(result);
    }

    /// <summary>
    ///     Updates the role state settings for a guild
    /// </summary>
    [HttpPost("settings")]
    public async Task<IActionResult> UpdateSettings(ulong guildId, [FromBody] RoleStateSetting settings)
    {
        if (settings.GuildId != guildId)
            return BadRequest("Guild ID mismatch");

        await roleStatesService.UpdateRoleStateSettings(settings);
        return Ok();
    }

    /// <summary>
    ///     Saves the current role states of all users in a guild
    /// </summary>
    [HttpPost("save-all")]
    public async Task<IActionResult> SaveAllUserRoleStates(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var (savedCount, errorMessage) = await roleStatesService.SaveAllUserRoleStates(guild);
        return Ok(new
        {
            savedCount, errorMessage
        });
    }
}