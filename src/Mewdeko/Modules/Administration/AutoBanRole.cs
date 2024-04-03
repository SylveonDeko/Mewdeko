using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration;

/// <summary>
/// Command group for managing the AutoBanRole feature.
/// </summary>
public class AutoBanRole : MewdekoSubmodule<AutoBanRoleService>
{
    /// <summary>
    /// Adds a role to the list of AutoBanRoles.
    /// </summary>
    /// <param name="role">The role to add</param>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator)]
    public async Task AutoBanRoleAdd(IRole role)
    {
        var success = await Service.AddAutoBanRole(Context.Guild.Id, role.Id);
        if (success)
        {
            await ReplyConfirmLocalizedAsync("abrole_add", role.Mention).ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorLocalizedAsync("abrole_exists", role.Mention).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Removes a role from the list of AutoBanRoles.
    /// </summary>
    /// <param name="role">The role to remove</param>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator)]
    public async Task AutoBanRoleRemove(IRole role)
    {
        var success = await Service.RemoveAutoBanRole(Context.Guild.Id, role.Id);
        if (success)
        {
            await ReplyConfirmLocalizedAsync("abrole_remove", role.Mention).ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorLocalizedAsync("abrole_notexists", role.Mention).ConfigureAwait(false);
        }
    }
}