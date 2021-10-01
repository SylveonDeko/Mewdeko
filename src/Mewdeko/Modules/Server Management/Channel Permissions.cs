using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using PermValue = Discord.PermValue;

namespace Mewdeko.Modules.Server_Management
{
    public partial class ServerManagement
    {
        public class PermControls : MewdekoSubmodule
        {
            private static ulong GetRawPermissionValue(IEnumerable<ChannelPermission> permissions)
            {
                ulong result = 0;
                foreach (var permission in permissions)
                    result |= (ulong)permission;
                return result;
            }
            private static ulong GetRawPermissionValue(IEnumerable<GuildPermission> permissions)
            {
                ulong result = 0;
                foreach (var permission in permissions)
                    result |= (ulong)permission;
                return result;
            }
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(0)]
            public async Task PermControl(SocketGuildChannel channel, PermValue perm, IRole role, params ChannelPermission[] perms)
            {
                var list = new List<string>();
                OverwritePermissions result;
                var newPermsRaw = GetRawPermissionValue(perms);
                var currentPerms = channel.GetPermissionOverwrite(role);
                if (currentPerms == null)
                {
                    if (perm == PermValue.Allow)
                        result = new OverwritePermissions(newPermsRaw, 0);
                    else
                        result = new OverwritePermissions(0, newPermsRaw);
                }
                else
                {
                    var allowPermsRaw = GetRawPermissionValue(currentPerms.Value.ToAllowList());
                    var denyPermsRaw = GetRawPermissionValue(currentPerms.Value.ToDenyList());
                    if (perm == PermValue.Allow)
                        allowPermsRaw |= newPermsRaw;
                    else
                        denyPermsRaw |= newPermsRaw;
                    result = new OverwritePermissions(allowPermsRaw, denyPermsRaw);
                }

                foreach (var e in perms)
                {
                   list.Add(e.ToString()); 
                }
                await channel.AddPermissionOverwriteAsync(role, result);
                await ctx.Channel.SendConfirmAsync(
                    $"I have allowed the following permissions for the role {role.Mention} in {channel}: \n**{string.Join("\n", list)}**");
            }
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [Priority(1)]
            public async Task PermControl(SocketGuildChannel channel, PermValue perm, IUser role, params ChannelPermission[] perms)
            {
                var list = new List<string>();
                OverwritePermissions result;
                var newPermsRaw = GetRawPermissionValue(perms);
                var currentPerms = channel.GetPermissionOverwrite(role);
                if (currentPerms == null)
                {
                    if (perm == PermValue.Allow)
                        result = new OverwritePermissions(newPermsRaw, 0);
                    else
                        result = new OverwritePermissions(0, newPermsRaw);
                }
                else
                {
                    var allowPermsRaw = GetRawPermissionValue(currentPerms.Value.ToAllowList());
                    var denyPermsRaw = GetRawPermissionValue(currentPerms.Value.ToDenyList());
                    if (perm == PermValue.Allow)
                        allowPermsRaw |= newPermsRaw;
                    else
                        denyPermsRaw |= newPermsRaw;
                    result = new OverwritePermissions(allowPermsRaw, denyPermsRaw);
                }

                foreach (var e in perms)
                {
                    list.Add(e.ToString());
                }
                await channel.AddPermissionOverwriteAsync(role, result);
                await ctx.Channel.SendConfirmAsync(
                    $"I have allowed the following permissions for the user {role.Mention} in {channel}: \n**{string.Join("\n", list)}**");
            }
            //[MewdekoCommand]
            //[Usage]
            //[Description]
            //[Aliases]
            //[RequireContext(ContextType.Guild)]
            //[UserPerm(GuildPerm.ManageChannels)]
            //[Priority(1)]
            //public async Task PermControl(IRole channel, PermValue perm,  params GuildPermission[] perms)
            //{
            //    var list = new List<string>();
            //    GuildPermissions result;
            //    var newPermsRaw = GetRawPermissionValue(perms);
            //    var currentPerms = channel.Permissions;
            //    var perms2 = GetRawPermissionValue(currentPerms.ToList());
            //    currentPerms |= newPermsRaw;
            //    result = new GuildPermissions(perms2 |= newPermsRaw);
            //    foreach (var e in perms)
            //    {
            //        list.Add(e.ToString());
            //    }
            //    await channel.ModifyAsync(x => x.Permissions = result);
            //    await ctx.Channel.SendConfirmAsync(
            //        $"I have allowed the following permissions for the user {channel.Mention}: \n**{string.Join("\n", list)}**");
            //}
        }
    }
}