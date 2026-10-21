using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.FluxerMigration.Services;

namespace Mewdeko.Modules.FluxerMigration;

/// <summary>
///     Module for moving a server and its members over to Fluxer.
/// </summary>
public class FluxerMigration : MewdekoModuleBase<FluxerMigrationService>
{
    /// <summary>
    ///     Exports the server and DMs the code needed to import it on Fluxer.
    /// </summary>
    /// <example>.fluxermigrate</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task FluxerMigrate()
    {
        var guildId = ctx.Guild.Id;
        var working = await LoadingLocalizedAsync(Strings.FluxermigrationWorking(guildId)).ConfigureAwait(false);

        FluxerMigrationService.ExportResult result;
        using (ctx.Channel.EnterTypingState())
        {
            try
            {
                result = await Service.ExportAsync(ctx.Guild, ctx.User.Id).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await working.DeleteAsync().ConfigureAwait(false);
                await ReplyErrorAsync(Strings.FluxermigrationFailed(guildId)).ConfigureAwait(false);
                return;
            }
        }

        await working.DeleteAsync().ConfigureAwait(false);

        try
        {
            await ctx.User.SendMessageAsync(Strings.FluxermigrationDm(guildId, ctx.Guild.Name, result.Code))
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            await ReplyErrorAsync(Strings.FluxermigrationDmFailed(guildId)).ConfigureAwait(false);
            return;
        }

        await ReplyConfirmAsync(Strings.FluxermigrationExported(guildId, result.Tables, result.Rows))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     DMs a code that links the user's Discord account to their Fluxer account.
    /// </summary>
    /// <example>.fluxerlink</example>
    [Cmd]
    [Aliases]
    public async Task FluxerLink()
    {
        var guildId = ctx.Guild?.Id ?? 0;
        var code = await Service.CreateLinkCodeAsync(ctx.User.Id).ConfigureAwait(false);

        try
        {
            await ctx.User.SendMessageAsync(Strings.FluxerlinkDm(guildId, code)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await ReplyErrorAsync(Strings.FluxerlinkDmFailed(guildId)).ConfigureAwait(false);
            return;
        }

        if (ctx.Guild is not null)
            await ReplyConfirmAsync(Strings.FluxerlinkSent(guildId)).ConfigureAwait(false);
    }
}
