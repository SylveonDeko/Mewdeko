using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class SlashServerTools
{
    /// <summary>
    ///     Slash commands for checking and re-registering this server's slash commands.
    /// </summary>
    [Group("slash-commands", "Check and re-register this server's slash commands")]
    public class SlashCommandDiagnostics : MewdekoSlashModuleBase<SlashDiagnosticsService>
    {
        /// <summary>
        ///     Shows which slash commands Discord has for this server compared to what the bot expects.
        /// </summary>
        [SlashCommand("diagnose", "Compare this server's slash commands with what the bot expects")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task Diagnose()
        {
            await DeferAsync(true).ConfigureAwait(false);
            var diagnosis = await Service.DiagnoseAsync(ctx.Guild, ctx.Channel as IGuildChannel,
                ctx.User as IGuildUser).ConfigureAwait(false);
            await ctx.Interaction.FollowupAsync(
                embed: SlashDiagnosticsService.BuildEmbed(Strings, ctx.Guild.Id, diagnosis).Build(),
                ephemeral: true).ConfigureAwait(false);
        }

        /// <summary>
        ///     Registers this server's slash commands with Discord again.
        /// </summary>
        [SlashCommand("resync", "Register this server's slash commands again")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task Resync()
        {
            await DeferAsync(true).ConfigureAwait(false);
            if (await Service.ResyncAsync(ctx.Guild).ConfigureAwait(false))
                await EphemeralReplyConfirmAsync(Strings.SlashResyncDone(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await EphemeralReplyErrorAsync(Strings.SlashResyncFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }
}
