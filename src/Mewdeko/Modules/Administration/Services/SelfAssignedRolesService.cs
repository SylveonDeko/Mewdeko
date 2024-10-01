using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Xp.Common;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     The service for managing self-assigned roles.
/// </summary>
public class SelfAssignedRolesService(DbContextProvider dbProvider) : INService
{
    /// <summary>
    ///     Enum representing the possible results of an assign operation.
    /// </summary>
    public enum AssignResult
    {
        /// <summary>
        ///     The role was successfully assigned.
        /// </summary>
        Assigned, // successfully removed

        /// <summary>
        ///     The role is not assignable.
        /// </summary>
        ErrNotAssignable, // not assignable (error)

        /// <summary>
        ///     The user already has the role.
        /// </summary>
        ErrAlreadyHave, // you already have that role (error)

        /// <summary>
        ///     The bot doesn't have the necessary permissions.
        /// </summary>
        ErrNotPerms, // bot doesn't have perms (error)

        /// <summary>
        ///     The user does not meet the level requirement.
        /// </summary>
        ErrLvlReq // you are not required level (error)
    }

    /// <summary>
    ///     Enum representing the possible results of a remove operation.
    /// </summary>
    public enum RemoveResult
    {
        /// <summary>
        ///     The role was successfully removed.
        /// </summary>
        Removed, // successfully removed

        /// <summary>
        ///     The role is not assignable.
        /// </summary>
        ErrNotAssignable, // not assignable (error)

        /// <summary>
        ///     The user does not have the role.
        /// </summary>
        ErrNotHave, // you don't have a role you want to remove (error)

        /// <summary>
        ///     The bot doesn't have the necessary permissions.
        /// </summary>
        ErrNotPerms // bot doesn't have perms (error)
    }

    /// <summary>
    ///     Adds a new self-assignable role to a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to add the role to.</param>
    /// <param name="role">The role to add.</param>
    /// <param name="group">The group number for the role.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> AddNew(ulong guildId, IRole role, int group)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var roles = await dbContext.SelfAssignableRoles.GetFromGuild(guildId);
        if (roles.Any(s => s.RoleId == role.Id && s.GuildId == role.Guild.Id)) return false;

        dbContext.SelfAssignableRoles.Add(new SelfAssignedRole
        {
            Group = group, RoleId = role.Id, GuildId = role.Guild.Id
        });
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    /// <summary>
    ///     Toggles the auto-deletion of self-assigned role messages for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to toggle the setting for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating the new value of the
    ///     setting.
    /// </returns>
    public async Task<bool> ToggleAdSarm(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var config = await dbContext.ForGuildId(guildId, set => set);

        config.AutoDeleteSelfAssignedRoleMessages = !config.AutoDeleteSelfAssignedRoleMessages;

        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        return config.AutoDeleteSelfAssignedRoleMessages;
    }

    /// <summary>
    ///     Assigns a self-assignable role to a guild user.
    /// </summary>
    /// <param name="guildUser">The guild user to assign the role to.</param>
    /// <param name="role">The role to assign.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains the result of the operation, a boolean
    ///     indicating whether auto-deletion is enabled, and an extra object containing additional information about the
    ///     operation.
    /// </returns>
    public async Task<(AssignResult Result, bool AutoDelete, object extra)> Assign(IGuildUser guildUser, IRole role)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var stats = await dbContext.UserXpStats.GetOrCreateUser(guildUser.Guild.Id, guildUser.Id);
        var userLevelData = new LevelStats(stats.Xp + stats.AwardedXp);

        var (autoDelete, exclusive, roles) = await GetAdAndRoles(guildUser.Guild.Id);

        var selfAssignedRoles = roles as SelfAssignedRole[] ?? roles.ToArray();
        var theRoleYouWant = Array.Find(selfAssignedRoles, r => r.RoleId == role.Id);
        if (theRoleYouWant == null)
            return (AssignResult.ErrNotAssignable, autoDelete, null);
        if (theRoleYouWant.LevelRequirement > userLevelData.Level)
            return (AssignResult.ErrLvlReq, autoDelete, theRoleYouWant.LevelRequirement);
        if (guildUser.RoleIds.Contains(role.Id)) return (AssignResult.ErrAlreadyHave, autoDelete, null);

        var roleIds = selfAssignedRoles
            .Where(x => x.Group == theRoleYouWant.Group)
            .Select(x => x.RoleId).ToArray();
        if (exclusive)
        {
            var sameRoles = guildUser.RoleIds
                .Where(r => roleIds.Contains(r));

            foreach (var roleId in sameRoles)
            {
                var sameRole = guildUser.Guild.GetRole(roleId);
                if (sameRole == null) continue;
                try
                {
                    await guildUser.RemoveRoleAsync(sameRole).ConfigureAwait(false);
                    await Task.Delay(300).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }
        }

        try
        {
            await guildUser.AddRoleAsync(role).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return (AssignResult.ErrNotPerms, autoDelete, ex);
        }

        return (AssignResult.Assigned, autoDelete, null);
    }

