using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Xp
{
    public partial class Xp
    {
        public class ResetCommands : MewdekoSubmodule<XpService>
        {
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public Task XpReset(IGuildUser user)
            {
                return XpReset(user.Id);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public async Task XpReset(ulong userId)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(GetText("reset"))
                    .WithDescription(GetText("reset_user_confirm"));

                if (!await PromptUserConfirmAsync(embed, ctx.User.Id).ConfigureAwait(false))
                    return;

                _service.XpReset(ctx.Guild.Id, userId);

                await ReplyConfirmLocalizedAsync("reset_user", userId).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public async Task XpReset()
            {
                var embed = new EmbedBuilder()
                    .WithTitle(GetText("reset"))
                    .WithDescription(GetText("reset_server_confirm"));

                if (!await PromptUserConfirmAsync(embed, ctx.User.Id).ConfigureAwait(false))
                    return;

                _service.XpReset(ctx.Guild.Id);

                await ReplyConfirmLocalizedAsync("reset_server").ConfigureAwait(false);
            }
        }
    }
}