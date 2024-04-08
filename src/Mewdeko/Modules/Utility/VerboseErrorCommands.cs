using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    /// Contains commands for managing verbose error messages.
    /// </summary>
    [Group]
    public class VerboseErrorCommands : MewdekoSubmodule<VerboseErrorsService>
    {
        /// <summary>
        /// Toggles verbose error messages for commands.
        /// </summary>
        /// <param name="newstate">The new state of verbose errors. If null, the state will be toggled.</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
        public async Task VerboseError(bool? newstate = null)
        {
            var state = await Service.ToggleVerboseErrors(ctx.Guild.Id, newstate);

            if (state)
                await ReplyConfirmLocalizedAsync("verbose_errors_enabled").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("verbose_errors_disabled").ConfigureAwait(false);
        }
    }
}