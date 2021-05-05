using Discord;
using Discord.Commands;
using System.Threading.Tasks;
using NadekoBot.Common.Attributes;

namespace NadekoBot.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class PrefixCommands : NadekoSubmodule
        {
            [NadekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public async Task PrefixCommand()
            {
                await ReplyConfirmLocalizedAsync("prefix_current", Format.Code(CmdHandler.GetPrefix(ctx.Guild))).ConfigureAwait(false);
            }

            public enum Set
            {
                Set
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [Priority(0)]
            public Task PrefixCommand(Set _, [Leftover] string prefix)
                => PrefixCommand(prefix);

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            [Priority(0)]
            public async Task PrefixCommand([Leftover]string prefix)
            {
                if (string.IsNullOrWhiteSpace(prefix))
                    return;

                var oldPrefix = base.Prefix;
                var newPrefix = CmdHandler.SetPrefix(ctx.Guild, prefix);

                await ReplyConfirmLocalizedAsync("prefix_new", Format.Code(oldPrefix), Format.Code(newPrefix)).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task DefPrefix([Leftover]string prefix = null)
            {
                if (string.IsNullOrWhiteSpace(prefix))
                {
                    await ReplyConfirmLocalizedAsync("defprefix_current", CmdHandler.GetPrefix()).ConfigureAwait(false);
                    return;
                }

                var oldPrefix = CmdHandler.GetPrefix();
                var newPrefix = CmdHandler.SetDefaultPrefix(prefix);

                await ReplyConfirmLocalizedAsync("defprefix_new", Format.Code(oldPrefix), Format.Code(newPrefix)).ConfigureAwait(false);
            }
        }
    }
}
