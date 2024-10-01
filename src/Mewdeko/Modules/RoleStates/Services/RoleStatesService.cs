using Mewdeko.Database.DbContextStuff;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.RoleStates.Services;

/// <summary>
///     Provides services for managing user role states within a guild. This includes saving roles before a user leaves or
///     is banned, and optionally restoring them upon rejoining.
/// </summary>
public class RoleStatesService : INService
{
    private readonly DbContextProvider dbProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RoleStatesService" /> class.
    /// </summary>
    /// <param name="dbContext">The database service to interact with stored data.</param>
    /// <param name="eventHandler">The event handler to subscribe to guild member events.</param>
    public RoleStatesService(DbContextProvider dbProvider, EventHandler eventHandler)
    {
        this.dbProvider = dbProvider;
        eventHandler.UserLeft += OnUserLeft;
        eventHandler.UserBanned += OnUserBanned;
        eventHandler.UserJoined += OnUserJoined;
    }

    private async Task OnUserBanned(SocketUser args, SocketGuild arsg2)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        if (args is not SocketGuildUser usr) return;
        var roleStateSettings = await dbContext.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == arsg2.Id);
        if (roleStateSettings is null || !roleStateSettings.Enabled || !roleStateSettings.ClearOnBan) return;

        var roleState =
            await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == arsg2.Id && x.UserId == usr.Id);
        if (roleState is null) return;
        dbContext.Remove(roleState);
        await dbContext.SaveChangesAsync();
    }

    private async Task OnUserJoined(IGuildUser usr)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var roleStateSettings = await dbContext.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == usr.Guild.Id);
        if (roleStateSettings is null || !roleStateSettings.Enabled) return;

        if (roleStateSettings.IgnoreBots && usr.IsBot) return;

        var deniedUsers = string.IsNullOrWhiteSpace(roleStateSettings.DeniedUsers)
            ? []
            : roleStateSettings.DeniedUsers.Split(',').Select(ulong.Parse).ToList();

        if (deniedUsers.Contains(usr.Id)) return;

        var roleState =
            await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == usr.Guild.Id && x.UserId == usr.Id);
        if (roleState is null || string.IsNullOrWhiteSpace(roleState.SavedRoles)) return;

        var savedRoleIds = roleState.SavedRoles.Split(',').Select(ulong.Parse).ToList();

        if (savedRoleIds.Any())
        {
            try
            {
                await usr.AddRolesAsync(savedRoleIds);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to assign roles to {User} in {Guild}. Most likely missing permissions\n{Exception}",
                    usr.Username, usr.Guild, ex);
            }
        }
    }


    private async Task OnUserLeft(IGuild args, IUser args2)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var roleStateSettings = await dbContext.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == args.Id);
        if (roleStateSettings is null || !roleStateSettings.Enabled) return;

        if (roleStateSettings.IgnoreBots && args2.IsBot) return;

        var deniedRoles = string.IsNullOrWhiteSpace(roleStateSettings.DeniedRoles)
            ? []
            : roleStateSettings.DeniedRoles.Split(',').Select(ulong.Parse).ToList();

        var deniedUsers = string.IsNullOrWhiteSpace(roleStateSettings.DeniedUsers)
            ? []
            : roleStateSettings.DeniedUsers.Split(',').Select(ulong.Parse).ToList();

        if (deniedUsers.Contains(args2.Id)) return;

        if (args2 is not SocketGuildUser usr) return;

        var rolesToSave = usr.Roles.Where(x => !x.IsManaged && !x.IsEveryone).Select(x => x.Id);
        if (deniedRoles.Any())
        {
            rolesToSave = rolesToSave.Except(deniedRoles);
        }

        if (!rolesToSave.Any()) return;

        var roleState =
            await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == args.Id && x.UserId == usr.Id);
        if (roleState is null)
        {
            var newRoleState = new UserRoleStates
            {
                UserName = usr.ToString(),
                GuildId = args.Id,
                UserId = usr.Id,
                SavedRoles = string.Join(",", rolesToSave)
            };
            await dbContext.UserRoleStates.AddAsync(newRoleState);
        }
        else
        {
            roleState.SavedRoles = string.Join(",", rolesToSave);
            dbContext.Update(roleState);
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    ///     Toggles the role state feature on or off for a guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <returns>A task that represents the asynchronous operation, containing a boolean indicating the operation success.</returns>
    public async Task<bool> ToggleRoleStates(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var roleStateSettings = await dbContext.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (roleStateSettings is null)
        {
            var toAdd = new RoleStateSettings
            {
                GuildId = guildId, Enabled = true
            };
            await dbContext.RoleStateSettings.AddAsync(toAdd);
            await dbContext.SaveChangesAsync();
            return true;
        }

        roleStateSettings.Enabled = !roleStateSettings.Enabled;
        dbContext.RoleStateSettings.Update(roleStateSettings);
        await dbContext.SaveChangesAsync();
        return roleStateSettings.Enabled;
    }

    /// <summary>
    ///     Retrieves the role state settings for a guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing the <see cref="RoleStateSettings" /> or null if
    ///     not found.
    /// </returns>
    public async Task<RoleStateSettings?> GetRoleStateSettings(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        return await dbContext.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == guildId) ?? null;
    }

    /// <summary>
    ///     Retrieves a user's saved role state within a guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing the <see cref="UserRoleStates" /> or null if not
    ///     found.
    /// </returns>
    public async Task<UserRoleStates?> GetUserRoleState(ulong guildId, ulong userId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        return await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId) ??
               null;
    }

    /// <summary>
    ///     Retrieves all user role states within a guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <returns>A task that represents the asynchronous operation, containing a list of <see cref="UserRoleStates" />.</returns>
    public async Task<List<UserRoleStates>> GetAllUserRoleStates(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        return await dbContext.UserRoleStates.Where(x => x.GuildId == guildId).ToListAsync();
    }

    /// <summary>
    ///     Updates the role state settings for a guild.
    /// </summary>
    /// <param name="roleStateSettings">The <see cref="RoleStateSettings" /> to be updated.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task UpdateRoleStateSettings(RoleStateSettings roleStateSettings)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        dbContext.RoleStateSettings.Update(roleStateSettings);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    ///     Toggles the option to ignore bots when saving and restoring roles.
    /// </summary>
    /// <param name="roleStateSettings">The <see cref="RoleStateSettings" /> to be updated.</param>
    /// <returns>A task that represents the asynchronous operation, containing a boolean indicating if bots are now ignored.</returns>
    public async Task<bool> ToggleIgnoreBots(RoleStateSettings roleStateSettings)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        roleStateSettings.IgnoreBots = !roleStateSettings.IgnoreBots;

        dbContext.RoleStateSettings.Update(roleStateSettings);
        await dbContext.SaveChangesAsync();

        return roleStateSettings.IgnoreBots;
    }

    /// <summary>
    ///     Toggles the option to clear saved roles upon a user's ban.
    /// </summary>
    /// <param name="roleStateSettings">The <see cref="RoleStateSettings" /> to be updated.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a boolean indicating if roles are cleared on
    ///     ban.
    /// </returns>
    public async Task<bool> ToggleClearOnBan(RoleStateSettings roleStateSettings)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var previousClearOnBanValue = roleStateSettings.ClearOnBan;
        roleStateSettings.ClearOnBan = !roleStateSettings.ClearOnBan;

        dbContext.RoleStateSettings.Update(roleStateSettings);
        await dbContext.SaveChangesAsync();

        return roleStateSettings.ClearOnBan;
    }

    /// <summary>
    ///     Adds roles to a user's saved role state.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="roleIds">The roles to be added.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a tuple with a boolean indicating success and an
    ///     optional error message.
    /// </returns>
    public async Task<(bool, string)> AddRolesToUserRoleState(ulong guildId, ulong userId, IEnumerable<ulong> roleIds)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var userRoleState =
            await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);

        if (userRoleState == null)
        {
            return (false, "No role state found for this user.");
        }

        var savedRoleIds = userRoleState.SavedRoles.Split(',').Select(ulong.Parse).ToList();
        var anyRoleAdded = false;

        foreach (var roleId in roleIds.Where(roleId => !savedRoleIds.Contains(roleId)))
        {
            savedRoleIds.Add(roleId);
            anyRoleAdded = true;
        }

        if (!anyRoleAdded)
        {
            return (false, "No roles to add.");
        }

        userRoleState.SavedRoles = string.Join(",", savedRoleIds);
        dbContext.Update(userRoleState);
        await dbContext.SaveChangesAsync();

        return (true, "");
    }

    /// <summary>
    ///     Removes roles from a user's saved role state.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="roleIds">The roles to be removed.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a tuple with a boolean indicating success and an
    ///     optional error message.
    /// </returns>
    public async Task<(bool, string)> RemoveRolesFromUserRoleState(ulong guildId, ulong userId,
        IEnumerable<ulong> roleIds)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var userRoleState =
            await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);

        if (userRoleState == null)
        {
            return (false, "No role state found for this user.");
        }

        var savedRoleIds = userRoleState.SavedRoles.Split(',').Select(ulong.Parse).ToList();
        var anyRoleRemoved = false;

        foreach (var roleId in roleIds.Where(roleId => savedRoleIds.Contains(roleId)))
        {
            savedRoleIds.Remove(roleId);
            anyRoleRemoved = true;
        }

        if (!anyRoleRemoved)
        {
            return (false, "No roles to remove.");
        }

        userRoleState.SavedRoles = string.Join(",", savedRoleIds);
        dbContext.Update(userRoleState);
        await dbContext.SaveChangesAsync();

        return (true, "");
    }

    /// <summary>
    ///     Deletes a user's role state.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a boolean indicating if the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> DeleteUserRoleState(ulong userId, ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var userRoleState =
            await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);
        if (userRoleState is null) return false;
        dbContext.UserRoleStates.Remove(userRoleState);
        await dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Applies the saved role state from one user to another.
    /// </summary>
    /// <param name="sourceUserId">The source user's unique identifier.</param>
    /// <param name="targetUser">The target <see cref="IGuildUser" />.</param>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a boolean indicating if the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> ApplyUserRoleStateToAnotherUser(ulong sourceUserId, IGuildUser targetUser, ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();


        var sourceUserRoleState =
            await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == sourceUserId);

        if (sourceUserRoleState is null || string.IsNullOrWhiteSpace(sourceUserRoleState.SavedRoles)) return false;


        var sourceUserSavedRoleIds = sourceUserRoleState.SavedRoles.Split(',').Select(ulong.Parse).ToList();
        var rolesToAssign = targetUser.Guild.Roles.Where(role => sourceUserSavedRoleIds.Contains(role.Id)).ToList();

        if (!rolesToAssign.Any()) return false;
        try
        {
            await targetUser.AddRolesAsync(rolesToAssign);
        }
        catch
        {
            Log.Error("Failed to assign roles to user {User}", targetUser.Username);
        }

        return true;
    }

    /// <summary>
    ///     Sets a user's role state manually.
    /// </summary>
    /// <param name="user">The <see cref="IUser" /> whose role state is to be set.</param>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <param name="roles">The roles to be saved.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetRoleStateManually(IUser user, ulong guildId, IEnumerable<ulong> roles)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var userRoleState =
            await dbContext.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == user.Id);
        if (userRoleState is null)
        {
            userRoleState = new UserRoleStates
            {
                GuildId = guildId, UserId = user.Id, UserName = user.ToString(), SavedRoles = string.Join(",", roles)
            };
            await dbContext.UserRoleStates.AddAsync(userRoleState);
            await dbContext.SaveChangesAsync();
        }
        else
        {
            userRoleState.SavedRoles = string.Join(",", roles);
            dbContext.UserRoleStates.Update(userRoleState);
            await dbContext.SaveChangesAsync();
        }
    }
}