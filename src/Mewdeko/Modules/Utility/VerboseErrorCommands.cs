using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility
{
    public partial class Utility
    {
        [Group]
        public class VerboseErrorCommands : MewdekoSubmodule<VerboseErrorsService>
        {
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            public async Task VerboseError(bool? newstate = null)
            {
                var state = _service.ToggleVerboseErrors(ctx.Guild.Id, newstate);

                if (state)
                    await ReplyConfirmLocalizedAsync("verbose_errors_enabled").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("verbose_errors_disabled").ConfigureAwait(false);
            }
        }
    }
}