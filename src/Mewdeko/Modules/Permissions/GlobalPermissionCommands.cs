using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Modules.Permissions;

public partial class Permissions
{
    /// <summary>
    /// Provides commands for managing global permissions, allowing for the blocking or unblocking of specific commands and modules across all guilds.
    /// </summary>
    [Group, OwnerOnly]
    public class GlobalPermissionCommands : MewdekoSubmodule<GlobalPermissionService>
    {
        /// <summary>
        /// Lists all currently globally blocked modules and commands.
        /// </summary>
        /// <returns>A task representing the asynchronous operation to send the list of globally blocked modules and commands.</returns>
        /// <remarks>
        /// This command is restricted to bot owners. It provides an overview of all modules and commands that have been globally restricted.
        /// </remarks>
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

        /// <summary>
        /// Resets all global permissions, clearing all global command and module blocks.
        /// </summary>
        /// <returns>A task representing the asynchronous operation to reset global permissions.</returns>
        /// <remarks>
        /// This command is restricted to bot owners. Use this command with caution as it will remove all global restrictions.
        /// </remarks>
        [Cmd, Aliases]
        public async Task ResetGlobalPerms()
        {
            await Service.Reset().ConfigureAwait(false);
            await ReplyConfirmLocalizedAsync("global_perms_reset").ConfigureAwait(false);
        }

        /// <summary>
        /// Toggles a module on or off the global block list.
        /// </summary>
        /// <param name="module">The module to toggle.</param>
        /// <returns>A task representing the asynchronous operation to block or unblock the module globally.</returns>
        /// <remarks>
        /// This command is restricted to bot owners. It allows for specifying modules to be globally blocked or unblocked.
        /// </remarks>
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

        /// <summary>
        /// Toggles a command on or off the global block list.
        /// </summary>
        /// <param name="cmd">The command to toggle.</param>
        /// <returns>A task representing the asynchronous operation to block or unblock the command globally.</returns>
        /// <remarks>
        /// This command is restricted to bot owners. Certain commands, like "source", are protected from being globally disabled.
        /// </remarks>
        [Cmd, Aliases]
        public async Task GlobalCommand(CommandOrCrInfo cmd)
        {
            var commandName = cmd.Name.ToLowerInvariant();
            if (commandName is "source")
            {
                await ctx.Channel
                    .SendErrorAsync("That command is not allowed to be globally disabled. What are you trying to do?",
                        Config)
                    .ConfigureAwait(false);
                return;
            }

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