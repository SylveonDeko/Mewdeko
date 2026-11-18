using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

/// <summary>
///     Slash command for running code in a throwaway sandbox container.
/// </summary>
public class SlashCodeRun(CodeRunService service) : MewdekoSlashCommandModule
{
    /// <summary>
    ///     Opens a form for the language and code.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("run", "Runs code in a sandbox and shows the output")]
    [RequireContext(ContextType.Guild)]
    [InteractionRatelimit(10)]
    public async Task Run()
    {
        if (!service.Available)
        {
            await RespondAsync(Strings.CodeRunUnavailable(ctx.Guild.Id), ephemeral: true,
                allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        await RespondWithModalAsync<CodeRunModal>("coderun_modal").ConfigureAwait(false);
    }

    /// <summary>
    ///     Runs the code from the form.
    /// </summary>
    /// <param name="modal">The submitted form.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ModalInteraction("coderun_modal", true)]
    [RequireContext(ContextType.Guild)]
    public async Task Submitted(CodeRunModal modal)
    {
        var guildId = ctx.Guild.Id;
        if (!service.Available)
        {
            await RespondAsync(Strings.CodeRunUnavailable(guildId), ephemeral: true,
                allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        var languageWord = string.Concat((modal.Language ?? "").Trim().Select(c => char.IsWhiteSpace(c) ? '_' : c));
        var parsed = CodeRunParser.Parse($"{languageWord} {modal.Code}", service.Languages);
        if (parsed.Status != CodeRunParseStatus.Ok)
        {
            await RespondAsync(CodeRunReply.ForParseError(Strings, guildId, parsed), ephemeral: true,
                allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        await DeferAsync().ConfigureAwait(false);
        var result = await service.RunAsync(parsed.Language!, parsed.Code, ctx.User.Id, guildId)
            .ConfigureAwait(false);
        await FollowupAsync(CodeRunReply.ForResult(Strings, guildId, parsed.Language!, result),
            allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }
}
