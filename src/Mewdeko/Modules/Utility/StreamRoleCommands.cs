using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    public class StreamRoleCommands : MewdekoSubmodule<StreamRoleService>
    {
        [Cmd, Aliases, BotPerm(GuildPermission.ManageRoles),
         UserPerm(GuildPermission.ManageRoles), RequireContext(ContextType.Guild)]
        public async Task StreamRole(IRole fromRole, IRole addRole)
        {
            await Service.SetStreamRole(fromRole, addRole).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("stream_role_enabled", Format.Bold(fromRole.ToString()),
                Format.Bold(addRole.ToString())).ConfigureAwait(false);
        }

        [Cmd, Aliases, BotPerm(GuildPermission.ManageRoles),
         UserPerm(GuildPermission.ManageRoles), RequireContext(ContextType.Guild)]
        public async Task StreamRole()
        {
            await Service.StopStreamRole(ctx.Guild).ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("stream_role_disabled").ConfigureAwait(false);
        }

        [Cmd, Aliases, BotPerm(GuildPermission.ManageRoles),
         UserPerm(GuildPermission.ManageRoles), RequireContext(ContextType.Guild)]
        public async Task StreamRoleKeyword([Remainder] string? keyword = null)
        {
            var kw = await Service.SetKeyword(ctx.Guild, keyword).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(keyword))
                await ReplyConfirmLocalizedAsync("stream_role_kw_reset").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("stream_role_kw_set", Format.Bold(kw)).ConfigureAwait(false);
        }

        [Cmd, Aliases, BotPerm(GuildPermission.ManageRoles),
         UserPerm(GuildPermission.ManageRoles), RequireContext(ContextType.Guild)]
        public async Task StreamRoleBlacklist(AddRemove action, [Remainder] IGuildUser user)
        {
            var success = await Service
                .ApplyListAction(StreamRoleListType.Blacklist, ctx.Guild, action, user.Id, user.ToString())
                .ConfigureAwait(false);

            if (action == AddRemove.Add)
            {
                if (success)
                {
                    await ReplyConfirmLocalizedAsync("stream_role_bl_add", Format.Bold(user.ToString()))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyConfirmLocalizedAsync("stream_role_bl_add_fail", Format.Bold(user.ToString()))
                        .ConfigureAwait(false);
                }
            }
            else if (success)
            {
                await ReplyConfirmLocalizedAsync("stream_role_bl_rem", Format.Bold(user.ToString()))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorLocalizedAsync("stream_role_bl_rem_fail", Format.Bold(user.ToString()))
                    .ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, BotPerm(GuildPermission.ManageRoles),
         UserPerm(GuildPermission.ManageRoles), RequireContext(ContextType.Guild)]
        public async Task StreamRoleWhitelist(AddRemove action, [Remainder] IGuildUser user)
        {
            var success = await Service
                .ApplyListAction(StreamRoleListType.Whitelist, ctx.Guild, action, user.Id, user.ToString())
                .ConfigureAwait(false);

            if (action == AddRemove.Add)
            {
                if (success)
                {
                    await ReplyConfirmLocalizedAsync("stream_role_wl_add", Format.Bold(user.ToString()))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyConfirmLocalizedAsync("stream_role_wl_add_fail", Format.Bold(user.ToString()))
                        .ConfigureAwait(false);
                }
            }
            else if (success)
            {
                await ReplyConfirmLocalizedAsync("stream_role_wl_rem", Format.Bold(user.ToString()))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorLocalizedAsync("stream_role_wl_rem_fail", Format.Bold(user.ToString()))
                    .ConfigureAwait(false);
            }
        }
    }
}