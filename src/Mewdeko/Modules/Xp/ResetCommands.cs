using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Xp;

public partial class Xp
{
    public class ResetCommands : MewdekoSubmodule<XpService>
    {
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public Task XpReset(IGuildUser user) => XpReset(user.Id);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task XpReset(ulong userId)
        {
            var embed = new EmbedBuilder()
                .WithTitle(GetText("reset"))
                .WithDescription(GetText("reset_user_confirm"));

            if (!await PromptUserConfirmAsync(embed, ctx.User.Id).ConfigureAwait(false))
                return;

            Service.XpReset(ctx.Guild.Id, userId);

            await ReplyConfirmLocalizedAsync("reset_user", userId).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task XpReset()
        {
            var embed = new EmbedBuilder()
                .WithTitle(GetText("reset"))
                .WithDescription(GetText("reset_server_confirm"));

            if (!await PromptUserConfirmAsync(embed, ctx.User.Id).ConfigureAwait(false))
                return;

            Service.XpReset(ctx.Guild.Id);

            await ReplyConfirmLocalizedAsync("reset_server").ConfigureAwait(false);
        }
    }
}