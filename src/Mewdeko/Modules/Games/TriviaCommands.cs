using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Common.Trivia;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

public partial class Games
{
    [Group]
    public class TriviaCommands(DiscordSocketClient client, IDataCache cache,
            GamesConfigService gamesConfig,
            GuildSettingsService guildSettings)
        : MewdekoSubmodule<GamesService>
    {
        [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(0),
         MewdekoOptions(typeof(TriviaOptions))]
        public Task Trivia(params string[] args) => InternalTrivia(args);

        public async Task InternalTrivia(params string[] args)
        {
            var channel = (ITextChannel)ctx.Channel;

            var (opts, _) = OptionsParser.ParseFrom(new TriviaOptions(), args);

            var config = gamesConfig.Data;
            if (config.Trivia.MinimumWinReq > 0 && config.Trivia.MinimumWinReq > opts.WinRequirement) return;
            var trivia = new TriviaGame(Strings, client, cache, channel.Guild, channel, opts,
                $"{await guildSettings.GetPrefix(ctx.Guild)}tq");
            if (Service.RunningTrivias.TryAdd(channel.Guild.Id, trivia))
            {
                try
                {
                    await trivia.StartGame().ConfigureAwait(false);
                }
                finally
                {
                    Service.RunningTrivias.TryRemove(channel.Guild.Id, out trivia);
                    await trivia.EnsureStopped().ConfigureAwait(false);
                }

                return;
            }

            await ctx.Channel.SendErrorAsync($"{GetText("trivia_already_running")}\n{trivia.CurrentQuestion}")
                .ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Tl()
        {
            var channel = (ITextChannel)ctx.Channel;

            if (Service.RunningTrivias.TryGetValue(channel.Guild.Id, out var trivia))
            {
                await channel.SendConfirmAsync(GetText("leaderboard"), trivia.GetLeaderboard())
                    .ConfigureAwait(false);
                return;
            }

            await ReplyErrorLocalizedAsync("trivia_none").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Tq()
        {
            var channel = (ITextChannel)ctx.Channel;

            if (Service.RunningTrivias.TryGetValue(channel.Guild.Id, out var trivia))
            {
                await trivia.StopGame().ConfigureAwait(false);
                return;
            }

            await ReplyErrorLocalizedAsync("trivia_none").ConfigureAwait(false);
        }
    }
}