using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    [Group]
    public class PrefixCommands : MewdekoSubmodule
    {
        private readonly GuildSettingsService _guildSettings;

        public PrefixCommands(GuildSettingsService guildSettings) => _guildSettings = guildSettings;

        public enum Set
        {
            Set
        }

        [Cmd, Aliases, Priority(1)]
        public async Task PrefixCommand() =>
            await ReplyConfirmLocalizedAsync("prefix_current", Format.Code(await _guildSettings.GetPrefix(ctx.Guild)))
                .ConfigureAwait(false);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator), Priority(0)]
        public Task PrefixCommand(Set _, [Remainder] string prefix) => PrefixCommand(prefix);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator), Priority(0)]
        public async Task PrefixCommand([Remainder] string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                return;

            var oldPrefix = await _guildSettings.GetPrefix(ctx.Guild);
            var newPrefix = await _guildSettings.SetPrefix(ctx.Guild, prefix);

            await ReplyConfirmLocalizedAsync("prefix_new", Format.Code(oldPrefix), Format.Code(newPrefix))
                .ConfigureAwait(false);
        }
    }
}