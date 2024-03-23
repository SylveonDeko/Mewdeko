using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    /// The module for managing game voice channels.
    /// </summary>
    [Group]
    public class GameChannelCommands : MewdekoSubmodule<GameVoiceChannelService>
    {
        /// <summary>
        /// Toggles the current voice channel as the game voice channel for the guild.
        /// </summary>
        /// <remarks>
        /// This command requires the caller to have GuildPermission.Administrator and BotPermission.MoveMembers.
        /// </remarks>
        /// <example>.gamevoicechannel</example>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator),
         BotPerm(GuildPermission.MoveMembers)]
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