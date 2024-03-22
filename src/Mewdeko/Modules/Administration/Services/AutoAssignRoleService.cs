using System.Net;
using Discord.Net;
using Serilog;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
/// Service for automatically assigning roles to users in a guild.
/// </summary>
public sealed class AutoAssignRoleService : INService
{
    /// <summary>
    /// A queue of users to assign roles to.
    /// </summary>
    private readonly Channel<IGuildUser> assignQueue = Channel.CreateBounded<IGuildUser>(
        new BoundedChannelOptions(int.MaxValue)
        {
            FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false
        });

    private readonly DbService db;
    private readonly GuildSettingsService guildSettings;

    /// <summary>
    /// Constructs a new instance of the AutoAssignRoleService.
    /// </summary>
    /// <param name="db">The database service.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    /// <param name="eventHandler">The event handler.</param>
    public AutoAssignRoleService(DbService db,
        GuildSettingsService guildSettings, EventHandler eventHandler)
    {
        this.db = db;
        this.guildSettings = guildSettings;
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var user = await assignQueue.Reader.ReadAsync().ConfigureAwait(false);
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

                        Log.Warning(
                            "Disabled 'Auto assign bot role' feature on {GuildName} [{GuildId}] server the roles dont exist",
                            user.Guild.Name,
                            user.Guild.Id);

                        await DisableAabrAsync(user.Guild.Id).ConfigureAwait(false);
                        continue;
                    }
                    catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
                    {
                        Log.Warning(
                            "Disabled 'Auto assign bot role' feature on {GuildName} [{GuildId}] server because I don't have role management permissions",
                            user.Guild.Name,
                            user.Guild.Id);

                        await DisableAabrAsync(user.Guild.Id).ConfigureAwait(false);
                        continue;
                    }
                    catch
                    {
                        Log.Warning("Error in aar. Probably one of the roles doesn't exist");
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

                        Log.Warning(
                            "Disabled 'Auto assign  role' feature on {GuildName} [{GuildId}] server the roles dont exist",
                            user.Guild.Name,
                            user.Guild.Id);

                        await DisableAarAsync(user.Guild.Id).ConfigureAwait(false);
                    }
                    catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
                    {
                        Log.Warning(
                            "Disabled 'Auto assign bot role' feature on {GuildName} [{GuildId}] server because I don't have role management permissions",
                            user.Guild.Name,
                            user.Guild.Id);

                        await DisableAarAsync(user.Guild.Id).ConfigureAwait(false);
                    }
                    catch
                    {
                        Log.Warning("Error in aar. Probably one of the roles doesn't exist");
                    }
                }
            }
        });

        eventHandler.UserJoined += OnClientOnUserJoined;
        eventHandler.GuildMemberUpdated += OnClientOnGuildMemberUpdated;
        eventHandler.RoleDeleted += OnClientRoleDeleted;
    }

    /// <summary>
    /// Event handler for when a guild member is updated.
    /// </summary>
    /// <param name="args">The cached user.</param>
    /// <param name="arsg2">The updated user.</param>
    private async Task OnClientOnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> args, SocketGuildUser arsg2)
    {
        if (arsg2.IsBot)
            return;
        if (arsg2.IsPending.HasValue && !arsg2.IsPending.Value)
            await assignQueue.Writer.WriteAsync(arsg2).ConfigureAwait(false);
    }

    /// <summary>
    /// Event handler for when a role is deleted in a guild.
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
    /// Event handler for when a user joins a guild.
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
    /// Toggles the auto-assign role for a given guild and role.
    /// </summary>
    /// <param name="guildId">The ID of the guild to toggle the auto-assign role for.</param>
    /// <param name="roleId">The ID of the role to toggle.</param>
    /// <returns>A list of role IDs that are currently set to auto-assign in the guild.</returns>
    public async Task<IReadOnlyList<ulong>> ToggleAarAsync(ulong guildId, ulong roleId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);
        var roles = gc.GetAutoAssignableRoles();
        if (!roles.Remove(roleId) && roles.Count < 10)
            roles.Add(roleId);

        gc.SetAutoAssignableRoles(roles);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guildId, gc);

        return roles;
    }

    /// <summary>
    /// Disables the auto-assign role feature for a given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to disable the auto-assign role feature for.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task DisableAarAsync(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);
        gc.AutoAssignRoleId = "";
        await guildSettings.UpdateGuildConfig(guildId, gc);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the auto-assign bot roles for a given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to set the auto-assign bot roles for.</param>
    /// <param name="newRoles">The new roles to set as auto-assign bot roles.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetAabrRolesAsync(ulong guildId, IEnumerable<ulong> newRoles)
    {
        await using var uow = db.GetDbContext();

        var gc = await uow.ForGuildId(guildId, set => set);
        gc.SetAutoAssignableBotRoles(newRoles);
        await guildSettings.UpdateGuildConfig(guildId, gc);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Toggles the auto-assign bot role for a given guild and role.
    /// </summary>
    /// <param name="guildId">The ID of the guild to toggle the auto-assign bot role for.</param>
    /// <param name="roleId">The ID of the role to toggle.</param>
    /// <returns>A list of role IDs that are currently set to auto-assign bot roles in the guild.</returns>
    public async Task<IReadOnlyList<ulong>> ToggleAabrAsync(ulong guildId, ulong roleId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);
        var roles = gc.GetAutoAssignableBotRoles();
        if (!roles.Remove(roleId) && roles.Count < 10)
            roles.Add(roleId);

        gc.SetAutoAssignableBotRoles(roles);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guildId, gc);

        return roles;
    }

    /// <summary>
    /// Disables the auto-assign bot role feature for a given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to disable the auto-assign bot role feature for.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DisableAabrAsync(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);
        gc.AutoBotRoleIds = " ";
        await guildSettings.UpdateGuildConfig(guildId, gc);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the auto-assign roles for a given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to set the auto-assign roles for.</param>
    /// <param name="newRoles">The new roles to set as auto-assign roles.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetAarRolesAsync(ulong guildId, IEnumerable<ulong> newRoles)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);
        gc.SetAutoAssignableRoles(newRoles);
        await guildSettings.UpdateGuildConfig(guildId, gc);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves the list of normal roles that are set to auto-assign for a given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve the auto-assign roles for.</param>
    /// <returns>A list of role IDs that are currently set to auto-assign in the guild.</returns>
    public async Task<IEnumerable<ulong>> TryGetNormalRoles(ulong guildId)
    {
        var tocheck = (await guildSettings.GetGuildConfig(guildId)).AutoAssignRoleId;
        return string.IsNullOrWhiteSpace(tocheck) ? new List<ulong>() : tocheck.Split(" ").Select(ulong.Parse).ToList();
    }

    /// <summary>
    /// Retrieves the list of bot roles that are set to auto-assign for a given guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve the auto-assign bot roles for.</param>
    /// <returns>A list of role IDs that are currently set to auto-assign bot roles in the guild.</returns>
    public async Task<IEnumerable<ulong>> TryGetBotRoles(ulong guildId)
    {
        var tocheck = (await guildSettings.GetGuildConfig(guildId)).AutoBotRoleIds;
        return string.IsNullOrWhiteSpace(tocheck) ? new List<ulong>() : tocheck.Split(" ").Select(ulong.Parse).ToList();
    }
}

