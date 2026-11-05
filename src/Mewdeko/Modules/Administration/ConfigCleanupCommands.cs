using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Commands for finding and fixing settings that point at deleted channels or roles.
    /// </summary>
    [Group]
    public class ConfigCleanupCommands(GuildSettingsService gss) : MewdekoSubmodule<ConfigCleanupService>
    {
        /// <summary>
        ///     Shows the settings that point at deleted channels or roles without changing anything.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ConfigCleanup()
        {
            var findings = await Service.ScanAsync(ctx.Guild.Id).ConfigureAwait(false);
            if (findings == null)
            {
                await ReplyErrorAsync(Strings.CleanupNotReady(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (findings.Count == 0)
            {
                await ReplyConfirmAsync(Strings.CleanupNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ctx.Channel.EmbedAsync(new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.CleanupTitle(ctx.Guild.Id))
                .WithDescription(ConfigCleanupService.Describe(Strings, ctx.Guild.Id, findings))
                .WithFooter(Strings.CleanupFooter(ctx.Guild.Id, await gss.GetPrefix(ctx.Guild))))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Fixes the settings that point at deleted channels or roles.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ConfigCleanupApply()
        {
            var findings = await Service.ScanAsync(ctx.Guild.Id).ConfigureAwait(false);
            if (findings == null)
            {
                await ReplyErrorAsync(Strings.CleanupNotReady(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var fixedCount = await Service.ApplyAsync(ctx.Guild.Id, findings).ConfigureAwait(false);
            await ReplyConfirmAsync(fixedCount == 0
                ? Strings.CleanupNone(ctx.Guild.Id)
                : Strings.CleanupApplied(ctx.Guild.Id, fixedCount)).ConfigureAwait(false);
        }
    }
}
