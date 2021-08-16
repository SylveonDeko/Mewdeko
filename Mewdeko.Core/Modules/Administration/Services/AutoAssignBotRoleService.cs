using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Mewdeko.Core.Services;
using System.Collections.Generic;
using System.Threading.Channels;
using LinqToDB;
using Microsoft.EntityFrameworkCore;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Serilog;

namespace Mewdeko.Modules.Administration.Services
{
    public sealed class AutoAssignBotRoleService : INService
    {
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;

        //guildid/roleid
        private readonly ConcurrentDictionary<ulong, IReadOnlyList<ulong>> _autoAssignableBotRoles;

        private Channel<SocketGuildUser> _assignQueue = Channel.CreateBounded<SocketGuildUser>(
            new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });

        public AutoAssignBotRoleService(DiscordSocketClient client, Mewdeko bot, DbService db)
        {
            _client = client;
            _db = db;

            _autoAssignableBotRoles = bot.AllGuildConfigs
                    .Where(x => !string.IsNullOrWhiteSpace(x.AutoBotRoleIds))
                    .ToDictionary<GuildConfig, ulong, IReadOnlyList<ulong>>(k => k.GuildId, v => v.GetAutoAssignableBotRoles())
                    .ToConcurrent();

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    var user = await _assignQueue.Reader.ReadAsync();
                    if (!_autoAssignableBotRoles.TryGetValue(user.Guild.Id, out var savedRoleIds))
                        continue;

                    try
                    {
                        var roleIds = savedRoleIds
                            .Select(roleId => user.Guild.GetRole(roleId))
                            .Where(x => !(x is null))
                            .ToList();

                        if (roleIds.Any())
                        {
                            if (!user.IsBot)
                                return;
                            await user.AddRolesAsync(roleIds).ConfigureAwait(false);
                            await Task.Delay(250).ConfigureAwait(false);
                        }
                        else
                        {
                            Log.Warning(
                                "Disabled 'Auto assign bot role' feature on {GuildName} [{GuildId}] server the roles dont exist",
                                user.Guild.Name,
                                user.Guild.Id);

                            await DisableAabrAsync(user.Guild.Id);
                        }
                    }
                    catch (Discord.Net.HttpException ex) when (ex.HttpCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        Log.Warning("Disabled 'Auto assign bot role' feature on {GuildName} [{GuildId}] server because I don't have role management permissions",
                            user.Guild.Name,
                            user.Guild.Id);

                        await DisableAabrAsync(user.Guild.Id);
                    }
                    catch
                    {
                        Log.Warning("Error in aabr. Probably one of the roles doesn't exist");
                    }
                }
            });

            _client.UserJoined += OnClientOnUserJoined;
            _client.RoleDeleted += OnClientRoleDeleted;
        }

        private async Task OnClientRoleDeleted(SocketRole role)
        {
            if (_autoAssignableBotRoles.TryGetValue(role.Guild.Id, out var roles)
                && roles.Contains(role.Id))
            {
                await ToggleAarAsync(role.Guild.Id, role.Id);
            }
        }

        private async Task OnClientOnUserJoined(SocketGuildUser user)
        {
            if (_autoAssignableBotRoles.TryGetValue(user.Guild.Id, out _))
                await _assignQueue.Writer.WriteAsync(user);
        }

        public async Task<IReadOnlyList<ulong>> ToggleAarAsync(ulong guildId, ulong roleId)
        {
            using var uow = _db.GetDbContext();
            var gc = uow.GuildConfigs.ForId(guildId, set => set);
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
            using var uow = _db.GetDbContext();

            await uow._context
                .GuildConfigs
                .AsNoTracking()
                .Where(x => x.GuildId == guildId)
                .UpdateAsync(_ => new GuildConfig() { AutoBotRoleIds = null });

            _autoAssignableBotRoles.TryRemove(guildId, out _);

            await uow.SaveChangesAsync();
        }

        public async Task SetAarRolesAsync(ulong guildId, IEnumerable<ulong> newRoles)
        {
            using var uow = _db.GetDbContext();

            var gc = uow.GuildConfigs.ForId(guildId, set => set);
            gc.SetAutoAssignableBotRoles(newRoles);

            await uow.SaveChangesAsync();
        }

        public bool TryGetRoles(ulong guildId, out IReadOnlyList<ulong> roles)
            => _autoAssignableBotRoles.TryGetValue(guildId, out roles);
    }

    public static class GuildConfigExtensions
    {
        public static List<ulong> GetAutoAssignableBotRoles(this GuildConfig gc)
        {
            if (string.IsNullOrWhiteSpace(gc.AutoBotRoleIds))
                return new List<ulong>();

            return gc.AutoBotRoleIds.Split(' ').Select(ulong.Parse).ToList();
        }

        public static void SetAutoAssignableBotRoles(this GuildConfig gc, IEnumerable<ulong> roles)
        {
            gc.AutoBotRoleIds = roles.JoinWith(' ');
        }
    }
}
