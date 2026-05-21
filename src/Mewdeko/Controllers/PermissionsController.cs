using System.Text.Json;
using DataModel;
using Discord.Commands;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Controllers.Common.Permissions;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Help;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for setting permissions for commands and triggers
/// </summary>
/// <param name="permissionService">Service for managing command permissions</param>
/// <param name="dpoService">Service for managing Discord permission overrides</param>
/// <param name="cmdServ">Discord command service for command execution and information</param>
/// <param name="dbFactory">Provider for database contexts</param>
/// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
/// <param name="logger">The logger instance for structured logging.</param>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class PermissionsController(
    PermissionService permissionService,
    DiscordPermOverrideService dpoService,
    CommandService cmdServ,
    IDataConnectionFactory dbFactory,
    IDashboardAuditContext auditContext,
    ILogger<PermissionsController> logger
) : Controller
{
    /// <summary>
    ///     Gets all dpos for a guild
    /// </summary>
    /// <param name="guildId">the guild to get permissions for</param>
    /// <returns></returns>
    [HttpGet("dpo/{guildId}")]
    public async Task<IActionResult> GetPermissionOverridesForGuildAsync(ulong guildId)
    {
        var overrides = await dpoService.GetAllOverrides(guildId);
        return Ok(overrides);
    }

    /// <summary>
    ///     Adds a Discord permission override for a command
    /// </summary>
    /// <param name="guildId">The ID of the guild to add the permission override for</param>
    /// <param name="request">The request containing command name and permissions</param>
    /// <returns>OK with the created override if successful, or BadRequest if command doesn't exist</returns>
    [HttpPost("dpo/{guildId}")]
    public async Task<IActionResult> AddDpo(ulong guildId, [FromBody] DpoRequest request)
    {
        var com = cmdServ.Search(request.Command);
        if (!com.IsSuccess)
            return BadRequest(com);
        var perms = (GuildPermission)request.Permissions;
        auditContext.RecordBefore(await dpoService.GetAllOverrides(guildId));
        var over = await dpoService.AddOverride(guildId, request.Command, perms);
        auditContext.RecordAfter(await dpoService.GetAllOverrides(guildId));
        return Ok(over);
    }

    /// <summary>
    ///     Remove a dpo
    /// </summary>
    /// <param name="guildId"></param>
    /// <param name="commandName"></param>
    /// <returns></returns>
    [HttpDelete("dpo/{guildId}")]
    public async Task<IActionResult> RemoveDpo(ulong guildId, [FromBody] string commandName)
    {
        var com = cmdServ.Search(commandName);
        if (!com.IsSuccess)
            return BadRequest(com);
        auditContext.RecordBefore(await dpoService.GetAllOverrides(guildId));
        await dpoService.RemoveOverride(guildId, commandName);
        auditContext.RecordAfter(await dpoService.GetAllOverrides(guildId));
        return Ok();
    }

    /// <summary>
    ///     Gets regular permissions for a guild
    /// </summary>
    /// <param name="guildId">the guild to get permissions for</param>
    /// <returns></returns>
    [HttpGet("regular/{guildId}")]
    public async Task<IActionResult> GetPermissionsForGuildAsync(ulong guildId)
    {
        var perms = await permissionService.GetCacheFor(guildId);
        return Ok(perms);
    }

    /// <summary>
    ///     Adds a new permission setting for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="permission">The permission configuration to add</param>
    /// <returns>Success response when added</returns>
    [HttpPost("regular/{guildId}")]
    public async Task<IActionResult> AddPermission(ulong guildId, [FromBody] Permission1 permission)
    {
        auditContext.RecordBefore(await permissionService.GetCacheFor(guildId));
        await permissionService.AddPermissions(guildId, permission);
        auditContext.RecordAfter(await permissionService.GetCacheFor(guildId));
        return Ok();
    }

    /// <summary>
    ///     Removes a permission setting by its index
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="index">The index of the permission to remove</param>
    /// <returns>Success response when removed</returns>
    [HttpDelete("regular/{guildId}/{index}")]
    public async Task<IActionResult> RemovePermission(ulong guildId, int index)
    {
        auditContext.RecordBefore(await permissionService.GetCacheFor(guildId));
        await permissionService.RemovePerm(guildId, index);
        auditContext.RecordAfter(await permissionService.GetCacheFor(guildId));
        return Ok();
    }

    /// <summary>
    ///     Moves a permission setting to a new position
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="request">The move request containing from and to indices</param>
    /// <returns>Success response when moved</returns>
    [HttpPost("regular/{guildId}/move")]
    public async Task<IActionResult> MovePermission(ulong guildId, [FromBody] MovePermRequest request)
    {
        auditContext.RecordBefore(await permissionService.GetCacheFor(guildId));
        await permissionService.UnsafeMovePerm(guildId, request.From, request.To);
        auditContext.RecordAfter(await permissionService.GetCacheFor(guildId));
        return Ok();
    }

    /// <summary>
    ///     Resets all permission settings for a guild
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <returns>Success response when reset</returns>
    [HttpPost("regular/{guildId}/reset")]
    public async Task<IActionResult> ResetPermissions(ulong guildId)
    {
        auditContext.RecordBefore(await permissionService.GetCacheFor(guildId));
        await permissionService.Reset(guildId);
        return Ok();
    }

    /// <summary>
    ///     Sets the verbose mode for permission feedback
    /// </summary>
    /// <param name="guildId">The ID of the guild</param>
    /// <param name="request">Whether to enable verbose mode</param>
    /// <returns>Success response when updated</returns>
    [HttpPost("regular/{guildId}/verbose")]
    public async Task<IActionResult> SetVerbose(ulong guildId, [FromBody] JsonElement request)
    {
        var verbose = request.GetProperty("verbose").GetBoolean();

        // Create a connection using the factory
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get the guild config
        var config = await db.GuildConfigs.Where(gc => gc.GuildId == guildId).FirstOrDefaultAsync();

        if (config == null)
            return NotFound("Guild configuration not found");

        auditContext.RecordBefore(config.VerbosePermissions);

        await db.GuildConfigs
            .Where(gc => gc.GuildId == guildId)
            .Set(gc => gc.VerbosePermissions, verbose)
            .UpdateAsync();

        auditContext.RecordAfter(verbose);
        return Ok();
    }

    /// <summary>
    ///     Sets the permission role for the specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="roleId">The string representation of the role ID to set.</param>
    /// <returns>An <see cref="OkResult" /> if successful, otherwise potentially an error response.</returns>
    [HttpPost("regular/{guildId:ulong}/role")]
    public async Task<IActionResult> SetPermissionRole(ulong guildId, [FromBody] string roleId)
    {
        try
        {
            await using (var db = await dbFactory.CreateConnectionAsync())
            {
                var rowsAffected = await db.GuildConfigs
                    .Where(gc => gc.GuildId == guildId)
                    .Set(gc => gc.PermissionRole, roleId)
                    .UpdateAsync();

                if (rowsAffected == 0)
                {
                    logger.LogWarning("Attempted to set permission role for non-existent GuildConfig {GuildId}",
                        guildId);
                    return NotFound($"Configuration for guild {guildId} not found.");
                }

                var config = await db.GuildConfigs.Where(gc => gc.GuildId == guildId).FirstOrDefaultAsync();
                if (config == null)
                {
                    logger.LogError("Failed to re-fetch GuildConfig {GuildId} after successful update.", guildId);
                    return StatusCode(500, "Failed to retrieve configuration after update.");
                }

                // Get permissions needed for cache update
                var permissions = await db.Permissions1
                    .Where(p => p.GuildId == guildId).ToListAsync();

                permissionService.UpdateCache(guildId, permissions, config);
            } // db context disposed here

            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting permission role for guild {GuildId}", guildId);
            return StatusCode(500, "An error occurred while updating settings.");
        }
    }


    /// <summary>
    ///     Gets all commands and modules with their information
    /// </summary>
    /// <returns>A dictionary of module names and their commands</returns>
    [HttpGet("commands")]
    public async Task<IActionResult> GetCommandsAndModules()
    {
        await Task.CompletedTask;
        try
        {
            var modules = cmdServ.Modules;
            var moduleList = (from module in modules
                let moduleName = module.IsSubmodule ? module.Parent!.Name : module.Name
                let commands = module.Commands.OrderByDescending(x => x.Name)
                    .Select(cmd =>
                    {
                        var userPerm =
                            cmd.Preconditions.FirstOrDefault(ca => ca is UserPermAttribute) as UserPermAttribute;
                        var botPerm =
                            cmd.Preconditions.FirstOrDefault(ca => ca is BotPermAttribute) as BotPermAttribute;
                        var isDragon =
                            cmd.Preconditions.FirstOrDefault(ca => ca is RequireDragonAttribute) as
                                RequireDragonAttribute;

                        return new Command
                        {
                            BotVersion = StatsService.BotVersion,
                            CommandName = cmd.Aliases.Any() ? cmd.Aliases[0] : cmd.Name,
                            Description = cmd.Summary ?? "No description available",
                            Example = cmd.Remarks?.Split('\n').ToList() ?? [],
                            GuildUserPermissions = userPerm?.UserPermissionAttribute.GuildPermission?.ToString() ?? "",
                            ChannelUserPermissions =
                                userPerm?.UserPermissionAttribute.ChannelPermission?.ToString() ?? "",
                            GuildBotPermissions = botPerm?.GuildPermission?.ToString() ?? "",
                            ChannelBotPermissions = botPerm?.ChannelPermission?.ToString() ?? "",
                            IsDragon = isDragon != null
                        };
                    })
                    .ToList()
                select new Module(commands, moduleName)).ToList();

            return Ok(moduleList);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error fetching commands and modules");
            return StatusCode(500, "Error fetching commands and modules");
        }
    }
}