using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;

namespace Mewdeko.Modules.Server_Management;

public partial class ServerManagement
{
    /// <summary>
    /// Contains commands for managing channel permission overrides for roles and users.
    /// </summary>
    public class PermControls : MewdekoSubmodule
    {
        /// <summary>
        /// Computes the raw value of a set of channel permissions by aggregating their bitwise representations.
        /// </summary>
        /// <param name="permissions">A collection of channel permissions.</param>
        /// <returns>The aggregated raw permission value.</returns>
        private static ulong GetRawPermissionValue(IEnumerable<ChannelPermission> permissions)
            => permissions.Aggregate<ChannelPermission, ulong>(0, (current, permission) => current | (ulong)permission);

        /// <summary>
        /// Modifies channel permissions for a specified role according to the provided permissions and action.
        /// </summary>
        /// <param name="channel">The channel for which permissions are being modified.</param>
        /// <param name="perm">The action to perform (Allow, Deny, Inherit).</param>
        /// <param name="role">The role to which the permissions modifications apply.</param>
        /// <param name="perms">A set of permissions to be modified.</param>
        /// <remarks>
        /// Allows, denies, or resets (inherit) specific channel permissions for a role.
        /// Notifies the channel of the modifications made.
        /// </remarks>
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
                result = perm == PermValue.Allow
                    ? new OverwritePermissions(newPermsRaw, 0)
                    : new OverwritePermissions(0, newPermsRaw);
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
                    await ctx.Channel
                        .SendConfirmAsync(
                            $"I have set the following permissions for the user {role.Mention} in {channel} to inherit: \n**{string.Join("\n", list)}**")
                        .ConfigureAwait(false);
                    break;
                case PermValue.Allow:
                    await ctx.Channel
                        .SendConfirmAsync(
                            $"I have allowed the following permissions for the user {role.Mention} in {channel}: \n**{string.Join("\n", list)}**")
                        .ConfigureAwait(false);
                    break;
                default:
                    await ctx.Channel
                        .SendConfirmAsync(
                            $"I have denied the following permissions for the user {role.Mention} in {channel}: \n**{string.Join("\n", list)}**")
                        .ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        /// Modifies channel permissions for a specified user according to the provided permissions and action.
        /// </summary>
        /// <param name="channel">The channel for which permissions are being modified.</param>
        /// <param name="perm">The action to perform (Allow, Deny, Inherit).</param>
        /// <param name="user">The user to which the permissions modifications apply.</param>
        /// <param name="perms">A set of permissions to be modified.</param>
        /// <remarks>
        /// Allows, denies, or resets (inherit) specific channel permissions for a user.
        /// Notifies the channel of the modifications made.
        /// </remarks>
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
                result = perm == PermValue.Allow
                    ? new OverwritePermissions(newPermsRaw, 0)
                    : new OverwritePermissions(0, newPermsRaw);
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
                    await ctx.Channel
                        .SendConfirmAsync(
                            $"I have set the following permissions for the user {user.Mention} in {channel} to inherit: \n**{string.Join("\n", list)}**")
                        .ConfigureAwait(false);
                    break;
                case PermValue.Allow:
                    await ctx.Channel
                        .SendConfirmAsync(
                            $"I have allowed the following permissions for the user {user.Mention} in {channel}: \n**{string.Join("\n", list)}**")
                        .ConfigureAwait(false);
                    break;
                default:
                    await ctx.Channel
                        .SendConfirmAsync(
                            $"I have denied the following permissions for the user {user.Mention} in {channel}: \n**{string.Join("\n", list)}**")
                        .ConfigureAwait(false);
                    break;
            }
        }
    }
}