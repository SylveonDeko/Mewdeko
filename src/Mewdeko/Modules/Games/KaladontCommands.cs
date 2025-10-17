using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Common.Kaladont;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

/// <summary>
///     A module containing various games.
/// </summary>
public partial class Games
{
    /// <summary>
    ///     A module containing Kaladont commands.
    /// </summary>
    [Group]
    public class KaladontCommands(EventHandler handler) : MewdekoSubmodule<GamesService>
    {
        /// <summary>
        ///     Command for starting a Kaladont game.
        /// </summary>
        /// <param name="args">Arguments passed to the command.</param>
        /// <example>.kaladont</example>
        /// <example>.kaladont -l sr</example>
        /// <example>.kaladont -t 45 -l sr</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [MewdekoOptions(typeof(KaladontGame.Options))]
        public async Task Kaladont(params string[] args)
        {
            var (options, _) = OptionsParser.ParseFrom(new KaladontGame.Options(), args);
            var channel = (ITextChannel)ctx.Channel;

            // Get dictionary for selected language
            var dictionary = Service.GetKaladontDictionary(options.Language);
            if (dictionary.Count == 0)
            {
                await ReplyErrorAsync(
                        $"Dictionary for language '{options.Language}' is not loaded or is empty. Please ensure the dictionary file exists in data/kaladont/")
                    .ConfigureAwait(false);
                return;
            }

            // Get random starting word
            var startingWord = Service.GetRandomKaladontStartingWord(options.Language);

            var game = new KaladontGame(options, startingWord, dictionary);

            if (Service.KaladontGames.TryAdd(channel.Id, game))
            {
                try
                {
                    // Subscribe to game events
                    game.OnGameStarted += Game_OnStarted;
                    game.OnPlayerTurn += Game_OnPlayerTurn;
                    game.OnWordPlayed += Game_OnWordPlayed;
                    game.OnPlayerEliminated += Game_OnPlayerEliminated;
                    game.OnGameEnded += Game_OnGameEnded;

                    // Subscribe to message events
                    handler.Subscribe("MessageReceived", "KaladontCommands", ClientMessageReceived);
                    handler.Subscribe("ReactionAdded", "KaladontCommands", ClientReactionAdded);

                    // Show join phase message
                    await ShowJoinPhase(game).ConfigureAwait(false);

                    // Wait for players to join and start game
                    var success = await game.Initialize().ConfigureAwait(false);

                    if (!success)
                    {
                        await ctx.Channel.SendErrorAsync(
                            Strings.Kaladont(ctx.Guild.Id),
                            Strings.KaladontNotEnoughPlayers(ctx.Guild.Id, game.Opts.MinPlayers)
                        ).ConfigureAwait(false);
                    }
                }
                finally
                {
                    handler.Unsubscribe("MessageReceived", "KaladontCommands", ClientMessageReceived);
                    handler.Unsubscribe("ReactionAdded", "KaladontCommands", ClientReactionAdded);
                    Service.KaladontGames.TryRemove(channel.Id, out game);
                    game?.Dispose();
                }
            }
            else
            {
                await ReplyErrorAsync(Strings.KaladontAlreadyRunning(ctx.Guild.Id)).ConfigureAwait(false);
            }

            async Task ClientMessageReceived(SocketMessage msg)
            {
                if (msg.Channel.Id != ctx.Channel.Id || msg.Author.IsBot)
                    return;

                var content = msg.Content?.Trim();
                if (string.IsNullOrEmpty(content))
                    return;

                // Check if user is saying "kaladont" to give up
                if (content.Equals("kaladont", StringComparison.OrdinalIgnoreCase))
                {
                    var success = await game.SayKaladont(msg.Author.Id).ConfigureAwait(false);
                    if (success)
                    {
                        try
                        {
                            await msg.DeleteAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                            // Ignore deletion errors
                        }
                    }

                    return;
                }

                // Try to play the word
                var (wordSuccess, validationResult) = await game.PlayWord(msg.Author.Id, content).ConfigureAwait(false);

                if (wordSuccess || validationResult != KaladontGame.ValidationResult.Valid)
                {
                    // Delete the message if it was a game-related input
                    try
                    {
                        await msg.DeleteAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignore deletion errors
                    }

                    // Show error for invalid words
                    if (!wordSuccess && validationResult != KaladontGame.ValidationResult.Valid)
                    {
                        var errorMessage = GetValidationErrorMessage(validationResult, content);
                        await channel.SendErrorAsync(Strings.Kaladont(ctx.Guild.Id), errorMessage)
                            .ConfigureAwait(false);
                    }
                }
            }

            async Task ClientReactionAdded(Cacheable<IUserMessage, ulong> message,
                Cacheable<IMessageChannel, ulong> reactionChannel, SocketReaction reaction)
            {
                if (reactionChannel.Id != ctx.Channel.Id || reaction.UserId == ctx.Client.CurrentUser.Id)
                    return;

                if (game.CurrentPhase == KaladontGame.Phase.Joining && reaction.Emote.Name == "✅")
                {
                    var user = await ctx.Guild.GetUserAsync(reaction.UserId).ConfigureAwait(false);
                    if (user == null || user.IsBot)
                        return;

                    var joined = await game.Join(user.Id, user.ToString()).ConfigureAwait(false);
                    if (joined)
                    {
                        await channel.SendConfirmAsync(
                            Strings.KaladontPlayerJoined(ctx.Guild.Id, Format.Bold(user.ToString()),
                                game.Players.Length, game.Opts.MinPlayers)
                        ).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        ///     Command for stopping the current Kaladont game.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task KaladontStop()
        {
            var channel = (ITextChannel)ctx.Channel;

            if (Service.KaladontGames.TryGetValue(channel.Id, out var game))
            {
                await game.StopGame().ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.KaladontStopped(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync("No Kaladont game is running in this channel.").ConfigureAwait(false);
            }
        }

        private async Task ShowJoinPhase(KaladontGame game)
        {
            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"🎮 {Strings.Kaladont(ctx.Guild.Id)}")
                .WithDescription(Strings.KaladontJoinPhase(ctx.Guild.Id, game.Opts.JoinTime, game.Opts.MinPlayers))
                .AddField(Strings.KaladontRules(ctx.Guild.Id, game.Opts.TurnTime), "\u200b")
                .WithFooter($"Language: {game.Opts.Language.ToUpperInvariant()} | Join time: {game.Opts.JoinTime}s");

            var msg = await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            await msg.AddReactionAsync(new Emoji("✅")).ConfigureAwait(false);
        }

        private Task Game_OnStarted(KaladontGame game)
        {
            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"🎮 {Strings.Kaladont(ctx.Guild.Id)}")
                .WithDescription(Strings.KaladontGameStarted(ctx.Guild.Id,
                    Format.Bold(game.CurrentWord),
                    game.CurrentPlayer != null ? Format.Bold(game.CurrentPlayer.UserName) : "?"))
                .AddField("Players", string.Join("\n", game.Players.Select((p, i) => $"{i + 1}. {p.UserName}")), true)
                .AddField("Starting Word", Format.Bold(game.CurrentWord.ToUpperInvariant()), true)
                .WithFooter(Strings.KaladontGameStartedFooter(ctx.Guild.Id, game.Opts.TurnTime));

            return ctx.Channel.EmbedAsync(embed);
        }

        private Task Game_OnPlayerTurn(KaladontGame game, KaladontPlayer player)
        {
            var lastTwo = game.CurrentWord[^2..].ToUpperInvariant();

            var embed = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithTitle($"⏰ {Strings.Kaladont(ctx.Guild.Id)}")
                .WithDescription(Strings.KaladontPlayerTurn(ctx.Guild.Id,
                    Format.Bold(player.UserName),
                    Format.Bold(game.CurrentWord),
                    Format.Bold(lastTwo)))
                .AddField("Last 2 Letters", Format.Bold(lastTwo), true)
                .AddField("Words Used", game.UsedWordsCount.ToString(), true);

            if (game.RecentWords.Length > 0)
            {
                embed.AddField("Recent Words",
                    string.Join(" → ", game.RecentWords.TakeLast(5).Select(w => Format.Code(w))));
            }

            embed.WithFooter($"⏱️ {game.Opts.TurnTime}s remaining");

            return ctx.Channel.EmbedAsync(embed);
        }

        private Task Game_OnWordPlayed(KaladontGame game, KaladontPlayer player, string word)
        {
            return ctx.Channel.SendConfirmAsync(
                $"✅ {Strings.KaladontWordPlayed(ctx.Guild.Id, Format.Bold(player.UserName), Format.Bold(word))}"
            );
        }

        private async Task Game_OnPlayerEliminated(KaladontGame game, KaladontPlayer player, string reason)
        {
            var reasonText = reason switch
            {
                "timeout" => Strings.KaladontPlayerTimeout(ctx.Guild.Id),
                "kaladont" => Strings.KaladontUserSaidKaladont(ctx.Guild.Id),
                "too_short" => Strings.KaladontInvalidLength(ctx.Guild.Id),
                "already_used" => Strings.KaladontAlreadyUsed(ctx.Guild.Id, ""),
                "wrong_letters" => Strings.KaladontWrongLetters(ctx.Guild.Id, ""),
                "kaladont_loop" => Strings.KaladontLoopDetected(ctx.Guild.Id, ""),
                "not_in_dictionary" => Strings.KaladontNotFound(ctx.Guild.Id, ""),
                _ => reason
            };

            var embed = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle($"💀 {Strings.Kaladont(ctx.Guild.Id)}")
                .WithDescription(Strings.KaladontEliminated(ctx.Guild.Id, Format.Bold(player.UserName), reasonText))
                .AddField("Players Remaining", game.Players.Length.ToString());

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        private Task Game_OnGameEnded(KaladontGame game, KaladontPlayer? winner)
        {
            var embed = new EmbedBuilder()
                .WithTitle($"🏆 {Strings.KaladontGameEnded(ctx.Guild.Id)}")
                .AddField("Total Words", game.UsedWordsCount.ToString(), true)
                .AddField("Language", game.Opts.Language.ToUpperInvariant(), true);

            if (winner != null)
            {
                embed.WithOkColor()
                    .WithDescription(Strings.KaladontWinner(ctx.Guild.Id, Format.Bold(winner.UserName)));
            }
            else
            {
                embed.WithErrorColor()
                    .WithDescription("Game ended with no winner.");
            }

            return ctx.Channel.EmbedAsync(embed);
        }

        private string GetValidationErrorMessage(KaladontGame.ValidationResult result, string word)
        {
            return result switch
            {
                KaladontGame.ValidationResult.TooShort => Strings.KaladontInvalidLength(ctx.Guild.Id),
                KaladontGame.ValidationResult.AlreadyUsed => Strings.KaladontAlreadyUsed(ctx.Guild.Id,
                    Format.Bold(word)),
                KaladontGame.ValidationResult.WrongLetters => Strings.KaladontWrongLetters(ctx.Guild.Id, ""),
                KaladontGame.ValidationResult.KaladontLoop => Strings.KaladontLoopDetected(ctx.Guild.Id,
                    Format.Bold(word)),
                KaladontGame.ValidationResult.NotInDictionary => Strings.KaladontNotFound(ctx.Guild.Id,
                    Format.Bold(word)),
                _ => "Invalid word"
            };
        }
    }
}