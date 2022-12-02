using System.Net;
using System.Threading.Tasks;
using Discord.Net;
using Serilog;

namespace Mewdeko.Modules.Administration.Services;

public sealed class AutoAssignRoleService : INService
{
    private readonly Channel<IGuildUser> assignQueue = Channel.CreateBounded<IGuildUser>(
        new BoundedChannelOptions(int.MaxValue)
        {
            FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false
        });

    //guildid/roleid
    private readonly DbService db;
    private readonly GuildSettingsService guildSettings;

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
        eventHandler.RoleDeleted += OnClientRoleDeleted;
    }

    private async Task OnClientRoleDeleted(SocketRole role)
    {
        var broles = (await guildSettings.GetGuildConfig(role.Guild.Id)).AutoBotRoleIds;
        var roles = (await guildSettings.GetGuildConfig(role.Guild.Id)).AutoAssignRoleId;
        if (!string.IsNullOrWhiteSpace(roles)
            && roles.Split(" ").Select(ulong.Parse).Contains(role.Id))
        {
            await ToggleAarAsync(role.Guild.Id, role.Id).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(broles)
            && broles.Split(" ").Select(ulong.Parse).Contains(role.Id))
        {
            await ToggleAabrAsync(role.Guild.Id, role.Id).ConfigureAwait(false);
        }
    }

    private async Task OnClientOnUserJoined(IGuildUser user)
    {
        var broles = await TryGetBotRoles(user.Guild.Id);
        var roles = await TryGetNormalRoles(user.Guild.Id);
        if (user.IsBot && broles.Any())
            await assignQueue.Writer.WriteAsync(user).ConfigureAwait(false);
        if (roles.Any())
            await assignQueue.Writer.WriteAsync(user).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ulong>> ToggleAarAsync(ulong guildId, ulong roleId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);
        var roles = gc.GetAutoAssignableRoles();
        if (!roles.Remove(roleId) && roles.Count < 10)
            roles.Add(roleId);

        gc.SetAutoAssignableRoles(roles);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        guildSettings.UpdateGuildConfig(guildId, gc);

        return roles;
    }

    private async Task DisableAarAsync(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);
        gc.AutoAssignRoleId = "";
        guildSettings.UpdateGuildConfig(guildId, gc);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task SetAabrRolesAsync(ulong guildId, IEnumerable<ulong> newRoles)
    {
        await using var uow = db.GetDbContext();

        var gc = await uow.ForGuildId(guildId, set => set);
        gc.SetAutoAssignableBotRoles(newRoles);
        guildSettings.UpdateGuildConfig(guildId, gc);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ulong>> ToggleAabrAsync(ulong guildId, ulong roleId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);
        var roles = gc.GetAutoAssignableBotRoles();
        if (!roles.Remove(roleId) && roles.Count < 10)
            roles.Add(roleId);

        gc.SetAutoAssignableBotRoles(roles);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        guildSettings.UpdateGuildConfig(guildId, gc);

        return roles;
    }

    public async Task DisableAabrAsync(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);
        gc.AutoBotRoleIds = " ";
        guildSettings.UpdateGuildConfig(guildId, gc);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task SetAarRolesAsync(ulong guildId, IEnumerable<ulong> newRoles)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set);
        gc.SetAutoAssignableRoles(newRoles);
        guildSettings.UpdateGuildConfig(guildId, gc);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<IEnumerable<ulong>> TryGetNormalRoles(ulong guildId)
    {
        var tocheck = (await guildSettings.GetGuildConfig(guildId)).AutoAssignRoleId;
        return string.IsNullOrWhiteSpace(tocheck) ? new List<ulong>() : tocheck.Split(" ").Select(ulong.Parse).ToList();
    }

    public async Task<IEnumerable<ulong>> TryGetBotRoles(ulong guildId)
    {
        var tocheck = (await guildSettings.GetGuildConfig(guildId)).AutoBotRoleIds;
        return string.IsNullOrWhiteSpace(tocheck) ? new List<ulong>() : tocheck.Split(" ").Select(ulong.Parse).ToList();
    }
}

public static class GuildConfigExtensions
{
    public static List<ulong> GetAutoAssignableRoles(this GuildConfig gc)
        => string.IsNullOrWhiteSpace(gc.AutoAssignRoleId) ? new List<ulong>() : gc.AutoAssignRoleId.Split(" ").Select(ulong.Parse).ToList();

    public static void SetAutoAssignableRoles(this GuildConfig gc, IEnumerable<ulong> roles) => gc.AutoAssignRoleId = roles.JoinWith(" ");

    public static List<ulong> GetAutoAssignableBotRoles(this GuildConfig gc)
        => string.IsNullOrWhiteSpace(gc.AutoBotRoleIds) ? new List<ulong>() : gc.AutoBotRoleIds.Split(" ").Select(ulong.Parse).ToList();

    public static void SetAutoAssignableBotRoles(this GuildConfig gc, IEnumerable<ulong> roles) => gc.AutoBotRoleIds = roles.JoinWith(" ");
}