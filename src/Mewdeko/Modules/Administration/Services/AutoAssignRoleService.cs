using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Threading.Channels;
using Discord.Net;
using Discord.WebSocket;
using LinqToDB;
using Mewdeko._Extensions;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

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

    private readonly ConcurrentDictionary<ulong, IReadOnlyList<ulong>> _autoAssignableBotRoles;

    //guildid/roleid
    private readonly ConcurrentDictionary<ulong, IReadOnlyList<ulong>> _autoAssignableRoles;
    private readonly ConcurrentDictionary<ulong, string> _brcheck;
    private readonly DbService _db;

    public AutoAssignRoleService(DiscordSocketClient client, Mewdeko bot, DbService db)
    {
        var client1 = client;
        _db = db;

        _autoAssignableRoles = bot.AllGuildConfigs
            .Where(x => !string.IsNullOrWhiteSpace(x.AutoAssignRoleId))
            .ToDictionary<GuildConfig, ulong, IReadOnlyList<ulong>>(k => k.GuildId, v => v.GetAutoAssignableRoles())
            .ToConcurrent();
        _autoAssignableBotRoles = bot.AllGuildConfigs
            .Where(x => !string.IsNullOrWhiteSpace(x.AutoBotRoleIds))
            .ToDictionary<GuildConfig, ulong, IReadOnlyList<ulong>>(k => k.GuildId,
                v => v.GetAutoAssignableBotRoles())
            .ToConcurrent();
        _brcheck = bot.AllGuildConfigs
            .ToDictionary(x => x.GuildId, x => x.AutoBotRoleIds)
            .ToConcurrent();
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var user = await _assignQueue.Reader.ReadAsync();
                if (user.IsBot && _autoAssignableBotRoles.TryGetValue(user.Guild.Id, out var savedRoleIds))
                    try
                    {
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

                if (!_autoAssignableRoles.TryGetValue(user.Guild.Id, out var savedRoleIds1)) continue;
                {
                    try
                    {
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

        client1.UserJoined += OnClientOnUserJoined;
        client1.RoleDeleted += OnClientRoleDeleted;
    }

    public string GetStaffRole(ulong? id)
    {
        Debug.Assert(id != null, nameof(id) + " != null");
        _brcheck.TryGetValue(id.Value, out var snum);
        return snum;
    }

    private async Task OnClientRoleDeleted(SocketRole role)
    {
        if (_autoAssignableRoles.TryGetValue(role.Guild.Id, out var roles)
            && roles.Contains(role.Id))
            await ToggleAarAsync(role.Guild.Id, role.Id);
        if (_autoAssignableBotRoles.TryGetValue(role.Guild.Id, out var broles)
            && broles.Contains(role.Id))
            await ToggleAabrAsync(role.Guild.Id, role.Id);
    }

    private async Task OnClientOnUserJoined(SocketGuildUser user)
    {
        if (user.IsBot && _autoAssignableBotRoles.TryGetValue(user.Guild.Id, out _))
            await _assignQueue.Writer.WriteAsync(user);
        if (_autoAssignableRoles.TryGetValue(user.Guild.Id, out _))
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

        if (roles.Count > 0)
            _autoAssignableRoles[guildId] = roles;
        else
            _autoAssignableRoles.TryRemove(guildId, out _);

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

        _autoAssignableRoles.TryRemove(guildId, out _);

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
            _autoAssignableBotRoles[guildId] = roles;
        else
            _autoAssignableBotRoles.TryRemove(guildId, out _);

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

        _autoAssignableBotRoles.TryRemove(guildId, out _);

        await uow.SaveChangesAsync();
    }

    public async Task SetAarRolesAsync(ulong guildId, IEnumerable<ulong> newRoles)
    {
        await using var uow = _db.GetDbContext();

        var gc = uow.ForGuildId(guildId, set => set);
        gc.SetAutoAssignableRoles(newRoles);

        await uow.SaveChangesAsync();
    }

    public bool TryGetNormalRoles(ulong guildId, out IReadOnlyList<ulong> roles) => _autoAssignableRoles.TryGetValue(guildId, out roles);

    public bool TryGetBotRoles(ulong guildId, out IReadOnlyList<ulong> roles) => _autoAssignableBotRoles.TryGetValue(guildId, out roles);
}

public static class GuildConfigExtensions
{
    public static List<ulong> GetAutoAssignableRoles(this GuildConfig gc)
    {
        if (string.IsNullOrWhiteSpace(gc.AutoAssignRoleId))
            return new List<ulong>();

        return gc.AutoAssignRoleId.Split(' ').Select(ulong.Parse).ToList();
    }

    public static void SetAutoAssignableRoles(this GuildConfig gc, IEnumerable<ulong> roles) => gc.AutoAssignRoleId = roles.JoinWith(' ');

    public static List<ulong> GetAutoAssignableBotRoles(this GuildConfig gc)
    {
        if (string.IsNullOrWhiteSpace(gc.AutoBotRoleIds))
            return new List<ulong>();

        return gc.AutoBotRoleIds.Split(' ').Select(ulong.Parse).ToList();
    }

    public static void SetAutoAssignableBotRoles(this GuildConfig gc, IEnumerable<ulong> roles) => gc.AutoBotRoleIds = roles.JoinWith(' ');
}