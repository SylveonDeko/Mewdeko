using Discord;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
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

    private readonly DbService _db;

    public SelfAssignedRolesService(DbService db) => _db = db;

    public bool AddNew(ulong guildId, IRole role, int group)
    {
        using var uow = _db.GetDbContext();
        var roles = uow.SelfAssignableRoles.GetFromGuild(guildId);
        if (roles.Any(s => s.RoleId == role.Id && s.GuildId == role.Guild.Id)) return false;

        uow.SelfAssignableRoles.Add(new SelfAssignedRole
        {
            Group = group,
            RoleId = role.Id,
            GuildId = role.Guild.Id
        });
        uow.SaveChanges();

        return true;
    }

    public bool ToggleAdSarm(ulong guildId)
    {
        bool newval;
        using var uow = _db.GetDbContext();
        var config = uow.ForGuildId(guildId, set => set);
        newval = config.AutoDeleteSelfAssignedRoleMessages = !config.AutoDeleteSelfAssignedRoleMessages;
        uow.SaveChanges();

        return newval;
    }

    public async Task<(AssignResult Result, bool AutoDelete, object extra)> Assign(IGuildUser guildUser, IRole role)
    {
        LevelStats userLevelData;
        await using (var uow = _db.GetDbContext())
        {
            var stats = uow.UserXpStats.GetOrCreateUser(guildUser.Guild.Id, guildUser.Id);
            userLevelData = new LevelStats(stats.Xp + stats.AwardedXp);
        }

        var (autoDelete, exclusive, roles) = GetAdAndRoles(guildUser.Guild.Id);

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
                if (sameRole != null)
                {
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
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guildId, y => y.Include(x => x.SelfAssignableRoleGroupNames));
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
                Name = name,
                Number = group
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
        var (autoDelete, _, roles) = GetAdAndRoles(guildUser.Guild.Id);

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

    public bool RemoveSar(ulong guildId, ulong roleId)
    {
        bool success;
        using var uow = _db.GetDbContext();
        success = uow.SelfAssignableRoles.DeleteByGuildAndRoleId(guildId, roleId);
        uow.SaveChanges();

        return success;
    }

    public (bool AutoDelete, bool Exclusive, IEnumerable<SelfAssignedRole>) GetAdAndRoles(ulong guildId)
    {
        using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guildId, set => set);
        var autoDelete = gc.AutoDeleteSelfAssignedRoleMessages;
        var exclusive = gc.ExclusiveSelfAssignedRoles;
        var roles = uow.SelfAssignableRoles.GetFromGuild(guildId);

        return (autoDelete, exclusive, roles);
    }

    public bool SetLevelReq(ulong guildId, IRole role, int level)
    {
        using var uow = _db.GetDbContext();
        var roles = uow.SelfAssignableRoles.GetFromGuild(guildId);
        var sar = roles.FirstOrDefault(x => x.RoleId == role.Id);
        if (sar != null)
        {
            sar.LevelRequirement = level;
            uow.SaveChanges();
        }
        else
        {
            return false;
        }

        return true;
    }

    public bool ToggleEsar(ulong guildId)
    {
        bool areExclusive;
        using var uow = _db.GetDbContext();
        var config = uow.ForGuildId(guildId, set => set);

        areExclusive = config.ExclusiveSelfAssignedRoles = !config.ExclusiveSelfAssignedRoles;
        uow.SaveChanges();

        return areExclusive;
    }

    public (bool Exclusive, IEnumerable<(SelfAssignedRole Model, IRole Role)> Roles, IDictionary<int, string>
        GroupNames) GetRoles(IGuild guild)
    {
        var exclusive = false;

        IEnumerable<(SelfAssignedRole Model, IRole Role)> roles;
        IDictionary<int, string> groupNames;
        using (var uow = _db.GetDbContext())
        {
            var gc = uow.ForGuildId(guild.Id, set => set.Include(x => x.SelfAssignableRoleGroupNames));
            exclusive = gc.ExclusiveSelfAssignedRoles;
            groupNames = gc.SelfAssignableRoleGroupNames.ToDictionary(x => x.Number, x => x.Name);
            var roleModels = uow.SelfAssignableRoles.GetFromGuild(guild.Id);
            roles = roleModels
                .Select(x => (Model: x, Role: guild.GetRole(x.RoleId)));
            uow.SelfAssignableRoles.RemoveRange(roles.Where(x => x.Role == null).Select(x => x.Model).ToArray());
            uow.SaveChanges();
        }

        return (exclusive, roles.Where(x => x.Role != null), groupNames);
    }
}