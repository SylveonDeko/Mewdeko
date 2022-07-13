using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Server_Management;

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

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels), Priority(0)]
        public async Task PermControl(SocketGuildChannel channel, PermValue perm, IRole role,
            params ChannelPermission[] perms)
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

            foreach (var e in perms) list.Add(e.ToString());
            await channel.AddPermissionOverwriteAsync(role, result).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                $"I have allowed the following permissions for the role {role.Mention} in {channel}: \n**{string.Join("\n", list)}**").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels), Priority(1)]
        public async Task PermControl(SocketGuildChannel channel, PermValue perm, IUser role,
            params ChannelPermission[] perms)
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

            foreach (var e in perms) list.Add(e.ToString());
            await channel.AddPermissionOverwriteAsync(role, result).ConfigureAwait(false);
            if (perm == PermValue.Allow)
                await ctx.Channel.SendConfirmAsync($"I have allowed the following permissions for the user {role.Mention} in {channel}: \n**{string.Join("\n", list)}**").ConfigureAwait(false);
            else
                await ctx.Channel.SendConfirmAsync($"I have denied the following permissions for the user {role.Mention} in {channel}: \n**{string.Join("\n", list)}**").ConfigureAwait(false);
        }
    }
}