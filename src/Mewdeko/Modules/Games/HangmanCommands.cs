using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Common.Hangman;
using Mewdeko.Modules.Games.Common.Hangman.Exceptions;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

public partial class Games
{
    [Group]
    public class HangmanCommands : MewdekoSubmodule<GamesService>
    {
        private readonly DiscordSocketClient client;
        private readonly GuildSettingsService guildSettings;

        public HangmanCommands(DiscordSocketClient client, GuildSettingsService guildSettings)
        {
            this.client = client;
            this.guildSettings = guildSettings;
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Hangmanlist() =>
            await ctx.Channel
                .SendConfirmAsync(
                    $"{Format.Code(GetText("hangman_types", await guildSettings.GetPrefix(ctx.Guild.Id)))}\n{string.Join("\n", Service.TermPool.Data.Keys)}").ConfigureAwait(false);

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Hangman([Remainder] string type = "random")
        {
            Hangman hm;
            try
            {
                hm = new Hangman(type, Service.TermPool);
            }
            catch (TermNotFoundException)
            {
                return;
            }

            if (!Service.HangmanGames.TryAdd(ctx.Channel.Id, hm))
            {
                hm.Dispose();
                await ReplyErrorLocalizedAsync("hangman_running").ConfigureAwait(false);
                return;
            }

            hm.OnGameEnded += Hm_OnGameEnded;
            hm.OnGuessFailed += Hm_OnGuessFailed;
            hm.OnGuessSucceeded += Hm_OnGuessSucceeded;
            hm.OnLetterAlreadyUsed += Hm_OnLetterAlreadyUsed;
            client.MessageReceived += ClientMessageReceived;

            try
            {
                await ctx.Channel.SendConfirmAsync($"{GetText("hangman_game_started")} ({hm.TermType})",
                        $"{hm.ScrambledWord}\n{hm.GetHangman()}")
                    .ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            await hm.EndedTask.ConfigureAwait(false);

            client.MessageReceived -= ClientMessageReceived;
            Service.HangmanGames.TryRemove(ctx.Channel.Id, out _);
            hm.Dispose();

            Task ClientMessageReceived(SocketMessage msg)
            {
                _ = Task.Run(() =>
                {
                    if (ctx.Channel.Id == msg.Channel.Id && !msg.Author.IsBot)
                        return hm.Input(msg.Author.Id, msg.Author.ToString(), msg.Content);
                    return Task.CompletedTask;
                });
                return Task.CompletedTask;
            }
        }

        private Task Hm_OnGameEnded(Hangman game, string? winner)
        {
            if (winner == null)
            {
                var loseEmbed = new EmbedBuilder().WithTitle($"Hangman Game ({game.TermType}) - Ended")
                    .WithDescription(Format.Bold("You lose."))
                    .AddField(efb => efb.WithName("It was").WithValue(game.Term.GetWord()))
                    .WithFooter(efb => efb.WithText(string.Join(" ", game.PreviousGuesses)))
                    .WithErrorColor();

                if (Uri.IsWellFormedUriString(game.Term.ImageUrl, UriKind.Absolute))
                    loseEmbed.WithImageUrl(game.Term.ImageUrl);

                return ctx.Channel.EmbedAsync(loseEmbed);
            }

            var winEmbed = new EmbedBuilder().WithTitle($"Hangman Game ({game.TermType}) - Ended")
                .WithDescription(Format.Bold($"{winner} Won."))
                .AddField(efb => efb.WithName("It was").WithValue(game.Term.GetWord()))
                .WithFooter(efb => efb.WithText(string.Join(" ", game.PreviousGuesses)))
                .WithOkColor();

            if (Uri.IsWellFormedUriString(game.Term.ImageUrl, UriKind.Absolute))
                winEmbed.WithImageUrl(game.Term.ImageUrl);

            return ctx.Channel.EmbedAsync(winEmbed);
        }

        private Task Hm_OnLetterAlreadyUsed(Hangman game, string user, char guess) =>
            ctx.Channel.SendErrorAsync($"Hangman Game ({game.TermType})",
                $"{user} Letter `{guess}` has already been used. You can guess again in 3 seconds.\n{game.ScrambledWord}\n{game.GetHangman()}",
                footer: string.Join(" ", game.PreviousGuesses));

        private Task Hm_OnGuessSucceeded(Hangman game, string user, char guess) =>
            ctx.Channel.SendConfirmAsync($"Hangman Game ({game.TermType})",
                $"{user} guessed a letter `{guess}`!\n{game.ScrambledWord}\n{game.GetHangman()}",
                footer: string.Join(" ", game.PreviousGuesses));

        private Task Hm_OnGuessFailed(Hangman game, string user, char guess) =>
            ctx.Channel.SendErrorAsync($"Hangman Game ({game.TermType})",
                $"{user} Letter `{guess}` does not exist. You can guess again in 3 seconds.\n{game.ScrambledWord}\n{game.GetHangman()}",
                footer: string.Join(" ", game.PreviousGuesses));

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task HangmanStop()
        {
            if (Service.HangmanGames.TryRemove(ctx.Channel.Id, out var removed))
            {
                await removed.Stop().ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("hangman_stopped").ConfigureAwait(false);
            }
        }
    }
}