using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Runs code in a throwaway sandbox container.
    /// </summary>
    [Group]
    public class CodeRunCommands(CodeRunService service) : MewdekoSubmodule
    {
        /// <summary>
        ///     Runs code and shows what it printed, or lists the languages.
        /// </summary>
        /// <param name="input">A language and code, a fenced code block, or the word languages.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [Ratelimit(10)]
        public async Task Run([Remainder] string input = "")
        {
            var guildId = ctx.Guild.Id;
            if (!service.Available)
            {
                await Send(Strings.CodeRunUnavailable(guildId)).ConfigureAwait(false);
                return;
            }

            var parsed = CodeRunParser.Parse(input, service.Languages);
            switch (parsed.Status)
            {
                case CodeRunParseStatus.ListLanguages:
                    var installed = await service.GetInstalledAsync().ConfigureAwait(false);
                    await Send(CodeRunReply.ForLanguages(Strings, guildId, installed)).ConfigureAwait(false);
                    return;
                case not CodeRunParseStatus.Ok:
                    await Send(CodeRunReply.ForParseError(Strings, guildId, parsed)).ConfigureAwait(false);
                    return;
            }

            await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var result = await service.RunAsync(parsed.Language!, parsed.Code, ctx.User.Id, guildId)
                .ConfigureAwait(false);
            await Send(CodeRunReply.ForResult(Strings, guildId, parsed.Language!, result)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Pulls the images of every configured language and reports progress.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [OwnerOnly]
        public async Task PullRunImages()
        {
            var guildId = ctx.Guild?.Id ?? 0;
            if (!service.CanPullImages)
            {
                await Send(Strings.CodeRunUnavailable(guildId)).ConfigureAwait(false);
                return;
            }

            var total = service.Languages.All.Select(l => l.Image).Distinct().Count();
            var message = await Send(Strings.CodeRunPullStart(guildId, total)).ConfigureAwait(false);
            var failures = await service.PullAllAsync((done, count) =>
                message.ModifyAsync(m =>
                {
                    m.Content = Strings.CodeRunPullProgress(guildId, done, count);
                    m.AllowedMentions = AllowedMentions.None;
                })).ConfigureAwait(false);

            var lines = new List<string>
            {
                Strings.CodeRunPullDone(guildId, total, failures.Count)
            };
            lines.AddRange(failures.Select(f => Strings.CodeRunPullFailed(guildId, f.Language.Name, f.Error)));
            await Send(string.Join('\n', lines)).ConfigureAwait(false);
        }

        private Task<IUserMessage> Send(string text)
        {
            return ctx.Channel.SendMessageAsync(text, allowedMentions: AllowedMentions.None,
                messageReference: ctx.Message == null ? null : new MessageReference(ctx.Message.Id));
        }
    }
}
