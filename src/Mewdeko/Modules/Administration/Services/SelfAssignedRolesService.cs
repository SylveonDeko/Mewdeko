using Mewdeko.Modules.Xp.Common;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
/// The service for managing self-assigned roles.
/// </summary>
public class SelfAssignedRolesService : INService
{
    /// <summary>
    /// Enum representing the possible results of an assign operation.
    /// </summary>
    public enum AssignResult
    {
        Assigned, // successfully removed
        ErrNotAssignable, // not assignable (error)
        ErrAlreadyHave, // you already have that role (error)
        ErrNotPerms, // bot doesn't have perms (error)
        ErrLvlReq // you are not required level (error)
    }

    /// <summary>
    /// Enum representing the possible results of a remove operation.
    /// </summary>
    public enum RemoveResult
    {
        Removed, // successfully removed
        ErrNotAssignable, // not assignable (error)
        ErrNotHave, // you don't have a role you want to remove (error)
        ErrNotPerms // bot doesn't have perms (error)
    }

    /// <summary>
    /// The database service.
    /// </summary>
    private readonly DbService db;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelfAssignedRolesService"/> class.
    /// </summary>
    /// <param name="db">The database service.</param>
    public SelfAssignedRolesService(DbService db) => this.db = db;

    /// <summary>
    /// Adds a new self-assignable role to a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to add the role to.</param>
    /// <param name="role">The role to add.</param>
    /// <param name="group">The group number for the role.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating whether the operation was successful.</returns>
    public async Task<bool> AddNew(ulong guildId, IRole role, int group)
    {
        await using var uow = db.GetDbContext();
        var roles = await uow.SelfAssignableRoles.GetFromGuild(guildId);
        if (roles.Any(s => s.RoleId == role.Id && s.GuildId == role.Guild.Id)) return false;

        uow.SelfAssignableRoles.Add(new SelfAssignedRole
        {
            Group = group, RoleId = role.Id, GuildId = role.Guild.Id
        });
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Toggles the auto-deletion of self-assigned role messages for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to toggle the setting for.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating the new value of the setting.</returns>
    public async Task<bool> ToggleAdSarm(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var config = await uow.ForGuildId(guildId, set => set);

        // convert the long to bool for processing
        var currentVal = config.AutoDeleteSelfAssignedRoleMessages != 0;
        var newVal = !currentVal;

        // convert the bool back to long for storage
        config.AutoDeleteSelfAssignedRoleMessages = newVal ? 1 : 0;

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return newVal;
    }

    /// <summary>
    /// Assigns a self-assignable role to a guild user.
    /// </summary>
    /// <param name="guildUser">The guild user to assign the role to.</param>
    /// <param name="role">The role to assign.</param>
    /// <returns>A task that represents the asynchronous operation and contains the result of the operation, a boolean indicating whether auto-deletion is enabled, and an extra object containing additional information about the operation.</returns>
    public async Task<(AssignResult Result, bool AutoDelete, object extra)> Assign(IGuildUser guildUser, IRole role)
    {
        LevelStats userLevelData;
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var stats = await uow.UserXpStats.GetOrCreateUser(guildUser.Guild.Id, guildUser.Id);
            userLevelData = new LevelStats(stats.Xp + stats.AwardedXp);
        }

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
    /// Sets the name of a self-assignable role group in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to set the name for.</param>
    /// <param name="group">The group number to set the name for.</param>
    /// <param name="name">The new name for the group.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating whether the operation was successful.</returns>
    public async Task<bool> SetNameAsync(ulong guildId, int group, string name)
    {
        var set = false;
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, y => y.Include(x => x.SelfAssignableRoleGroupNames));
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

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return set;
    }

