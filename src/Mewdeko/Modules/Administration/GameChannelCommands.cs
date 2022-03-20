using Discord;
using Discord.Commands;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    [Group]
    public class GameChannelCommands : MewdekoSubmodule<GameVoiceChannelService>
    {
        private readonly Mewdeko _bot;

        public GameChannelCommands(Mewdeko bot) 
            => _bot = bot;

        [MewdekoCommand, Usage, Description, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator), BotPerm(GuildPermission.MoveMembers)]
        public async Task GameVoiceChannel()
        {
            var vch = ((IGuildUser) ctx.User).VoiceChannel;

            if (vch == null)
            {
                await ReplyErrorLocalizedAsync("not_in_voice").ConfigureAwait(false);
                return;
            }

            var id = Service.ToggleGameVoiceChannel(ctx.Guild.Id, vch.Id);

            if (id == null)
            {
                await ReplyConfirmLocalizedAsync("gvc_disabled").ConfigureAwait(false);
            }
            else
            {
                _bot.AllGuildConfigs[ctx.Guild.Id].GameVoiceChannel = vch.Id;
                await ReplyConfirmLocalizedAsync("gvc_enabled", Format.Bold(vch.Name)).ConfigureAwait(false);
            }
        }
    }
}