/// <summary>
/// Extension methods for the GuildConfig class.
/// </summary>
public static class GuildConfigExtensions
{
    /// <summary>
    /// Retrieves the list of roles that are set to auto-assign for a given guild configuration.
    /// </summary>
    /// <param name="gc">The guild configuration to retrieve the auto-assign roles for.</param>
    /// <returns>A list of role IDs that are currently set to auto-assign in the guild configuration.</returns>
    public static List<ulong> GetAutoAssignableRoles(this GuildConfig gc)
        => string.IsNullOrWhiteSpace(gc.AutoAssignRoleId)
            ? new List<ulong>()
            : gc.AutoAssignRoleId.Split(" ").Select(ulong.Parse).ToList();

    /// <summary>
    /// Sets the auto-assign roles for a given guild configuration.
    /// </summary>
    /// <param name="gc">The guild configuration to set the auto-assign roles for.</param>
    /// <param name="roles">The new roles to set as auto-assign roles.</param>
    public static void SetAutoAssignableRoles(this GuildConfig gc, IEnumerable<ulong> roles) =>
        gc.AutoAssignRoleId = roles.JoinWith(" ");

    /// <summary>
    /// Retrieves the list of bot roles that are set to auto-assign for a given guild configuration.
    /// </summary>
    /// <param name="gc">The guild configuration to retrieve the auto-assign bot roles for.</param>
    /// <returns>A list of role IDs that are currently set to auto-assign bot roles in the guild configuration.</returns>
    public static List<ulong> GetAutoAssignableBotRoles(this GuildConfig gc)
        => string.IsNullOrWhiteSpace(gc.AutoBotRoleIds)
            ? new List<ulong>()
            : gc.AutoBotRoleIds.Split(" ").Select(ulong.Parse).ToList();

    /// <summary>
    /// Sets the auto-assign bot roles for a given guild configuration.
    /// </summary>
    /// <param name="gc">The guild configuration to set the auto-assign bot roles for.</param>
    /// <param name="roles">The new roles to set as auto-assign bot roles.</param>
    public static void SetAutoAssignableBotRoles(this GuildConfig gc, IEnumerable<ulong> roles) =>
        gc.AutoBotRoleIds = roles.JoinWith(" ");
}