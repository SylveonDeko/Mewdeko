using System.Net;
using DataModel;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.RoleStates.Services;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Service for automatically assigning roles to users in a guild.
/// </summary>
public sealed class AutoAssignRoleService : INService
{
    /// <summary>
    ///     A queue of users to assign roles to.
    /// </summary>
    private readonly Channel<IGuildUser> assignQueue = Channel.CreateBounded<IGuildUser>(
        new BoundedChannelOptions(int.MaxValue)
        {
            FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false
        });

    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly GuildSettingsService guildSettings;
    private readonly ILogger<AutoAssignRoleService> logger;
    private readonly RoleStatesService roleStatesService;

    /// <summary>
    ///     Constructs a new instance of the AutoAssignRoleService.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="roleStatesService">The role states service.</param>
    public AutoAssignRoleService(IDataConnectionFactory dbFactory,
        GuildSettingsService guildSettings, EventHandler eventHandler, ILogger<AutoAssignRoleService> logger,
        RoleStatesService roleStatesService)
    {
        this.dbFactory = dbFactory;
        this.guildSettings = guildSettings;
        this.logger = logger;
        this.eventHandler = eventHandler;
        this.roleStatesService = roleStatesService;
        _ = RunAutoLoop();

        eventHandler.Subscribe("UserJoined", "AutoAssignRoleService", OnClientOnUserJoined);
        eventHandler.Subscribe("GuildMemberUpdated", "AutoAssignRoleService", OnClientOnGuildMemberUpdated);
        eventHandler.Subscribe("RoleDeleted", "AutoAssignRoleService", OnClientRoleDeleted);
    }

    private async Task RunAutoLoop()
    {
        while (true)
        {
            var user = await assignQueue.Reader.ReadAsync().ConfigureAwait(false);
            if (user is null)
                continue;

            // Check if skip auto-assign roles is enabled and user has a role state
            if (await roleStatesService.ShouldSkipAutoAssignRoles(user.Guild.Id) &&
                await roleStatesService.UserHasRoleState(user.Guild.Id, user.Id))
            {
                logger.LogDebug(
                    "Skipping auto-assign roles for {User} in {Guild} because they have a saved role state",
                    user.Username,
                    user.Guild.Name);
                continue;
            }

            var autoroles = await TryGetNormalRoles(user.Guild.Id);
            var autobotroles = await TryGetBotRoles(user.Guild.Id);
            if (user.IsBot && autobotroles.Any())
            {
                try
                {
                    var roleIds = autobotroles
                        .Select(roleId => user.Guild.GetRole(roleId))
                        .Where(x => x is not null)
                        .ToList();

                    if (roleIds.Count > 0)
                    {
                        try
                        {
                            await user.AddRolesAsync(roleIds).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }

                        continue;
                    }

                    logger.LogWarning(
                        "Disabled 'Auto assign bot role' feature on {GuildName} [{GuildId}] server the roles dont exist",
                        user.Guild.Name,
                        user.Guild.Id);

                    await DisableAabrAsync(user.Guild.Id).ConfigureAwait(false);
                    continue;
                }
                catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
                {
                    logger.LogWarning(
                        "Disabled 'Auto assign bot role' feature on {GuildName} [{GuildId}] server because I don't have role management permissions",
                        user.Guild.Name,
                        user.Guild.Id);

                    await DisableAabrAsync(user.Guild.Id).ConfigureAwait(false);
                    continue;
                }
                catch
                {
                    logger.LogWarning("Error in aar. Probably one of the roles doesn't exist");
                    continue;
                }
            }

            if (!autoroles.Any()) continue;
            {
                try
                {
                    var roleIds = autoroles
                        .Select(roleId => user.Guild.GetRole(roleId))
                        .Where(x => x is not null)
                        .ToList();

                    if (roleIds.Count > 0)
                    {
                        await user.AddRolesAsync(roleIds).ConfigureAwait(false);
                        await Task.Delay(250).ConfigureAwait(false);
                        continue;
                    }

                    logger.LogWarning(
                        "Disabled 'Auto assign  role' feature on {GuildName} [{GuildId}] server the roles dont exist",
                        user.Guild.Name,
                        user.Guild.Id);

                    await DisableAarAsync(user.Guild.Id).ConfigureAwait(false);
                }
                catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
                {
                    logger.LogWarning(
                        "Disabled 'Auto assign bot role' feature on {GuildName} [{GuildId}] server because I don't have role management permissions",
                        user.Guild.Name,
                        user.Guild.Id);

                    await DisableAarAsync(user.Guild.Id).ConfigureAwait(false);
                }
                catch
                {
                    logger.LogWarning("Error in aar. Probably one of the roles doesn't exist");
                }
            }
        }
    }