    /// <summary>
    ///     Sets the name of a self-assignable role group in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to set the name for.</param>
    /// <param name="group">The group number to set the name for.</param>
    /// <param name="name">The new name for the group.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> SetNameAsync(ulong guildId, int group, string name)
    {
        var set = false;

        await using var dbContext = await dbProvider.GetContextAsync();
        var gc = await dbContext.ForGuildId(guildId, y => y.Include(x => x.SelfAssignableRoleGroupNames));
        var toUpdate = gc.SelfAssignableRoleGroupNames.Find(x => x.Number == group);

        if (string.IsNullOrWhiteSpace(name))
        {
            if (toUpdate != null)
                gc.SelfAssignableRoleGroupNames.Remove(toUpdate);
        }
        else if (toUpdate == null)
        {
            gc.SelfAssignableRoleGroupNames.Add(new GroupName
            {
                Name = name, Number = group
            });
            set = true;
        }
        else
        {
            toUpdate.Name = name;
            set = true;
        }

        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        return set;
    }

    /// <summary>
    ///     Removes a self-assignable role from a guild user.
    /// </summary>
    /// <param name="guildUser">The guild user to remove the role from.</param>
    /// <param name="role">The role to remove.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains the result of the operation and a boolean
    ///     indicating whether auto-deletion is enabled.
    /// </returns>
    public async Task<(RemoveResult Result, bool AutoDelete)> Remove(IGuildUser guildUser, IRole role)
    {
        var (autoDelete, _, roles) = await GetAdAndRoles(guildUser.Guild.Id);

        if (roles.FirstOrDefault(r => r.RoleId == role.Id) == null)
            return (RemoveResult.ErrNotAssignable, autoDelete);
        if (!guildUser.RoleIds.Contains(role.Id)) return (RemoveResult.ErrNotHave, autoDelete);
        try
        {
            await guildUser.RemoveRoleAsync(role).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return (RemoveResult.ErrNotPerms, autoDelete);
        }

        return (RemoveResult.Removed, autoDelete);
    }

    /// <summary>
    ///     Removes a self-assignable role from a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to remove the role from.</param>
    /// <param name="roleId">The ID of the role to remove.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> RemoveSar(ulong guildId, ulong roleId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var success = await dbContext.SelfAssignableRoles.DeleteByGuildAndRoleId(guildId, roleId);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        return success;
    }

    /// <summary>
    ///     Retrieves the auto-delete, exclusive, and self-assignable roles for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve the information for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a tuple with a boolean indicating whether
    ///     auto-deletion is enabled, a boolean indicating whether exclusive self-assignable roles are enabled, and a
    ///     collection of self-assignable roles.
    /// </returns>
    public async Task<(bool AutoDelete, bool Exclusive, IEnumerable<SelfAssignedRole>)> GetAdAndRoles(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var gc = await dbContext.ForGuildId(guildId, set => set);
        var roles = await dbContext.SelfAssignableRoles.GetFromGuild(guildId);

        return (gc.AutoDeleteSelfAssignedRoleMessages, gc.ExclusiveSelfAssignedRoles, roles);
    }

    /// <summary>
    ///     Sets the level requirement for a self-assignable role in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to set the level requirement in.</param>
    /// <param name="role">The role to set the level requirement for.</param>
    /// <param name="level">The new level requirement.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating whether the operation was
    ///     successful.
    /// </returns>
    public async Task<bool> SetLevelReq(ulong guildId, IRole role, int level)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var roles = await dbContext.SelfAssignableRoles.GetFromGuild(guildId);
        var sar = roles.FirstOrDefault(x => x.RoleId == role.Id);
        if (sar != null)
        {
            sar.LevelRequirement = level;
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
        }
        else
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Toggles the exclusive self-assignable roles setting for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to toggle the setting for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a boolean indicating the new value of the
    ///     setting.
    /// </returns>
    public async Task<bool> ToggleEsar(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var config = await dbContext.ForGuildId(guildId, set => set);
        config.ExclusiveSelfAssignedRoles = !config.ExclusiveSelfAssignedRoles;
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        return config.ExclusiveSelfAssignedRoles;
    }

    /// <summary>
    ///     Retrieves the exclusive setting, self-assignable roles, and group names for a guild.
    /// </summary>
    /// <param name="guild">The guild to retrieve the information for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and contains a tuple with a boolean indicating whether
    ///     exclusive self-assignable roles are enabled, a collection of tuples containing self-assignable role models and
    ///     their corresponding roles, and a dictionary mapping group numbers to their names.
    /// </returns>
    public async
        Task<(bool Exclusive, IEnumerable<(SelfAssignedRole Model, IRole Role)> Roles, IDictionary<int, string>
            GroupNames)>
        GetRoles(IGuild guild)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var gc = await dbContext.ForGuildId(guild.Id, set => set.Include(x => x.SelfAssignableRoleGroupNames));
        IDictionary<int, string> groupNames = gc.SelfAssignableRoleGroupNames.ToDictionary(x => x.Number, x => x.Name);
        var roleModels = await dbContext.SelfAssignableRoles.GetFromGuild(guild.Id);
        var roles = roleModels
            .Select(x => (Model: x, Role: guild.GetRole(x.RoleId)));
        dbContext.SelfAssignableRoles.RemoveRange(roles.Where(x => x.Role.Name == null).Select(x => x.Model).ToArray());
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        return (gc.ExclusiveSelfAssignedRoles, roles.Where(x => x.Role.Name != null), groupNames);
    }
}