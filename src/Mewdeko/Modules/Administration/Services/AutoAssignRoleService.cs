using Discord.Net;
using Discord.WebSocket;
using LinqToDB;
using Mewdeko._Extensions;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Net;
using System.Threading.Channels;

namespace Mewdeko.Modules.Administration.Services;

public sealed class AutoAssignRoleService : INService
{
    private readonly Channel<SocketGuildUser> _assignQueue = Channel.CreateBounded<SocketGuildUser>(
        new BoundedChannelOptions(int.MaxValue)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly Mewdeko _bot;

    //guildid/roleid
    private readonly DbService _db;

    public AutoAssignRoleService(DiscordSocketClient client, Mewdeko bot, DbService db)
    {
        _bot = bot;
        _db = db;
        _ = Task.Run(async () =>
        {
            while (true)
            {
                
                var user = await _assignQueue.Reader.ReadAsync();
                var autoroles = _bot.AllGuildConfigs[user.Guild.Id].AutoAssignRoleId;
                var autobotroles = _bot.AllGuildConfigs[user.Guild.Id].AutoBotRoleIds;
                if (user.IsBot && autoroles != "0")
                    try
                    {
                        var savedRoleIds = _bot.AllGuildConfigs[user.Guild.Id].AutoBotRoleIds.Split(" ").Select(ulong.Parse);
                        var roleIds = savedRoleIds
                            .Select(roleId => user.Guild.GetRole(roleId))
                            .Where(x => x is not null)
                            .ToList();

                        if (roleIds.Any())
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

                        await DisableAabrAsync(user.Guild.Id);
                        continue;
                    }
                    catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
                    {
                        Log.Warning(
                            "Disabled 'Auto assign bot role' feature on {GuildName} [{GuildId}] server because I don't have role management permissions",
                            user.Guild.Name,
                            user.Guild.Id);

                        await DisableAabrAsync(user.Guild.Id);
                        continue;
                    }
                    catch
                    {
                        Log.Warning("Error in aar. Probably one of the roles doesn't exist");
                        continue;
                    }

                if (autobotroles != null) continue;
                {
                    try
                    {
                        var savedRoleIds1 = autobotroles.Split(" ").Select(ulong.Parse);
                        var roleIds = savedRoleIds1
                            .Select(roleId => user.Guild.GetRole(roleId))
                            .Where(x => x is not null)
                            .ToList();

                        if (roleIds.Any())
                        {
                            await user.AddRolesAsync(roleIds).ConfigureAwait(false);
                            await Task.Delay(250).ConfigureAwait(false);
                            continue;
                        }

                        Log.Warning(
                            "Disabled 'Auto assign  role' feature on {GuildName} [{GuildId}] server the roles dont exist",
                            user.Guild.Name,
                            user.Guild.Id);

                        await DisableAarAsync(user.Guild.Id);
                    }
                    catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden)
                    {
                        Log.Warning(
                            "Disabled 'Auto assign bot role' feature on {GuildName} [{GuildId}] server because I don't have role management permissions",
                            user.Guild.Name,
                            user.Guild.Id);

                        await DisableAarAsync(user.Guild.Id);
                    }
                    catch
                    {
                        Log.Warning("Error in aar. Probably one of the roles doesn't exist");
                    }
                }
            }
        });

        client.UserJoined += OnClientOnUserJoined;
        client.RoleDeleted += OnClientRoleDeleted;
    }

    private async Task OnClientRoleDeleted(SocketRole role)
    {
        var broles = _bot.AllGuildConfigs[role.Guild.Id].AutoBotRoleIds;
        var roles = _bot.AllGuildConfigs[role.Guild.Id].AutoAssignRoleId;
        if (roles is not null
            && roles.Split(" ").Select(ulong.Parse).Contains(role.Id))
            await ToggleAarAsync(role.Guild.Id, role.Id);
        if (broles is not null
            && broles.Split(" ").Select(ulong.Parse).Contains(role.Id))
            await ToggleAabrAsync(role.Guild.Id, role.Id);
    }

    private async Task OnClientOnUserJoined(SocketGuildUser user)
    {
        if (user.IsBot && _bot.AllGuildConfigs[user.Guild.Id].AutoBotRoleIds != null)
            await _assignQueue.Writer.WriteAsync(user);
        if (_bot.AllGuildConfigs[user.Guild.Id].AutoAssignRoleId != null)
            await _assignQueue.Writer.WriteAsync(user);
    }

