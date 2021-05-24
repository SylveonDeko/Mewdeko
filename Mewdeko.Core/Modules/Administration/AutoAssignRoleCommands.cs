using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class AutoAssignRoleCommands : MewdekoSubmodule<AutoAssignRoleService>
        {
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [Priority(1)]
            public async Task AutoAssignRole(params IRole[] role)
            {
                var guser = (IGuildUser) ctx.User;
                if (!role.Any())
                {
                    await AutoAssignRole2();
                    return;
                }

                if (role.Any())
                {
                    var roleIDs = role.Select(x => x.Id);
                    if (roleIDs.Contains(ctx.Guild.EveryoneRole.Id))
                        return;

                    _service.EnableAar(ctx.Guild.Id, string.Join(" ", roleIDs));
                    await ctx.Channel
                        .SendConfirmAsync("Auto role has been set to the roles: " +
                                          string.Join<string>("|", role.Select(x => x.Mention))).ConfigureAwait(false);
                }
            }

            public async Task AutoAssignRole2()
            {
                _service.DisableAar(ctx.Guild.Id);
                await ctx.Channel.SendConfirmAsync("Auto Assign Roles on join has been disabled!");
            }
        }
    }
}