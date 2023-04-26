using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;

namespace Mewdeko.Modules.Server_Management;

public partial class ServerManagement
{
    public class PermControls : MewdekoSubmodule
    {
        private static ulong GetRawPermissionValue(IEnumerable<ChannelPermission> permissions)
            => permissions.Aggregate<ChannelPermission, ulong>(0, (current, permission) => current | (ulong)permission);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels), Priority(1)]
        public async Task PermControl(SocketGuildChannel channel, PermValue perm, IRole role,
            params ChannelPermission[] perms)
        {
            OverwritePermissions result;
            var newPermsRaw = GetRawPermissionValue(perms);
            var currentPerms = channel.GetPermissionOverwrite(role);

            if (currentPerms == null)
            {
                if (perm == PermValue.Inherit) return;
                result = perm == PermValue.Allow ? new OverwritePermissions(newPermsRaw, 0) : new OverwritePermissions(0, newPermsRaw);
            }
            else
            {
                var allowPermsRaw = GetRawPermissionValue(currentPerms.Value.ToAllowList());
                var denyPermsRaw = GetRawPermissionValue(currentPerms.Value.ToDenyList());

                switch (perm)
                {
                    case PermValue.Allow:
                        allowPermsRaw |= newPermsRaw;
                        denyPermsRaw &= ~newPermsRaw;
                        break;
                    case PermValue.Deny:
                        denyPermsRaw |= newPermsRaw;
                        allowPermsRaw &= ~newPermsRaw;
                        break;
                    default:
                        allowPermsRaw &= ~newPermsRaw;
                        denyPermsRaw &= ~newPermsRaw;
                        break;
                }

                result = new OverwritePermissions(allowPermsRaw, denyPermsRaw);
            }

            var list = perms.Select(e => e.ToString()).ToList();
            await channel.AddPermissionOverwriteAsync(role, result).ConfigureAwait(false);

            switch (perm)
            {
                case PermValue.Inherit:
                    await ctx.Channel.SendConfirmAsync($"I have set the following permissions for the user {role.Mention} in {channel} to inherit: \n**{string.Join("\n", list)}**")
                        .ConfigureAwait(false);
                    break;
                case PermValue.Allow:
                    await ctx.Channel.SendConfirmAsync($"I have allowed the following permissions for the user {role.Mention} in {channel}: \n**{string.Join("\n", list)}**")
                        .ConfigureAwait(false);
                    break;
                default:
                    await ctx.Channel.SendConfirmAsync($"I have denied the following permissions for the user {role.Mention} in {channel}: \n**{string.Join("\n", list)}**")
                        .ConfigureAwait(false);
                    break;
            }
        }


        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels), Priority(1)]
        public async Task PermControl(SocketGuildChannel channel, PermValue perm, IUser user,
            params ChannelPermission[] perms)
        {
            OverwritePermissions result;
            var newPermsRaw = GetRawPermissionValue(perms);
            var currentPerms = channel.GetPermissionOverwrite(user);

            if (currentPerms == null)
            {
                if (perm == PermValue.Inherit) return;
                result = perm == PermValue.Allow ? new OverwritePermissions(newPermsRaw, 0) : new OverwritePermissions(0, newPermsRaw);
            }
            else
            {
                var allowPermsRaw = GetRawPermissionValue(currentPerms.Value.ToAllowList());
                var denyPermsRaw = GetRawPermissionValue(currentPerms.Value.ToDenyList());

                switch (perm)
                {
                    case PermValue.Allow:
                        allowPermsRaw |= newPermsRaw;
                        denyPermsRaw &= ~newPermsRaw;
                        break;
                    case PermValue.Deny:
                        denyPermsRaw |= newPermsRaw;
                        allowPermsRaw &= ~newPermsRaw;
                        break;
                    default:
                        allowPermsRaw &= ~newPermsRaw;
                        denyPermsRaw &= ~newPermsRaw;
                        break;
                }

                result = new OverwritePermissions(allowPermsRaw, denyPermsRaw);
            }

            var list = perms.Select(e => e.ToString()).ToList();
            await channel.AddPermissionOverwriteAsync(user, result).ConfigureAwait(false);

            switch (perm)
            {
                case PermValue.Inherit:
                    await ctx.Channel.SendConfirmAsync($"I have set the following permissions for the user {user.Mention} in {channel} to inherit: \n**{string.Join("\n", list)}**")
                        .ConfigureAwait(false);
                    break;
                case PermValue.Allow:
                    await ctx.Channel.SendConfirmAsync($"I have allowed the following permissions for the user {user.Mention} in {channel}: \n**{string.Join("\n", list)}**")
                        .ConfigureAwait(false);
                    break;
                default:
                    await ctx.Channel.SendConfirmAsync($"I have denied the following permissions for the user {user.Mention} in {channel}: \n**{string.Join("\n", list)}**")
                        .ConfigureAwait(false);
                    break;
            }
        }
    }
}