    /// <summary>
    /// Removes a self-assignable role from a guild user.
    /// </summary>
    /// <param name="guildUser">The guild user to remove the role from.</param>
    /// <param name="role">The role to remove.</param>
    /// <returns>A task that represents the asynchronous operation and contains the result of the operation and a boolean indicating whether auto-deletion is enabled.</returns>
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
    /// Removes a self-assignable role from a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to remove the role from.</param>
    /// <param name="roleId">The ID of the role to remove.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating whether the operation was successful.</returns>
    public async Task<bool> RemoveSar(ulong guildId, ulong roleId)
    {
        await using var uow = db.GetDbContext();
        var success = await uow.SelfAssignableRoles.DeleteByGuildAndRoleId(guildId, roleId);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return success;
    }

    /// <summary>
    /// Retrieves the auto-delete, exclusive, and self-assignable roles for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve the information for.</param>
    /// <returns>A task that represents the asynchronous operation and contains a tuple with a boolean indicating whether auto-deletion is enabled, a boolean indicating whether exclusive self-assignable roles are enabled, and a collection of self-assignable roles.</returns>
    public async Task<(bool AutoDelete, bool Exclusive, IEnumerable<SelfAssignedRole>)> GetAdAndRoles(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);
        var autoDelete = false.ParseBoth(gc.AutoDeleteSelfAssignedRoleMessages.ToString());
        var exclusive = false.ParseBoth(gc.ExclusiveSelfAssignedRoles.ToString());
        var roles = await uow.SelfAssignableRoles.GetFromGuild(guildId);

        return (autoDelete, exclusive, roles);
    }

    /// <summary>
    /// Sets the level requirement for a self-assignable role in a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to set the level requirement in.</param>
    /// <param name="role">The role to set the level requirement for.</param>
    /// <param name="level">The new level requirement.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating whether the operation was successful.</returns>
    public async Task<bool> SetLevelReq(ulong guildId, IRole role, int level)
    {
        await using var uow = db.GetDbContext();
        var roles = await uow.SelfAssignableRoles.GetFromGuild(guildId);
        var sar = roles.FirstOrDefault(x => x.RoleId == role.Id);
        if (sar != null)
        {
            sar.LevelRequirement = level;
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
        else
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Toggles the exclusive self-assignable roles setting for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to toggle the setting for.</param>
    /// <returns>A task that represents the asynchronous operation and contains a boolean indicating the new value of the setting.</returns>
    public async Task<bool> ToggleEsar(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var config = await uow.ForGuildId(guildId, set => set);

        // Use a ternary operator to toggle the value
        config.ExclusiveSelfAssignedRoles = config.ExclusiveSelfAssignedRoles == 0L ? 1L : 0L;

        await uow.SaveChangesAsync().ConfigureAwait(false);

        // Return the boolean equivalent of the new value
        return config.ExclusiveSelfAssignedRoles != 0;
    }

    /// <summary>
    /// Retrieves the exclusive setting, self-assignable roles, and group names for a guild.
    /// </summary>
    /// <param name="guild">The guild to retrieve the information for.</param>
    /// <returns>A task that represents the asynchronous operation and contains a tuple with a boolean indicating whether exclusive self-assignable roles are enabled, a collection of tuples containing self-assignable role models and their corresponding roles, and a dictionary mapping group numbers to their names.</returns>
    public async
        Task<(bool Exclusive, IEnumerable<(SelfAssignedRole Model, IRole Role)> Roles, IDictionary<int, string>
            GroupNames)>
        GetRoles(IGuild guild)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set.Include(x => x.SelfAssignableRoleGroupNames));
        var exclusive = false.ParseBoth(gc.ExclusiveSelfAssignedRoles.ToString());
        IDictionary<int, string> groupNames = gc.SelfAssignableRoleGroupNames.ToDictionary(x => x.Number, x => x.Name);
        var roleModels = await uow.SelfAssignableRoles.GetFromGuild(guild.Id);
        var roles = roleModels
            .Select(x => (Model: x, Role: guild.GetRole(x.RoleId)));
        uow.SelfAssignableRoles.RemoveRange(roles.Where(x => x.Role.Name == null).Select(x => x.Model).ToArray());
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return (exclusive, roles.Where(x => x.Role.Name != null), groupNames);
    }
}