    /// <summary>
    ///     Event handler for when a guild member is updated.
    /// </summary>
    /// <param name="args">The cached user.</param>
    /// <param name="arsg2">The updated user.</param>
    private async Task OnClientOnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> args, SocketGuildUser arsg2)
    {
        if (arsg2.IsBot)
            return;

        var old = args.HasValue ? args.Value : null;
        if (old?.IsPending != null && old.IsPending.Value && arsg2.IsPending.HasValue && !arsg2.IsPending.Value)
            await assignQueue.Writer.WriteAsync(arsg2).ConfigureAwait(false);
    }

    /// <summary>
    ///     Event handler for when a role is deleted in a guild.
    /// </summary>
    /// <param name="role">The role that was deleted.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnClientRoleDeleted(SocketRole role)
    {
        // Get the auto-assign bot roles and auto-assign roles for the guild
        var broles = (await guildSettings.GetGuildConfig(role.Guild.Id)).AutoBotRoleIds;
        var roles = (await guildSettings.GetGuildConfig(role.Guild.Id)).AutoAssignRoleId;

        // If the deleted role is in the auto-assign roles, toggle it
        if (!string.IsNullOrWhiteSpace(roles)
            && roles.Split(" ").Select(ulong.Parse).Contains(role.Id))
        {
            await ToggleAarAsync(role.Guild.Id, role.Id).ConfigureAwait(false);
        }

        // If the deleted role is in the auto-assign bot roles, toggle it
        if (!string.IsNullOrWhiteSpace(broles)
            && broles.Split(" ").Select(ulong.Parse).Contains(role.Id))
        {
            await ToggleAabrAsync(role.Guild.Id, role.Id).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Event handler for when a user joins a guild.
    /// </summary>
    /// <param name="user">The user that joined.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OnClientOnUserJoined(IGuildUser user)
    {
        // If the user is pending, return
        if (user.IsPending.HasValue && user.IsPending.Value)
            return;

        // Get the auto-assign bot roles and auto-assign roles for the guild
        var broles = await TryGetBotRoles(user.Guild.Id);
        var roles = await TryGetNormalRoles(user.Guild.Id);

        // If the user is a bot and there are auto-assign bot roles, add the user to the assign queue
        if (user.IsBot && broles.Any())
            await assignQueue.Writer.WriteAsync(user).ConfigureAwait(false);

        // If there are auto-assign roles, add the user to the assign queue
        if (roles.Any())
            await assignQueue.Writer.WriteAsync(user).ConfigureAwait(false);
    }


    /// <summary>
    ///     Toggles the auto-assign role for a given guild and role.
    /// </summary>
    /// <param name="guildId">The ID of the guild to toggle the auto-assign role for.</param>
    /// <param name="roleId">The ID of the role to toggle.</param>
    /// <returns>A list of role IDs that are currently set to auto-assign in the guild.</returns>
    public async Task<IReadOnlyList<ulong>> ToggleAarAsync(ulong guildId, ulong roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get the guild config
        var gc = await db.GuildConfigs
            .FirstOrDefaultAsync(g => g.GuildId == guildId);

        if (gc == null)
            return [];

        var roles = gc.GetAutoAssignableRoles();
        if (!roles.Remove(roleId) && roles.Count < 10)
            roles.Add(roleId);

        gc.SetAutoAssignableRoles(roles);

        // Update the guild config
        await db.GuildConfigs
            .Where(g => g.GuildId == guildId)
            .Set(g => g.AutoAssignRoleId, gc.AutoAssignRoleId)
            .UpdateAsync();

        await guildSettings.UpdateGuildConfig(guildId, gc);

        return roles;
    }

    /// <summary>
    ///     Disables the auto-assign role feature for a given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to disable the auto-assign role feature for.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task DisableAarAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Update the guild config directly
        await db.GuildConfigs
            .Where(g => g.GuildId == guildId)
            .Set(g => g.AutoAssignRoleId, "")
            .UpdateAsync();

        // Get the updated guild config for cache update
        var gc = await db.GuildConfigs
            .FirstOrDefaultAsync(g => g.GuildId == guildId);

        if (gc != null)
            await guildSettings.UpdateGuildConfig(guildId, gc);
    }

    /// <summary>
    ///     Sets the auto-assign bot roles for a given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to set the auto-assign bot roles for.</param>
    /// <param name="newRoles">The new roles to set as auto-assign bot roles.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetAabrRolesAsync(ulong guildId, IEnumerable<ulong> newRoles)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get the guild config
        var gc = await db.GuildConfigs
            .FirstOrDefaultAsync(g => g.GuildId == guildId);

        if (gc == null)
            return;

        gc.SetAutoAssignableBotRoles(newRoles);

        // Update the guild config
        await db.GuildConfigs
            .Where(g => g.GuildId == guildId)
            .Set(g => g.AutoBotRoleIds, gc.AutoBotRoleIds)
            .UpdateAsync();

        await guildSettings.UpdateGuildConfig(guildId, gc);
    }

    /// <summary>
    ///     Toggles the auto-assign bot role for a given guild and role.
    /// </summary>
    /// <param name="guildId">The ID of the guild to toggle the auto-assign bot role for.</param>
    /// <param name="roleId">The ID of the role to toggle.</param>
    /// <returns>A list of role IDs that are currently set to auto-assign bot roles in the guild.</returns>
    public async Task<IReadOnlyList<ulong>> ToggleAabrAsync(ulong guildId, ulong roleId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get the guild config
        var gc = await db.GuildConfigs
            .FirstOrDefaultAsync(g => g.GuildId == guildId);

        if (gc == null)
            return [];

        var roles = gc.GetAutoAssignableBotRoles();
        if (!roles.Remove(roleId) && roles.Count < 10)
            roles.Add(roleId);

        gc.SetAutoAssignableBotRoles(roles);

        // Update the guild config
        await db.GuildConfigs
            .Where(g => g.GuildId == guildId)
            .Set(g => g.AutoBotRoleIds, gc.AutoBotRoleIds)
            .UpdateAsync();

        await guildSettings.UpdateGuildConfig(guildId, gc);

        return roles;
    }

    /// <summary>
    ///     Disables the auto-assign bot role feature for a given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to disable the auto-assign bot role feature for.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DisableAabrAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Update the guild config directly
        await db.GuildConfigs
            .Where(g => g.GuildId == guildId)
            .Set(g => g.AutoBotRoleIds, " ")
            .UpdateAsync();

        // Get the updated guild config for cache update
        var gc = await db.GuildConfigs
            .FirstOrDefaultAsync(g => g.GuildId == guildId);

        if (gc != null)
            await guildSettings.UpdateGuildConfig(guildId, gc);
    }

    /// <summary>
    ///     Sets the auto-assign roles for a given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to set the auto-assign roles for.</param>
    /// <param name="newRoles">The new roles to set as auto-assign roles.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetAarRolesAsync(ulong guildId, IEnumerable<ulong> newRoles)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get the guild config
        var gc = await db.GuildConfigs
            .FirstOrDefaultAsync(g => g.GuildId == guildId);

        if (gc == null)
            return;

        gc.SetAutoAssignableRoles(newRoles);

        // Update the guild config
        await db.GuildConfigs
            .Where(g => g.GuildId == guildId)
            .Set(g => g.AutoAssignRoleId, gc.AutoAssignRoleId)
            .UpdateAsync();

        await guildSettings.UpdateGuildConfig(guildId, gc);
    }

    /// <summary>
    ///     Retrieves the list of normal roles that are set to auto-assign for a given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve the auto-assign roles for.</param>
    /// <returns>A list of role IDs that are currently set to auto-assign in the guild.</returns>
    public async Task<IEnumerable<ulong>> TryGetNormalRoles(ulong guildId)
    {
        var tocheck = (await guildSettings.GetGuildConfig(guildId)).AutoAssignRoleId;
        return string.IsNullOrWhiteSpace(tocheck) ? [] : tocheck.Split(" ").Select(ulong.Parse).ToList();
    }

    /// <summary>
    ///     Retrieves the list of bot roles that are set to auto-assign for a given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve the auto-assign bot roles for.</param>
    /// <returns>A list of role IDs that are currently set to auto-assign bot roles in the guild.</returns>
    public async Task<IEnumerable<ulong>> TryGetBotRoles(ulong guildId)
    {
        var tocheck = (await guildSettings.GetGuildConfig(guildId)).AutoBotRoleIds;
        return string.IsNullOrWhiteSpace(tocheck) ? [] : tocheck.Split(" ").Select(ulong.Parse).ToList();
    }

    /// <summary>
    ///     Unloads the service and unsubscribes from events.
    /// </summary>
    public Task Unload()
    {
        eventHandler.Unsubscribe("UserJoined", "AutoAssignRoleService", OnClientOnUserJoined);
        eventHandler.Unsubscribe("GuildMemberUpdated", "AutoAssignRoleService", OnClientOnGuildMemberUpdated);
        eventHandler.Unsubscribe("RoleDeleted", "AutoAssignRoleService", OnClientRoleDeleted);
        return Task.CompletedTask;
    }
}

