using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

/// <summary>
///     Slash commands for finding and fixing settings that point at deleted channels or roles.
/// </summary>
[Group("config-cleanup", "Find settings that point at deleted channels or roles")]
public class SlashConfigCleanup : MewdekoSlashModuleBase<ConfigCleanupService>
{
    /// <summary>
    ///     Shows the settings that point at deleted channels or roles without changing anything.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("preview", "Shows the settings that point at deleted channels or roles")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Preview()
    {
        await DeferAsync(true).ConfigureAwait(false);
        var findings = await Service.ScanAsync(ctx.Guild.Id).ConfigureAwait(false);
        if (findings == null)
        {
            await ctx.Interaction.FollowupAsync(Strings.CleanupNotReady(ctx.Guild.Id), ephemeral: true)
                .ConfigureAwait(false);
            return;
        }

        if (findings.Count == 0)
        {
            await ctx.Interaction.FollowupAsync(Strings.CleanupNone(ctx.Guild.Id), ephemeral: true)
                .ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.CleanupTitle(ctx.Guild.Id))
            .WithDescription(ConfigCleanupService.Describe(Strings, ctx.Guild.Id, findings))
            .WithFooter(Strings.CleanupFooterSlash(ctx.Guild.Id))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Fixes the settings that point at deleted channels or roles.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("apply", "Fixes the settings that point at deleted channels or roles")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Apply()
    {
        await DeferAsync(true).ConfigureAwait(false);
        var findings = await Service.ScanAsync(ctx.Guild.Id).ConfigureAwait(false);
        if (findings == null)
        {
            await ctx.Interaction.FollowupAsync(Strings.CleanupNotReady(ctx.Guild.Id), ephemeral: true)
                .ConfigureAwait(false);
            return;
        }

        var fixedCount = await Service.ApplyAsync(ctx.Guild.Id, findings).ConfigureAwait(false);
        await ctx.Interaction.FollowupAsync(fixedCount == 0
            ? Strings.CleanupNone(ctx.Guild.Id)
            : Strings.CleanupApplied(ctx.Guild.Id, fixedCount), ephemeral: true).ConfigureAwait(false);
    }
}
