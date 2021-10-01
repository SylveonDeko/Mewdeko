using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Modules.Permissions
{
    public partial class Permissions
    {
        [Group]
        public class ResetPermissionsCommands : MewdekoSubmodule
        {
            private readonly GlobalPermissionService _gps;
            private readonly PermissionService _perms;

            public ResetPermissionsCommands(GlobalPermissionService gps, PermissionService perms)
            {
                _gps = gps;
                _perms = perms;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public async Task ResetPerms()
            {
                await _perms.Reset(ctx.Guild.Id).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("perms_reset").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OwnerOnly]
            public async Task ResetGlobalPerms()
            {
                await _gps.Reset();
                await ReplyConfirmLocalizedAsync("global_perms_reset").ConfigureAwait(false);
            }
        }
    }
}