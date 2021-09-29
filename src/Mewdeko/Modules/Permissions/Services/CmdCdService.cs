﻿using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common.Collections;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;

namespace Mewdeko.Modules.Permissions.Services
{
    public class CmdCdService : ILateBlocker, INService
    {
        public ConcurrentDictionary<ulong, ConcurrentHashSet<CommandCooldown>> CommandCooldowns { get; }
        public ConcurrentDictionary<ulong, ConcurrentHashSet<ActiveCooldown>> ActiveCooldowns { get; } = new ConcurrentDictionary<ulong, ConcurrentHashSet<ActiveCooldown>>();

        public int Priority { get; } = 0;
            
        public CmdCdService(Mewdeko bot)
        {
            CommandCooldowns = new ConcurrentDictionary<ulong, ConcurrentHashSet<CommandCooldown>>(
                bot.AllGuildConfigs.ToDictionary(k => k.GuildId, 
                                 v => new ConcurrentHashSet<CommandCooldown>(v.CommandCooldowns)));
        }

        public Task<bool> TryBlock(IGuild guild, IUser user, string commandName)
        {
            if (guild is null)
                return Task.FromResult(false);
            
            var cmdcds = CommandCooldowns.GetOrAdd(guild.Id, new ConcurrentHashSet<CommandCooldown>());
            CommandCooldown cdRule;
            if ((cdRule = cmdcds.FirstOrDefault(cc => cc.CommandName == commandName)) != null)
            {
                var activeCdsForGuild = ActiveCooldowns.GetOrAdd(guild.Id, new ConcurrentHashSet<ActiveCooldown>());
                if (activeCdsForGuild.FirstOrDefault(ac => ac.UserId == user.Id && ac.Command == commandName) != null)
                {
                    return Task.FromResult(true);
                }
                
                activeCdsForGuild.Add(new ActiveCooldown()
                {
                    UserId = user.Id,
                    Command = commandName,
                });
                
                var _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(cdRule.Seconds * 1000).ConfigureAwait(false);
                        activeCdsForGuild.RemoveWhere(ac => ac.Command == commandName && ac.UserId == user.Id);
                    }
                    catch
                    {
                        // ignored
                    }
                });
            }

            return Task.FromResult(false);
        }
        
        public Task<bool> TryBlockLate(DiscordSocketClient client, ICommandContext ctx, string moduleName, CommandInfo command)
        {
            var guild = ctx.Guild;
            var user = ctx.User;
            var commandName = command.Name.ToLowerInvariant();

            return TryBlock(guild, user, commandName);
        }
    }

    public class ActiveCooldown
    {
        public string Command { get; set; }
        public ulong UserId { get; set; }
    }
}
