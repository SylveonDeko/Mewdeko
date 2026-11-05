using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Commands for checking and re-registering this server's slash commands.
    /// </summary>
    [Group]
    public class SlashDiagnosticsCommands : MewdekoSubmodule<SlashDiagnosticsService>
    {
        /// <summary>
        ///     Shows which slash commands Discord has for this server compared to what the bot expects, and what is
        ///     missing for slash usage in this channel.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task SlashDiagnose()
        {
            var diagnosis = await Service.DiagnoseAsync(ctx.Guild, ctx.Channel as IGuildChannel,
                ctx.User as IGuildUser).ConfigureAwait(false);
            await ctx.Channel.EmbedAsync(SlashDiagnosticsService.BuildEmbed(Strings, ctx.Guild.Id, diagnosis))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Registers this server's slash commands with Discord again.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task SlashResync()
        {
            if (await Service.ResyncAsync(ctx.Guild).ConfigureAwait(false))
                await ReplyConfirmAsync(Strings.SlashResyncDone(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.SlashResyncFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }
}
