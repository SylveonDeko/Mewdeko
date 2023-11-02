using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.RoleStates.Services;

public class RoleStatesService : INService
{
    private readonly DbService dbService;

    public RoleStatesService(DbService dbService, EventHandler eventHandler)
    {
        this.dbService = dbService;
        eventHandler.UserLeft += OnUserLeft;
        eventHandler.UserBanned += OnUserBanned;
        eventHandler.UserJoined += OnUserJoined;
    }

    private async Task OnUserBanned(SocketUser args, SocketGuild arsg2)
    {
        if (args is not SocketGuildUser usr) return;
        await using var db = this.dbService.GetDbContext();
        var roleStateSettings = await db.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == arsg2.Id);
        if (roleStateSettings is null || roleStateSettings.Enabled == 0 || roleStateSettings.ClearOnBan == 0) return;

        var roleState = await db.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == arsg2.Id && x.UserId == usr.Id);
        if (roleState is null) return;
        db.Remove(roleState);
        await db.SaveChangesAsync();
    }

    private async Task OnUserJoined(IGuildUser usr)
    {
        await using var db = dbService.GetDbContext();

        var roleStateSettings = await db.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == usr.Guild.Id);
        if (roleStateSettings is null || roleStateSettings.Enabled == 0) return;

        if (roleStateSettings.IgnoreBots == 1 && usr.IsBot) return;

        var deniedUsers = string.IsNullOrWhiteSpace(roleStateSettings.DeniedUsers)
            ? new List<ulong>()
            : roleStateSettings.DeniedUsers.Split(',').Select(ulong.Parse).ToList();

        if (deniedUsers.Contains(usr.Id)) return;

        var roleState =
            await db.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == usr.Guild.Id && x.UserId == usr.Id);
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
        await using var db = this.dbService.GetDbContext();
        var roleStateSettings = await db.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == args.Id);
        if (roleStateSettings is null || roleStateSettings.Enabled == 0) return;

        if (roleStateSettings.IgnoreBots == 1 && args2.IsBot) return;

        var deniedRoles = string.IsNullOrWhiteSpace(roleStateSettings.DeniedRoles)
            ? new List<ulong>()
            : roleStateSettings.DeniedRoles.Split(',').Select(ulong.Parse).ToList();

        var deniedUsers = string.IsNullOrWhiteSpace(roleStateSettings.DeniedUsers)
            ? new List<ulong>()
            : roleStateSettings.DeniedUsers.Split(',').Select(ulong.Parse).ToList();

        if (deniedUsers.Contains(args2.Id)) return;

        if (args2 is not SocketGuildUser usr) return;

        var rolesToSave = usr.Roles.Where(x => !x.IsManaged && !x.IsEveryone).Select(x => x.Id);
        if (deniedRoles.Any())
        {
            rolesToSave = rolesToSave.Except(deniedRoles);
        }

        if (!rolesToSave.Any()) return;

        var roleState = await db.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == args.Id && x.UserId == usr.Id);
        if (roleState is null)
        {
            var newRoleState = new UserRoleStates
            {
                UserName = usr.ToString(),
                GuildId = args.Id,
                UserId = usr.Id,
                SavedRoles = string.Join(",", rolesToSave),
            };
            await db.UserRoleStates.AddAsync(newRoleState);
        }
        else
        {
            roleState.SavedRoles = string.Join(",", rolesToSave);
            db.Update(roleState);
        }

        await db.SaveChangesAsync();
    }


    public async Task<bool> ToggleRoleStates(ulong guildId)
    {
        await using var db = this.dbService.GetDbContext();
        var roleStateSettings = await db.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
        if (roleStateSettings is null)
        {
            var toAdd = new RoleStateSettings
            {
                GuildId = guildId, Enabled = 1,
            };
            await db.RoleStateSettings.AddAsync(toAdd);
            await db.SaveChangesAsync();
            return true;
        }

        roleStateSettings.Enabled = roleStateSettings.Enabled == 1 ? 0 : 1;
        db.RoleStateSettings.Update(roleStateSettings);
        await db.SaveChangesAsync();
        return !false.ParseBoth(roleStateSettings.Enabled);
    }

    public async Task<RoleStateSettings?> GetRoleStateSettings(ulong guildId)
    {
        await using var db = dbService.GetDbContext();
        return await db.RoleStateSettings.FirstOrDefaultAsync(x => x.GuildId == guildId) ?? null;
    }

    public async Task<UserRoleStates?> GetUserRoleState(ulong guildId, ulong userId)
    {
        await using var db = dbService.GetDbContext();
        return await db.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId) ?? null;
    }

    public async Task<List<UserRoleStates>> GetAllUserRoleStates(ulong guildId)
    {
        return await dbService.GetDbContext().UserRoleStates.Where(x => x.GuildId == guildId).ToListAsync();
    }

    public async Task UpdateRoleStateSettings(RoleStateSettings roleStateSettings)
    {
        await using var db = dbService.GetDbContext();
        db.RoleStateSettings.Update(roleStateSettings);
        await db.SaveChangesAsync();
    }


    public async Task<bool> ToggleIgnoreBots(RoleStateSettings roleStateSettings)
    {
        await using var db = dbService.GetDbContext();

        var newIgnoreBotsValue = roleStateSettings.IgnoreBots == 1 ? 0 : 1;
        roleStateSettings.IgnoreBots = newIgnoreBotsValue;

        db.RoleStateSettings.Update(roleStateSettings);
        await db.SaveChangesAsync();

        return false.ParseBoth(newIgnoreBotsValue);
    }


    public async Task<bool> ToggleClearOnBan(RoleStateSettings roleStateSettings)
    {
        await using var db = dbService.GetDbContext();

        var previousClearOnBanValue = roleStateSettings.ClearOnBan;
        roleStateSettings.ClearOnBan = previousClearOnBanValue == 1 ? 0 : 1;

        db.RoleStateSettings.Update(roleStateSettings);
        await db.SaveChangesAsync();

        return false.ParseBoth(roleStateSettings.ClearOnBan);
    }


    public async Task<(bool, string)> AddRolesToUserRoleState(ulong guildId, ulong userId, IEnumerable<ulong> roleIds)
    {
        await using var db = dbService.GetDbContext();
        var userRoleState =
            await db.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);

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
        db.Update(userRoleState);
        await db.SaveChangesAsync();

        return (true, "");
    }

    public async Task<(bool, string)> RemoveRolesFromUserRoleState(ulong guildId, ulong userId,
        IEnumerable<ulong> roleIds)
    {
        await using var db = dbService.GetDbContext();
        var userRoleState =
            await db.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);

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
        db.Update(userRoleState);
        await db.SaveChangesAsync();

        return (true, "");
    }


    public async Task<bool> DeleteUserRoleState(ulong userId, ulong guildId)
    {
        await using var db = dbService.GetDbContext();
        var userRoleState =
            await db.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);
        if (userRoleState is null) return false;
        db.UserRoleStates.Remove(userRoleState);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ApplyUserRoleStateToAnotherUser(ulong sourceUserId, IGuildUser targetUser, ulong guildId)
    {
        await using var db = dbService.GetDbContext();

        var sourceUserRoleState =
            await db.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == sourceUserId);

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

    public async Task SetRoleStateManually(IUser user, ulong guildId, IEnumerable<ulong> roles)
    {
        await using var db = dbService.GetDbContext();
        var userRoleState =
            await db.UserRoleStates.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == user.Id);
        if (userRoleState is null)
        {
            userRoleState = new UserRoleStates
            {
                GuildId = guildId, UserId = user.Id, UserName = user.ToString(), SavedRoles = string.Join(",", roles)
            };
            await db.UserRoleStates.AddAsync(userRoleState);
            await db.SaveChangesAsync();
        }
        else
        {
            userRoleState.SavedRoles = string.Join(",", roles);
            db.UserRoleStates.Update(userRoleState);
            await db.SaveChangesAsync();
        }
    }
}