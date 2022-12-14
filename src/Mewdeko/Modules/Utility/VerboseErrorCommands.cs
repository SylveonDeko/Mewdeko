using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    [Group]
    public class VerboseErrorCommands : MewdekoSubmodule<VerboseErrorsService>
    {
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