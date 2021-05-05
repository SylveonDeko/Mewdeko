using Discord;
using Discord.Commands;
using System.Threading.Tasks;
using NadekoBot.Common.Attributes;
using NadekoBot.Modules.Utility.Services;
using NadekoBot.Common.TypeReaders;
using NadekoBot.Modules.Utility.Common;

namespace NadekoBot.Modules.Utility
{
    public partial class Utility
    {
        public class StreamRoleCommands : NadekoSubmodule<StreamRoleService>
        {
            [NadekoCommand, Usage, Description, Aliases]
            [BotPerm(GuildPerm.ManageRoles)]
            [UserPerm(GuildPerm.ManageRoles)]
            [RequireContext(ContextType.Guild)]
            public async Task StreamRole(IRole fromRole, IRole addRole)
            {
                await this._service.SetStreamRole(fromRole, addRole).ConfigureAwait(false);

                await ReplyConfirmLocalizedAsync("stream_role_enabled", Format.Bold(fromRole.ToString()), Format.Bold(addRole.ToString())).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [BotPerm(GuildPerm.ManageRoles)]
            [UserPerm(GuildPerm.ManageRoles)]
            [RequireContext(ContextType.Guild)]
            public async Task StreamRole()
            {
                await this._service.StopStreamRole(ctx.Guild).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("stream_role_disabled").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [BotPerm(GuildPerm.ManageRoles)]
            [UserPerm(GuildPerm.ManageRoles)]
            [RequireContext(ContextType.Guild)]
            public async Task StreamRoleKeyword([Leftover]string keyword = null)
            {
                string kw = await this._service.SetKeyword(ctx.Guild, keyword).ConfigureAwait(false);
                
                if(string.IsNullOrWhiteSpace(keyword))
                    await ReplyConfirmLocalizedAsync("stream_role_kw_reset").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("stream_role_kw_set", Format.Bold(kw)).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [BotPerm(GuildPerm.ManageRoles)]
            [UserPerm(GuildPerm.ManageRoles)]
            [RequireContext(ContextType.Guild)]
            public async Task StreamRoleBlacklist(AddRemove action, [Leftover] IGuildUser user)
            {
                var success = await this._service.ApplyListAction(StreamRoleListType.Blacklist, ctx.Guild, action, user.Id, user.ToString())
                    .ConfigureAwait(false);

                if(action == AddRemove.Add)
                    if(success)
                        await ReplyConfirmLocalizedAsync("stream_role_bl_add", Format.Bold(user.ToString())).ConfigureAwait(false);
                    else
                        await ReplyConfirmLocalizedAsync("stream_role_bl_add_fail", Format.Bold(user.ToString())).ConfigureAwait(false);
                else
                    if (success)
                        await ReplyConfirmLocalizedAsync("stream_role_bl_rem", Format.Bold(user.ToString())).ConfigureAwait(false);
                    else
                        await ReplyErrorLocalizedAsync("stream_role_bl_rem_fail", Format.Bold(user.ToString())).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [BotPerm(GuildPerm.ManageRoles)]
            [UserPerm(GuildPerm.ManageRoles)]
            [RequireContext(ContextType.Guild)]
            public async Task StreamRoleWhitelist(AddRemove action, [Leftover] IGuildUser user)
            {
                var success = await this._service.ApplyListAction(StreamRoleListType.Whitelist, ctx.Guild, action, user.Id, user.ToString())
                    .ConfigureAwait(false);

                if (action == AddRemove.Add)
                    if(success)
                        await ReplyConfirmLocalizedAsync("stream_role_wl_add", Format.Bold(user.ToString())).ConfigureAwait(false);
                    else
                        await ReplyConfirmLocalizedAsync("stream_role_wl_add_fail", Format.Bold(user.ToString())).ConfigureAwait(false);
                else 
                    if (success)
                        await ReplyConfirmLocalizedAsync("stream_role_wl_rem", Format.Bold(user.ToString())).ConfigureAwait(false);
                    else
                        await ReplyErrorLocalizedAsync("stream_role_wl_rem_fail", Format.Bold(user.ToString())).ConfigureAwait(false);
            }
        }
    }
}