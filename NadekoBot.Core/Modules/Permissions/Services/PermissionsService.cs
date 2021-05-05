using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using NadekoBot.Common.ModuleBehaviors;
using NadekoBot.Extensions;
using NadekoBot.Modules.Permissions.Common;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;

namespace NadekoBot.Modules.Permissions.Services
{
    public class PermissionService : ILateBlocker, INService
    {
        public int Priority { get; } = 0;
        
        private readonly DbService _db;
        private readonly CommandHandler _cmd;
        private readonly IBotStrings _strings;

        //guildid, root permission
        public ConcurrentDictionary<ulong, PermissionCache> Cache { get; } =
            new ConcurrentDictionary<ulong, PermissionCache>();

        public PermissionService(DiscordSocketClient client, DbService db, CommandHandler cmd, IBotStrings strings)
        {
            _db = db;
            _cmd = cmd;
            _strings = strings;

            using (var uow = _db.GetDbContext())
            {
                foreach (var x in uow.GuildConfigs.Permissionsv2ForAll(client.Guilds.ToArray().Select(x => x.Id).ToList()))
                {
                    Cache.TryAdd(x.GuildId, new PermissionCache()
                    {
                        Verbose = x.VerbosePermissions,
                        PermRole = x.PermissionRole,
                        Permissions = new PermissionsCollection<Permissionv2>(x.Permissions)
                    });
                }
            }
        }

        public PermissionCache GetCacheFor(ulong guildId)
        {
            if (!Cache.TryGetValue(guildId, out var pc))
            {
                using (var uow = _db.GetDbContext())
                {
                    var config = uow.GuildConfigs.ForId(guildId,
                        set => set.Include(x => x.Permissions));
                    UpdateCache(config);
                }
                Cache.TryGetValue(guildId, out pc);
                if (pc == null)
                    throw new Exception("Cache is null.");
            }
            return pc;
        }

        public async Task AddPermissions(ulong guildId, params Permissionv2[] perms)
        {
            using (var uow = _db.GetDbContext())
            {
                var config = uow.GuildConfigs.GcWithPermissionsv2For(guildId);
                //var orderedPerms = new PermissionsCollection<Permissionv2>(config.Permissions);
                var max = config.Permissions.Max(x => x.Index); //have to set its index to be the highest
                foreach (var perm in perms)
                {
                    perm.Index = ++max;
                    config.Permissions.Add(perm);
                }
                await uow.SaveChangesAsync();
                UpdateCache(config);
            }
        }

        public void UpdateCache(GuildConfig config)
        {
            Cache.AddOrUpdate(config.GuildId, new PermissionCache()
            {
                Permissions = new PermissionsCollection<Permissionv2>(config.Permissions),
                PermRole = config.PermissionRole,
                Verbose = config.VerbosePermissions
            }, (id, old) =>
            {
                old.Permissions = new PermissionsCollection<Permissionv2>(config.Permissions);
                old.PermRole = config.PermissionRole;
                old.Verbose = config.VerbosePermissions;
                return old;
            });
        }

        public async Task<bool> TryBlockLate(DiscordSocketClient client, ICommandContext ctx, string moduleName,
            CommandInfo command)
        {
            var guild = ctx.Guild;
            var msg = ctx.Message;
            var user = ctx.User;
            var channel = ctx.Channel;
            var commandName = command.Name.ToLowerInvariant();
            
            await Task.Yield();
            if (guild == null)
            {
                return false;
            }
            else
            {
                var resetCommand = commandName == "resetperms";

                PermissionCache pc = GetCacheFor(guild.Id);
                if (!resetCommand && !pc.Permissions.CheckPermissions(msg, commandName, moduleName, out int index))
                {
                    if (pc.Verbose)
                    {
                        try
                        {
                            await channel.SendErrorAsync(_strings.GetText("perm_prevent", guild.Id, index + 1,
                                    Format.Bold(pc.Permissions[index].GetCommand(_cmd.GetPrefix(guild), (SocketGuild) guild))))
                                .ConfigureAwait(false);
                        }
                        catch
                        {
                        }
                    }

                    return true;
                }


                if (moduleName == nameof(Permissions))
                {
                    if (!(user is IGuildUser guildUser))
                        return true;

                    if (guildUser.GuildPermissions.Administrator)
                        return false;

                    var permRole = pc.PermRole;
                    if (!ulong.TryParse(permRole, out var rid))
                        rid = 0;
                    string returnMsg;
                    IRole role;
                    if (string.IsNullOrWhiteSpace(permRole) || (role = guild.GetRole(rid)) == null)
                    {
                        returnMsg = $"You need Admin permissions in order to use permission commands.";
                        if (pc.Verbose)
                            try { await channel.SendErrorAsync(returnMsg).ConfigureAwait(false); } catch { }

                        return true;
                    }
                    else if (!guildUser.RoleIds.Contains(rid))
                    {
                        returnMsg = $"You need the {Format.Bold(role.Name)} role in order to use permission commands.";
                        if (pc.Verbose)
                            try { await channel.SendErrorAsync(returnMsg).ConfigureAwait(false); } catch { }

                        return true;
                    }
                    return false;
                }
            }

            return false;
        }

        public async Task Reset(ulong guildId)
        {
            using (var uow = _db.GetDbContext())
            {
                var config = uow.GuildConfigs.GcWithPermissionsv2For(guildId);
                config.Permissions = Permissionv2.GetDefaultPermlist;
                await uow.SaveChangesAsync();
                UpdateCache(config);
            }
        }
    }
}