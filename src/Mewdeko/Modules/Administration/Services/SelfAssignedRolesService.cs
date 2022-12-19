using System.Threading.Tasks;
using Mewdeko.Modules.Xp.Common;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Administration.Services;

public class SelfAssignedRolesService : INService
{
    public enum AssignResult
    {
        Assigned, // successfully removed
        ErrNotAssignable, // not assignable (error)
        ErrAlreadyHave, // you already have that role (error)
        ErrNotPerms, // bot doesn't have perms (error)
        ErrLvlReq // you are not required level (error)
    }

    public enum RemoveResult
    {
        Removed, // successfully removed
        ErrNotAssignable, // not assignable (error)
        ErrNotHave, // you don't have a role you want to remove (error)
        ErrNotPerms // bot doesn't have perms (error)
    }

    private readonly DbService db;

    public SelfAssignedRolesService(DbService db) => this.db = db;

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

    public async Task<bool> ToggleAdSarm(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var config = await uow.ForGuildId(guildId, set => set);
        var newval = config.AutoDeleteSelfAssignedRoleMessages = !config.AutoDeleteSelfAssignedRoleMessages;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return newval;
    }

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

    public async Task<bool> RemoveSar(ulong guildId, ulong roleId)
    {
        await using var uow = db.GetDbContext();
        var success = await uow.SelfAssignableRoles.DeleteByGuildAndRoleId(guildId, roleId);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return success;
    }

    public async Task<(bool AutoDelete, bool Exclusive, IEnumerable<SelfAssignedRole>)> GetAdAndRoles(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);
        var autoDelete = gc.AutoDeleteSelfAssignedRoleMessages;
        var exclusive = gc.ExclusiveSelfAssignedRoles;
        var roles = await uow.SelfAssignableRoles.GetFromGuild(guildId);

        return (autoDelete, exclusive, roles);
    }

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

    public async Task<bool> ToggleEsar(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var config = await uow.ForGuildId(guildId, set => set);

        var areExclusive = config.ExclusiveSelfAssignedRoles = !config.ExclusiveSelfAssignedRoles;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return areExclusive;
    }

    public async Task<(bool Exclusive, IEnumerable<(SelfAssignedRole Model, IRole Role)> Roles, IDictionary<int, string> GroupNames)> GetRoles(IGuild guild)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set.Include(x => x.SelfAssignableRoleGroupNames));
        var exclusive = gc.ExclusiveSelfAssignedRoles;
        IDictionary<int, string> groupNames = gc.SelfAssignableRoleGroupNames.ToDictionary(x => x.Number, x => x.Name);
        var roleModels = await uow.SelfAssignableRoles.GetFromGuild(guild.Id);
        var roles = roleModels
            .Select(x => (Model: x, Role: guild.GetRole(x.RoleId)));
        uow.SelfAssignableRoles.RemoveRange(roles.Where(x => x.Role.Name == null).Select(x => x.Model).ToArray());
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return (exclusive, roles.Where(x => x.Role.Name != null), groupNames);
    }
}