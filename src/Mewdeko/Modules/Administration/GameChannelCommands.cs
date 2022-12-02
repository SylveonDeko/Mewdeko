using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    [Group]
    public class GameChannelCommands : MewdekoSubmodule<GameVoiceChannelService>
    {
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator), BotPerm(GuildPermission.MoveMembers)]
        public async Task GameVoiceChannel()
        {
            var vch = ((IGuildUser)ctx.User).VoiceChannel;

            if (vch == null)
            {
                await ReplyErrorLocalizedAsync("not_in_voice").ConfigureAwait(false);
                return;
            }

            var id = await Service.ToggleGameVoiceChannel(ctx.Guild.Id, vch.Id);

            if (id == null)
            {
                await ReplyConfirmLocalizedAsync("gvc_disabled").ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("gvc_enabled", Format.Bold(vch.Name)).ConfigureAwait(false);
            }
        }
    }
}