/// <summary>
///     Extension methods for the GuildConfig class.
/// </summary>
public static class GuildConfigExtensions
{
    /// <summary>
    ///     Retrieves the list of roles that are set to auto-assign for a given guild configuration.
    /// </summary>
    /// <param name="gc">The guild configuration to retrieve the auto-assign roles for.</param>
    /// <returns>A list of role IDs that are currently set to auto-assign in the guild configuration.</returns>
    public static List<ulong> GetAutoAssignableRoles(this GuildConfig gc)
    {
        return string.IsNullOrWhiteSpace(gc.AutoAssignRoleId)
            ? []
            : gc.AutoAssignRoleId.Split(" ").Select(ulong.Parse).ToList();
    }

    /// <summary>
    ///     Sets the auto-assign roles for a given guild configuration.
    /// </summary>
    /// <param name="gc">The guild configuration to set the auto-assign roles for.</param>
    /// <param name="roles">The new roles to set as auto-assign roles.</param>
    public static void SetAutoAssignableRoles(this GuildConfig gc, IEnumerable<ulong> roles)
    {
        gc.AutoAssignRoleId = roles.JoinWith(" ");
    }

    /// <summary>
    ///     Retrieves the list of bot roles that are set to auto-assign for a given guild configuration.
    /// </summary>
    /// <param name="gc">The guild configuration to retrieve the auto-assign bot roles for.</param>
    /// <returns>A list of role IDs that are currently set to auto-assign bot roles in the guild configuration.</returns>
    public static List<ulong> GetAutoAssignableBotRoles(this GuildConfig gc)
    {
        return string.IsNullOrWhiteSpace(gc.AutoBotRoleIds)
            ? []
            : gc.AutoBotRoleIds.Split(" ").Select(ulong.Parse).ToList();
    }

    /// <summary>
    ///     Sets the auto-assign bot roles for a given guild configuration.
    /// </summary>
    /// <param name="gc">The guild configuration to set the auto-assign bot roles for.</param>
    /// <param name="roles">The new roles to set as auto-assign bot roles.</param>
    public static void SetAutoAssignableBotRoles(this GuildConfig gc, IEnumerable<ulong> roles)
    {
        gc.AutoBotRoleIds = roles.JoinWith(" ");
    }
}