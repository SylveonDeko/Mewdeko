using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Modules.Permissions;

public partial class Permissions
{
    [Group, OwnerOnly]
    public class GlobalPermissionCommands : MewdekoSubmodule<GlobalPermissionService>
    {
        [Cmd, Aliases]
        public async Task GlobalPermList()
        {
            var blockedModule = Service.BlockedModules;
            var blockedCommands = Service.BlockedCommands;
            if (blockedModule.Count == 0 && blockedCommands.Count == 0)
            {
                await ReplyErrorLocalizedAsync("lgp_none").ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder().WithOkColor();

            if (blockedModule.Count > 0)
            {
                embed.AddField(efb => efb
                    .WithName(GetText("blocked_modules"))
                    .WithValue(string.Join("\n", Service.BlockedModules))
                    .WithIsInline(false));
            }

            if (blockedCommands.Count > 0)
            {
                embed.AddField(efb => efb
                    .WithName(GetText("blocked_commands"))
                    .WithValue(string.Join("\n", Service.BlockedCommands))
                    .WithIsInline(false));
            }

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task ResetGlobalPerms()
        {
            await Service.Reset().ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("global_perms_reset").ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task GlobalModule(ModuleOrCrInfo module)
        {
            var moduleName = module.Name.ToLowerInvariant();

            var added = Service.ToggleModule(moduleName);

            if (added)
            {
                await ReplyConfirmLocalizedAsync("gmod_add", Format.Bold(module.Name)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmLocalizedAsync("gmod_remove", Format.Bold(module.Name)).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task GlobalCommand(CommandOrCrInfo cmd)
        {
            var commandName = cmd.Name.ToLowerInvariant();
            var added = Service.ToggleCommand(commandName);

            if (added)
            {
                await ReplyConfirmLocalizedAsync("gcmd_add", Format.Bold(cmd.Name)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmLocalizedAsync("gcmd_remove", Format.Bold(cmd.Name)).ConfigureAwait(false);
        }
    }
}