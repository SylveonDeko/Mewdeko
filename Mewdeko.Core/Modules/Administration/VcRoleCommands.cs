using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class VcRoleCommands : MewdekoSubmodule<VcRoleService>
        {
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            [RequireContext(ContextType.Guild)]
            public async Task VcRoleRm(ulong vcId)
            {
                if (_service.RemoveVcRole(ctx.Guild.Id, vcId))
                    await ReplyConfirmLocalizedAsync("vcrole_removed", Format.Bold(vcId.ToString()))
                        .ConfigureAwait(false);
                else
                    await ReplyErrorLocalizedAsync("vcrole_not_found").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            [RequireContext(ContextType.Guild)]
            public async Task VcRole([Leftover] IRole role = null)
            {
                var user = (IGuildUser) ctx.User;

                var vc = user.VoiceChannel;

                if (vc == null || vc.GuildId != user.GuildId)
                {
                    await ReplyErrorLocalizedAsync("must_be_in_voice").ConfigureAwait(false);
                    return;
                }

                if (role == null)
                {
                    if (_service.RemoveVcRole(ctx.Guild.Id, vc.Id))
                        await ReplyConfirmLocalizedAsync("vcrole_removed", Format.Bold(vc.Name)).ConfigureAwait(false);
                }
                else
                {
                    _service.AddVcRole(ctx.Guild.Id, role, vc.Id);
                    await ReplyConfirmLocalizedAsync("vcrole_added", Format.Bold(vc.Name), Format.Bold(role.Name))
                        .ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task VcRoleList()
            {
                var guild = (SocketGuild) ctx.Guild;
                string text;
                if (_service.VcRoles.TryGetValue(ctx.Guild.Id, out var roles))
                {
                    if (!roles.Any())
                        text = GetText("no_vcroles");
                    else
                        text = string.Join("\n", roles.Select(x =>
                            $"{Format.Bold(guild.GetVoiceChannel(x.Key)?.Name ?? x.Key.ToString())} => {x.Value}"));
                }
                else
                {
                    text = GetText("no_vcroles");
                }

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle(GetText("vc_role_list"))
                        .WithDescription(text))
                    .ConfigureAwait(false);
            }
        }
    }
}