    public async Task<IReadOnlyList<ulong>> ToggleAarAsync(ulong guildId, ulong roleId)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guildId, set => set);
        var roles = gc.GetAutoAssignableRoles();
        if (!roles.Remove(roleId) && roles.Count < 10)
            roles.Add(roleId);

        gc.SetAutoAssignableRoles(roles);
        await uow.SaveChangesAsync();

        _bot.AllGuildConfigs[guildId].AutoAssignRoleId = roles.Count > 0 ? string.Join(" ", roles) : null;

        return roles;
    }

    private async Task DisableAarAsync(ulong guildId)
    {
        await using var uow = _db.GetDbContext();

        await 
            uow
            .GuildConfigs
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .UpdateAsync(_ => new GuildConfig {AutoAssignRoleId = null});

        _bot.AllGuildConfigs[guildId].AutoAssignRoleId = null;

        await uow.SaveChangesAsync();
    }

    public async Task SetAabrRolesAsync(ulong guildId, IEnumerable<ulong> newRoles)
    {
        await using var uow = _db.GetDbContext();

        var gc = uow.ForGuildId(guildId, set => set);
        gc.SetAutoAssignableBotRoles(newRoles);

        await uow.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ulong>> ToggleAabrAsync(ulong guildId, ulong roleId)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guildId, set => set);
        var roles = gc.GetAutoAssignableBotRoles();
        if (!roles.Remove(roleId) && roles.Count < 10)
            roles.Add(roleId);

        gc.SetAutoAssignableBotRoles(roles);
        await uow.SaveChangesAsync();

        if (roles.Count > 0)
            _bot.AllGuildConfigs[guildId].AutoBotRoleIds = string.Join(" ", roles);
        else
            _bot.AllGuildConfigs[guildId].AutoBotRoleIds = null;

        return roles;
    }

    public async Task DisableAabrAsync(ulong guildId)
    {
        await using var uow = _db.GetDbContext();

        await 
                uow
            .GuildConfigs
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .UpdateAsync(_ => new GuildConfig {AutoBotRoleIds = null});

        _bot.AllGuildConfigs[guildId].AutoBotRoleIds = null;

        await uow.SaveChangesAsync();
    }

    public async Task SetAarRolesAsync(ulong guildId, IEnumerable<ulong> newRoles)
    {
        await using var uow = _db.GetDbContext();

        var gc = uow.ForGuildId(guildId, set => set);
        gc.SetAutoAssignableRoles(newRoles);

        await uow.SaveChangesAsync();
    }

    public IReadOnlyList<ulong>? TryGetNormalRoles(ulong guildId, out IReadOnlyList<ulong> roles) =>
        roles = _bot.AllGuildConfigs[guildId].AutoAssignRoleId.Split("").Select(ulong.Parse) as IReadOnlyList<ulong>;

    public IReadOnlyList<ulong>? TryGetBotRoles(ulong guildId, out IReadOnlyList<ulong> roles) =>
        roles = _bot.AllGuildConfigs[guildId].AutoBotRoleIds.Split("").Select(ulong.Parse) as IReadOnlyList<ulong>;
}

public static class GuildConfigExtensions
{
    public static List<ulong> GetAutoAssignableRoles(this GuildConfig gc)
    {
        return string.IsNullOrWhiteSpace(gc.AutoAssignRoleId) ? new List<ulong>() : gc.AutoAssignRoleId.Split(' ').Select(ulong.Parse).ToList();
    }

    public static void SetAutoAssignableRoles(this GuildConfig gc, IEnumerable<ulong> roles) => gc.AutoAssignRoleId = roles.JoinWith(' ');

    public static List<ulong> GetAutoAssignableBotRoles(this GuildConfig gc)
    {
        return string.IsNullOrWhiteSpace(gc.AutoBotRoleIds) ? new List<ulong>() : gc.AutoBotRoleIds.Split(' ').Select(ulong.Parse).ToList();
    }

    public static void SetAutoAssignableBotRoles(this GuildConfig gc, IEnumerable<ulong> roles) => gc.AutoBotRoleIds = roles.JoinWith(' ');
}