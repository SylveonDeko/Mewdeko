using Discord;
using Discord.Commands;
using NadekoBot.Common.Attributes;
using NadekoBot.Common.TypeReaders;
using NadekoBot.Core.Services;
using NadekoBot.Extensions;
using NadekoBot.Modules.Permissions.Services;
using System.Linq;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Permissions
{
    public partial class Permissions
    {
        [Group]
        public class GlobalPermissionCommands : NadekoSubmodule
        {
            private GlobalPermissionService _service;
            private readonly DbService _db;

            public GlobalPermissionCommands(GlobalPermissionService service, DbService db)
            {
                _service = service;
                _db = db;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GlobalPermList()
            {
                var blockedModule = _service.BlockedModules;
                var blockedCommands = _service.BlockedCommands;
                if (!blockedModule.Any() && !blockedCommands.Any())
                {
                    await ReplyErrorLocalizedAsync("lgp_none").ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder().WithOkColor();

                if (blockedModule.Any())
                    embed.AddField(efb => efb
                        .WithName(GetText("blocked_modules"))
                        .WithValue(string.Join("\n", _service.BlockedModules))
                        .WithIsInline(false));

                if (blockedCommands.Any())
                    embed.AddField(efb => efb
                        .WithName(GetText("blocked_commands"))
                        .WithValue(string.Join("\n", _service.BlockedCommands))
                        .WithIsInline(false));

                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GlobalModule(ModuleOrCrInfo module)
            {
                var moduleName = module.Name.ToLowerInvariant();

                var added = _service.ToggleModule(moduleName);
                
                if (added)
                {
                    await ReplyConfirmLocalizedAsync("gmod_add", Format.Bold(module.Name)).ConfigureAwait(false);
                    return;
                }
                
                await ReplyConfirmLocalizedAsync("gmod_remove", Format.Bold(module.Name)).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GlobalCommand(CommandOrCrInfo cmd)
            {
                var commandName = cmd.Name.ToLowerInvariant();
                var added = _service.ToggleCommand(commandName);
                
                if (added)
                {
                    await ReplyConfirmLocalizedAsync("gcmd_add", Format.Bold(cmd.Name)).ConfigureAwait(false);
                    return;
                }
                
                await ReplyConfirmLocalizedAsync("gcmd_remove", Format.Bold(cmd.Name)).ConfigureAwait(false);
            }
        }
    }
}
