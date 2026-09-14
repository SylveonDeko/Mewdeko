using System.IO;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Currency.Common;
using Mewdeko.Modules.Currency.Services;
using SkiaSharp;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Currency;

/// <summary>
///     The gambling games and the daily challenge slash commands.
/// </summary>
public partial class SlashCurrency
{
    /// <summary>
    ///     Gambling games played against the house or other members.
    /// </summary>
    [Group("games", "Gambling games")]
    public partial class CurrencyGames(
        BlackjackService blackjackService,
        HorseRacingService horseRacingService,
        ITriviaChainService triviaChainService)
        : SlashCurrencyBase
    {
        /// <summary>
        ///     Allows the user to flip a coin with a specified bet amount and guess.
        /// </summary>
        /// <param name="betAmount">The amount to bet.</param>
        /// <param name="guess">The user's guess, heads or tails.</param>
        [SlashCommand("coinflip", "Flips a coin, doubling your bet if you call it right")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task CoinFlip([Summary("bet", "The amount to bet")] long betAmount,
            [Summary("guess", "Heads or tails")] CoinSide guess)
        {
            await DeferAsync();

            if (!await TryTakeBetAsync(betAmount, "coinflip"))
                return;

            var guessText = guess.ToString().ToLowerInvariant();
            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);
            var coinFlip = CurrencyRng.Next(2) == 0 ? "heads" : "tails";

            if (coinFlip.Equals(guessText, StringComparison.OrdinalIgnoreCase))
            {
                await WinAsync(betAmount, 2.0, "coinflip");
                await ctx.Interaction.FollowupAsync(Strings.CoinflipWon(ctx.Guild.Id, coinFlip, betAmount, emote));
                return;
            }

            await ctx.Interaction.FollowupAsync(Strings.CoinflipLost(ctx.Guild.Id, coinFlip, betAmount, emote));
        }

        /// <summary>
        ///     Shows a number from 1 to 10 and pays out if you correctly call whether the next one is higher
        ///     or lower. Matching numbers return the stake.
        /// </summary>
        /// <param name="guess">The user's guess, higher or lower.</param>
        /// <param name="betAmount">The amount to bet.</param>
        [SlashCommand("highlow", "Call whether the next number from 1 to 10 is higher or lower")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task HighLow([Summary("guess", "Higher or lower")] HighLowGuess guess,
            [Summary("bet", "The amount to bet")] long betAmount = 100)
        {
            await DeferAsync();
            var higher = guess == HighLowGuess.Higher;
            var lower = guess == HighLowGuess.Lower;

            if (!await TryTakeBetAsync(betAmount, "highlow"))
                return;

            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);
            var currentNumber = CurrencyRng.Next(1, 11);
            var nextNumber = CurrencyRng.Next(1, 11);

            if (nextNumber == currentNumber)
            {
                await PushAsync(betAmount, "highlow");
                await ctx.Interaction.FollowupAsync(Strings.HighlowPush(ctx.Guild.Id, currentNumber, betAmount, emote));
                return;
            }

            if (higher && nextNumber > currentNumber || lower && nextNumber < currentNumber)
            {
                await WinAsync(betAmount, 2.0, "highlow");
                await ctx.Interaction.FollowupAsync(Strings.HighlowWon(ctx.Guild.Id, currentNumber, nextNumber, emote));
                return;
            }

            await ctx.Interaction.FollowupAsync(Strings.HighlowLost(ctx.Guild.Id, currentNumber, nextNumber, emote));
        }

        /// <summary>
        ///     Plays a slot machine game with a specified bet amount.
        /// </summary>
        /// <param name="bet">The amount to bet on the slot machine.</param>
        [SlashCommand("slot", "Plays the slot machine")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Slot([Summary("bet", "The amount to bet")] long bet = 10)
        {
            await DeferAsync();

            if (bet < 1)
            {
                await ErrorAsync(Strings.SlotMinimumBet(ctx.Guild.Id, await Service.GetCurrencyEmote(ctx.Guild.Id)));
                return;
            }

            if (!await TryTakeBetAsync(bet, "slots"))
                return;

            var result = new string[3];

            for (var i = 0; i < 3; i++)
            {
                result[i] = CurrencyRng.Pick(CurrencyGameLogic.SlotSymbols);
            }

            var winnings = CurrencyGameLogic.CalculateSlotWinnings(result, bet);

            if (winnings > 0)
                winnings = await WinAsync(bet, (double)winnings / bet, "slots");

            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.SlotTitle(ctx.Guild.Id))
                .WithDescription(Strings.SlotResult(ctx.Guild.Id, result[0], result[1], result[2]))
                .AddField(Strings.SlotBet(ctx.Guild.Id), $"{bet} {emote}", true)
                .AddField(Strings.SlotWinnings(ctx.Guild.Id), $"{winnings} {emote}", true)
                .AddField(Strings.SlotNetProfit(ctx.Guild.Id), $"{winnings - bet} {emote}", true);

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Play a game of roulette with a specified bet amount and type.
        /// </summary>
        /// <param name="betAmount">The amount to bet.</param>
        /// <param name="betType">The type of bet: red, black, even, odd, or a number from 0 to 36.</param>
        [SlashCommand("roulette", "Plays roulette on a colour, parity or a number")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Roulette([Summary("bet", "The amount to bet")] long betAmount,
            [Summary("type", "red, black, even, odd, or a number from 0 to 36")]
            string betType)
        {
            await DeferAsync();
            var isNumberBet = int.TryParse(betType, out var numberBet);

            if (isNumberBet && numberBet is < 0 or > 36)
            {
                await ErrorAsync(Strings.RouletteInvalidBetType(ctx.Guild.Id));
                return;
            }

            if (!isNumberBet && betType.ToLower() is not ("red" or "black" or "even" or "odd"))
            {
                await ErrorAsync(Strings.RouletteInvalidBetType(ctx.Guild.Id));
                return;
            }

            if (!await TryTakeBetAsync(betAmount, "roulette"))
                return;

            var result = CurrencyRng.Next(0, 37);
            var color = result == 0 ? "green" : result % 2 == 0 ? "black" : "red";

            bool won;
            int multiplier;

            if (isNumberBet)
            {
                won = result == numberBet;
                multiplier = 35;
            }
            else
            {
                won = betType.ToLower() switch
                {
                    "red" or "black" => betType.Equals(color, StringComparison.OrdinalIgnoreCase),
                    "even" => result != 0 && result % 2 == 0,
                    _ => result % 2 != 0
                };

                multiplier = 1;
            }

            var returned = won ? await WinAsync(betAmount, multiplier + 1, "roulette") : 0;
            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RouletteTitle(ctx.Guild.Id))
                .WithDescription(Strings.RouletteResult(ctx.Guild.Id, result, color))
                .AddField(Strings.RouletteBet(ctx.Guild.Id),
                    Strings.RouletteBetDetails(ctx.Guild.Id, betAmount, emote, betType), true)
                .AddField(Strings.RouletteOutcome(ctx.Guild.Id),
                    won ? Strings.RouletteWon(ctx.Guild.Id) : Strings.RouletteLost(ctx.Guild.Id), true)
                .AddField(Strings.RouletteProfit(ctx.Guild.Id), $"{returned - betAmount} {emote}", true);

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Play Rock Paper Scissors Lizard Spock against the bot, with or without betting.
        /// </summary>
        /// <param name="choice">Your choice.</param>
        /// <param name="betAmount">The amount to bet, 0 to play for fun.</param>
        [SlashCommand("rps", "Plays rock paper scissors lizard spock against the bot")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Rps([Summary("choice", "Your choice")] RpsChoice choice,
            [Summary("bet", "The amount to bet, 0 to play for fun")]
            long betAmount = 0)
        {
            await DeferAsync();
            var validChoices = new[]
            {
                "rock", "paper", "scissors", "lizard", "spock"
            };
            var playerChoice = choice.ToString().ToLowerInvariant();

            if (betAmount != 0 && !await TryTakeBetAsync(betAmount, "rps"))
                return;

            var botChoice = CurrencyRng.Pick(validChoices);

            var (result, description) = DetermineWinner(playerChoice, botChoice);

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RpsEmbedTitle(ctx.Guild.Id))
                .WithDescription(Strings.RpsEmbedDescription(ctx.Guild.Id, playerChoice, botChoice))
                .AddField(Strings.RpsEmbedResult(ctx.Guild.Id), result.ToUpperInvariant(), true)
                .AddField(Strings.RpsEmbedExplanation(ctx.Guild.Id), description, true);

            if (betAmount != 0)
            {
                var returned = result switch
                {
                    "win" => await WinAsync(betAmount, 2.0, "rps"),
                    "tie" => betAmount,
                    _ => 0L
                };

                if (result == "tie")
                    await PushAsync(betAmount, "rps");

                eb.AddField(Strings.RpsEmbedProfit(ctx.Guild.Id),
                    $"{returned - betAmount} {await Service.GetCurrencyEmote(ctx.Guild.Id)}", true);
            }

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Play a game of War against the bot.
        /// </summary>
        /// <param name="betAmount">The amount to bet.</param>
        [SlashCommand("war", "Plays a game of war against the bot")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task War([Summary("bet", "The amount to bet")] long betAmount)
        {
            await DeferAsync();

            if (!await TryTakeBetAsync(betAmount, "war"))
                return;

            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);
            var playerCard = CurrencyGameLogic.GenerateCard();
            var opponentCard = CurrencyGameLogic.GenerateCard();

            var eb = new EmbedBuilder()
                .WithTitle(Strings.WarTitle(ctx.Guild.Id))
                .WithColor(Color.Blue)
                .AddField(Strings.WarYourCard(ctx.Guild.Id), $"{playerCard.rank}{playerCard.suit}", true)
                .AddField(Strings.WarOpponentCard(ctx.Guild.Id), $"{opponentCard.rank}{opponentCard.suit}", true);

            if (playerCard.value > opponentCard.value)
            {
                await WinAsync(betAmount, 2.0, "war");
                eb.WithDescription(Strings.WarResultWin(ctx.Guild.Id, $"{playerCard.rank}{playerCard.suit}",
                        $"{opponentCard.rank}{opponentCard.suit}"))
                    .WithColor(Color.Green);
            }
            else if (playerCard.value < opponentCard.value)
            {
                eb.WithDescription(Strings.WarResultLose(ctx.Guild.Id, $"{playerCard.rank}{playerCard.suit}",
                        $"{opponentCard.rank}{opponentCard.suit}"))
                    .WithColor(Color.Red);
            }
            else
            {
                if (!await Service.TryDebitAsync(ctx.User.Id, betAmount,
                        Strings.BetPlacedTransaction(ctx.Guild.Id, "war"),
                        CurrencyCategory.GameBet, ctx.Guild.Id, "war"))
                {
                    await PushAsync(betAmount, "war");
                    eb.WithDescription(Strings.WarResultTie(ctx.Guild.Id, $"{playerCard.rank}{playerCard.suit}"))
                        .WithColor(Color.Gold);
                }
                else
                {
                    var warStake = betAmount * 2;

                    eb.WithTitle(Strings.WarBattleStart(ctx.Guild.Id))
                        .WithDescription(Strings.WarBattleDescription(ctx.Guild.Id))
                        .WithColor(Color.Orange);

                    var warPlayerCard = CurrencyGameLogic.GenerateCard();
                    var warOpponentCard = CurrencyGameLogic.GenerateCard();

                    eb.AddField(Strings.WarBattleYourCard(ctx.Guild.Id), $"{warPlayerCard.rank}{warPlayerCard.suit}",
                            true)
                        .AddField(Strings.WarBattleOpponentCard(ctx.Guild.Id),
                            $"{warOpponentCard.rank}{warOpponentCard.suit}", true);

                    if (warPlayerCard.value > warOpponentCard.value)
                    {
                        await WinAsync(warStake, 2.0, "war");
                        eb.AddField(Strings.WarBattleResult(ctx.Guild.Id),
                                Strings.WarBattleWin(ctx.Guild.Id, warStake, emote))
                            .WithColor(Color.Green);
                    }
                    else
                    {
                        eb.AddField(Strings.WarBattleResult(ctx.Guild.Id),
                                Strings.WarBattleLose(ctx.Guild.Id, warStake, emote))
                            .WithColor(Color.Red);
                    }
                }
            }

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Play a game of Craps.
        /// </summary>
        /// <param name="betAmount">The amount to bet.</param>
        /// <param name="betType">The type of bet: pass, don't pass, field, or any.</param>
        [SlashCommand("craps", "Plays a game of craps")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Craps([Summary("bet", "The amount to bet")] long betAmount,
            [Summary("type", "The type of bet")] CrapsBet betType = CrapsBet.Pass)
        {
            await DeferAsync();
            var betKey = betType.ToString().ToLowerInvariant();

            if (!await TryTakeBetAsync(betAmount, "craps"))
                return;

            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);
            var die1 = CurrencyRng.Next(1, 7);
            var die2 = CurrencyRng.Next(1, 7);
            var total = die1 + die2;

            var eb = new EmbedBuilder()
                .WithTitle(Strings.CrapsTitle(ctx.Guild.Id))
                .WithColor(Color.Blue)
                .AddField(Strings.CrapsRolled(ctx.Guild.Id, die1, die2, total), "");

            var won = false;
            var push = false;
            var multiplier = 1.0;

            switch (betType)
            {
                case CrapsBet.Pass:
                    switch (total)
                    {
                        case 7:
                        case 11:
                            won = true;
                            eb.AddField(Strings.CrapsResultField(ctx.Guild.Id),
                                Strings.CrapsNatural(ctx.Guild.Id, total));
                            break;
                        case 2:
                        case 3:
                        case 12:
                            won = false;
                            eb.AddField(Strings.CrapsResultField(ctx.Guild.Id),
                                Strings.CrapsCraps(ctx.Guild.Id, total));
                            break;
                        default:
                        {
                            eb.AddField(Strings.CrapsPointField(ctx.Guild.Id),
                                Strings.CrapsPointEstablished(ctx.Guild.Id, total));
                            var pointRoll = CurrencyGameLogic.RollForPoint(total);
                            won = pointRoll.won;
                            eb.AddField(Strings.CrapsPointRollField(ctx.Guild.Id), pointRoll.description);
                            break;
                        }
                    }

                    break;

                case CrapsBet.DontPass:
                    switch (total)
                    {
                        case 2:
                        case 3:
                            won = true;
                            break;
                        case 7:
                        case 11:
                            won = false;
                            break;
                        case 12:
                            push = true;
                            break;
                        default:
                        {
                            var pointRoll = CurrencyGameLogic.RollForPoint(total);
                            won = !pointRoll.won;
                            eb.AddField(Strings.CrapsPointRollField(ctx.Guild.Id), pointRoll.description);
                            break;
                        }
                    }

                    break;

                case CrapsBet.Field:
                    won = total is 2 or 3 or 4 or 9 or 10 or 11 or 12;
                    if (total is 2 or 12) multiplier = 2.0;
                    break;

                case CrapsBet.Any:
                    won = total == 7;
                    if (won) multiplier = 4.0;
                    break;
            }

            long returned;

            if (push)
            {
                await PushAsync(betAmount, "craps");
                returned = betAmount;
            }
            else
            {
                returned = won ? await WinAsync(betAmount, 1 + multiplier, "craps") : 0;
            }

            var profit = returned - betAmount;
            var shown = Math.Abs(profit);

            var resultMessage = betType switch
            {
                _ when push => Strings.CrapsPush(ctx.Guild.Id, betAmount, emote),
                CrapsBet.Pass when won => Strings.CrapsPassWin(ctx.Guild.Id, shown, emote),
                CrapsBet.Pass => Strings.CrapsPassLose(ctx.Guild.Id, shown, emote),
                CrapsBet.DontPass when won => Strings.CrapsDontpassWin(ctx.Guild.Id, shown, emote),
                CrapsBet.DontPass => Strings.CrapsDontpassLose(ctx.Guild.Id, shown, emote),
                CrapsBet.Field when won => Strings.CrapsFieldWin(ctx.Guild.Id, shown, emote),
                CrapsBet.Field => Strings.CrapsFieldLose(ctx.Guild.Id, shown, emote),
                CrapsBet.Any when won => Strings.CrapsAnyWin(ctx.Guild.Id, shown, emote),
                _ => Strings.CrapsAnyLose(ctx.Guild.Id, shown, emote)
            };

            eb.AddField(Strings.CrapsOutcomeField(ctx.Guild.Id), resultMessage)
                .WithColor(push ? Color.Gold : won ? Color.Green : Color.Red);

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Starts a new game of Blackjack or joins an existing one. Hit and stand are offered as buttons.
        /// </summary>
        /// <param name="amount">The bet amount for the player.</param>
        [SlashCommand("blackjack", "Starts or joins a game of blackjack")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Blackjack([Summary("bet", "The amount to bet")] long amount)
        {
            await DeferAsync();

            if (!await TryTakeBetAsync(amount, "blackjack"))
                return;

            try
            {
                var game = BlackjackService.StartOrJoinGame(ctx.User, amount);
                var embed = game.CreateGameEmbed(Strings.BlackjackJoined(ctx.Guild.Id, ctx.User.Username),
                    ctx.Guild.Id, Strings);
                await ctx.Interaction.FollowupAsync(embeds: embed, components: BuildBlackjackButtons());
            }
            catch (InvalidOperationException ex)
            {
                await PushAsync(amount, "blackjack");

                switch (ex.Message)
                {
                    case "blackjack_game_full":
                        await ReplyErrorAsync(Strings.BlackjackGameFull(ctx.Guild.Id));
                        break;
                    case "blackjack_already_in_game":
                        await ReplyErrorAsync(Strings.AlreadyInGame(ctx.Guild.Id));
                        break;
                    case "no_ongoing_game":
                        await ReplyErrorAsync(Strings.NoOngoingGame(ctx.Guild.Id));
                        break;
                }
            }
        }

        /// <summary>
        ///     Handles the blackjack hit button, drawing a new card for the invoking player.
        /// </summary>
        /// <param name="userId">The id of the player the game belongs to.</param>
        [ComponentInteraction("currency_bj_hit:*", true)]
        public async Task BlackjackHit(string userId)
        {
            if (!ulong.TryParse(userId, out var ownerId) || ownerId != ctx.User.Id)
            {
                await ctx.Interaction.RespondAsync(Strings.BlackjackNotYourGame(ctx.Guild.Id), ephemeral: true);
                return;
            }

            try
            {
                var game = BlackjackService.GetGame(ctx.User);
                game.HitPlayer(ctx.User);
                var embed = game.CreateGameEmbed(Strings.BlackjackHit(ctx.Guild.Id, ctx.User.Username), ctx.Guild.Id,
                    Strings);

                if (BlackjackService.BlackjackGame.CalculateHandTotal(game.PlayerHands[ctx.User]) > 21)
                {
                    await EndGame(game, false, Strings.BlackjackBust(ctx.Guild.Id, ctx.User.Username));
                }
                else if (BlackjackService.BlackjackGame.CalculateHandTotal(game.PlayerHands[ctx.User]) == 21)
                {
                    await StandInternal();
                }
                else
                {
                    await UpdateGameMessage(embed, BuildBlackjackButtons());
                }
            }
            catch (InvalidOperationException ex)
            {
                switch (ex.Message)
                {
                    case "no_ongoing_game":
                        await ReplyErrorAsync(Strings.NoOngoingGame(ctx.Guild.Id));
                        break;
                    default:
                        await ReplyErrorAsync(Strings.BlackjackError(ctx.Guild.Id, ex.Message));
                        break;
                }
            }
        }

        /// <summary>
        ///     Handles the blackjack stand button, ending the invoking player's turn and running the dealer.
        /// </summary>
        /// <param name="userId">The id of the player the game belongs to.</param>
        [ComponentInteraction("currency_bj_stand:*", true)]
        public async Task BlackjackStand(string userId)
        {
            if (!ulong.TryParse(userId, out var ownerId) || ownerId != ctx.User.Id)
            {
                await ctx.Interaction.RespondAsync(Strings.BlackjackNotYourGame(ctx.Guild.Id), ephemeral: true);
                return;
            }

            await StandInternal();
        }

        /// <summary>
        ///     Runs the dealer's turn for the invoking player and settles the game.
        /// </summary>
        private async Task StandInternal()
        {
            try
            {
                var embed = await blackjackService.HandleStandAsync(ctx.User, ctx.Guild.Id,
                    (userId, payout) => Service.AddUserBalanceAsync(userId, payout, ctx.Guild.Id),
                    (userId, payout, description) =>
                        Service.AddTransactionAsync(userId, payout, description, ctx.Guild.Id,
                            CurrencyCategory.GamePayout, "blackjack"),
                    await Service.GetCurrencyEmote(ctx.Guild.Id));
                await UpdateGameMessage(embed, new ComponentBuilder().Build());
            }
            catch (InvalidOperationException ex)
            {
                switch (ex.Message)
                {
                    case "no_ongoing_game":
                        await ReplyErrorAsync(Strings.NoOngoingGame(ctx.Guild.Id));
                        break;
                    default:
                        await ReplyErrorAsync(Strings.BlackjackError(ctx.Guild.Id, ex.Message));
                        break;
                }
            }
        }

        /// <summary>
        ///     Ends the game and updates the player's balance and transactions.
        /// </summary>
        /// <param name="game">The current game instance.</param>
        /// <param name="playerWon">Indicates whether the player won or lost.</param>
        /// <param name="message">The message to display in the embed.</param>
        private async Task EndGame(BlackjackService.BlackjackGame game, bool playerWon, string message)
        {
            var stake = game.Bets[ctx.User];

            if (playerWon)
                await WinAsync(stake, 2.0, "blackjack");

            BlackjackService.EndGame(ctx.User);

            var embed = game.CreateGameEmbed(message, ctx.Guild.Id, Strings);
            await UpdateGameMessage(embed, new ComponentBuilder().Build());
        }

        /// <summary>
        ///     Builds the hit and stand buttons bound to the invoking user.
        /// </summary>
        private MessageComponent BuildBlackjackButtons()
        {
            return new ComponentBuilder()
                .WithButton(Strings.BlackjackHitButton(ctx.Guild.Id), $"currency_bj_hit:{ctx.User.Id}")
                .WithButton(Strings.BlackjackStandButton(ctx.Guild.Id), $"currency_bj_stand:{ctx.User.Id}",
                    ButtonStyle.Secondary)
                .Build();
        }

        /// <summary>
        ///     Edits the blackjack message in place when responding to a button, or sends a follow-up otherwise.
        /// </summary>
        private async Task UpdateGameMessage(Embed[] embeds, MessageComponent components)
        {
            if (ctx.Interaction is SocketMessageComponent component && !component.HasResponded)
            {
                await component.UpdateAsync(x =>
                {
                    x.Embeds = embeds;
                    x.Components = components;
                });
                return;
            }

            await ctx.Interaction.FollowupAsync(embeds: embeds, components: components);
        }

        /// <summary>
        ///     Play a game of Baccarat.
        /// </summary>
        /// <param name="betAmount">The amount to bet.</param>
        /// <param name="betType">The type of bet: player, banker, or tie.</param>
        [SlashCommand("baccarat", "Plays a game of baccarat")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Baccarat([Summary("bet", "The amount to bet")] long betAmount,
            [Summary("type", "The hand to bet on")]
            BaccaratBet betType = BaccaratBet.Player)
        {
            await DeferAsync();

            if (!await TryTakeBetAsync(betAmount, "baccarat"))
                return;

            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

            var playerHand = new List<(string rank, string suit, int value)>
            {
                CurrencyGameLogic.GenerateCard(), CurrencyGameLogic.GenerateCard()
            };
            var bankerHand = new List<(string rank, string suit, int value)>
            {
                CurrencyGameLogic.GenerateCard(), CurrencyGameLogic.GenerateCard()
            };

            var playerTotal = CurrencyGameLogic.CalculateBaccaratTotal(playerHand);
            var bankerTotal = CurrencyGameLogic.CalculateBaccaratTotal(bankerHand);

            var playerNatural = playerTotal >= 8;
            var bankerNatural = bankerTotal >= 8;

            if (!playerNatural && !bankerNatural)
            {
                if (playerTotal <= 5)
                {
                    playerHand.Add(CurrencyGameLogic.GenerateCard());
                    playerTotal = CurrencyGameLogic.CalculateBaccaratTotal(playerHand);
                }

                switch (bankerTotal)
                {
                    case <= 5 when playerHand.Count == 2:
                        bankerHand.Add(CurrencyGameLogic.GenerateCard());
                        bankerTotal = CurrencyGameLogic.CalculateBaccaratTotal(bankerHand);
                        break;
                    case <= 2:
                        bankerHand.Add(CurrencyGameLogic.GenerateCard());
                        bankerTotal = CurrencyGameLogic.CalculateBaccaratTotal(bankerHand);
                        break;
                }
            }

            var eb = new EmbedBuilder()
                .WithTitle(Strings.BaccaratTitle(ctx.Guild.Id))
                .WithColor(Color.Blue)
                .AddField(Strings.BaccaratPlayerHand(ctx.Guild.Id),
                    string.Join(" ", playerHand.Select(c => $"{c.rank}{c.suit}")) + $" (Total: {playerTotal})")
                .AddField(Strings.BaccaratBankerHand(ctx.Guild.Id),
                    string.Join(" ", bankerHand.Select(c => $"{c.rank}{c.suit}")) + $" (Total: {bankerTotal})");

            var won = false;
            var multiplier = 1.0;

            if (playerTotal > bankerTotal)
            {
                eb.AddField(Strings.BaccaratResultField(ctx.Guild.Id),
                    Strings.BaccaratPlayerWins(ctx.Guild.Id, playerTotal));
                if (betType == BaccaratBet.Player)
                {
                    won = true;
                    multiplier = 1.95;
                }
            }
            else if (bankerTotal > playerTotal)
            {
                eb.AddField(Strings.BaccaratResultField(ctx.Guild.Id),
                    Strings.BaccaratBankerWins(ctx.Guild.Id, bankerTotal));
                if (betType == BaccaratBet.Banker)
                {
                    won = true;
                    multiplier = 1.95;
                }
            }
            else
            {
                eb.AddField(Strings.BaccaratResultField(ctx.Guild.Id), Strings.BaccaratTie(ctx.Guild.Id, playerTotal));
                if (betType == BaccaratBet.Tie)
                {
                    won = true;
                    multiplier = 8.0;
                }
            }

            var returned = won ? await WinAsync(betAmount, multiplier, "baccarat") : 0;
            var profit = Math.Abs(returned - betAmount);

            var resultMessage = betType switch
            {
                BaccaratBet.Player when won => Strings.BaccaratPlayerBetWin(ctx.Guild.Id, profit, emote),
                BaccaratBet.Banker when won => Strings.BaccaratBankerBetWin(ctx.Guild.Id, profit, emote),
                BaccaratBet.Tie when won => Strings.BaccaratTieBetWin(ctx.Guild.Id, profit, emote),
                _ => Strings.BaccaratBetLose(ctx.Guild.Id, profit, emote)
            };

            if (playerNatural || bankerNatural)
            {
                eb.AddField(Strings.BaccaratSpecialField(ctx.Guild.Id),
                    Strings.BaccaratNatural(ctx.Guild.Id, Math.Max(playerTotal, bankerTotal)));
            }

            eb.AddField(Strings.BaccaratOutcomeField(ctx.Guild.Id), resultMessage)
                .WithColor(won ? Color.Green : Color.Red);

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Play a Crash game where you bet on a multiplier that can crash at any time.
        /// </summary>
        /// <param name="betAmount">The amount to bet.</param>
        /// <param name="targetMultiplier">The multiplier to cash out at (1.1x to 10.0x).</param>
        [SlashCommand("crash", "Bets on a multiplier that can crash at any moment")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Crash([Summary("bet", "The amount to bet")] long betAmount,
            [Summary("target", "The multiplier to cash out at, 1.1 to 10.0")]
            double targetMultiplier = 2.0)
        {
            await DeferAsync();

            if (targetMultiplier is < 1.1 or > 10.0)
            {
                await ErrorAsync(Strings.CrashInvalidMultiplier(ctx.Guild.Id));
                return;
            }

            if (!await TryTakeBetAsync(betAmount, "crash"))
                return;

            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);
            var crashPoint = CurrencyGameLogic.GenerateCrashPoint();
            var won = targetMultiplier <= crashPoint;

            var eb = new EmbedBuilder()
                .WithTitle(Strings.CrashTitle(ctx.Guild.Id))
                .WithColor(won ? Color.Green : Color.Red)
                .AddField(Strings.CrashTargetMultiplier(ctx.Guild.Id, $"{targetMultiplier:F2}x"), "", true)
                .AddField(Strings.CrashActualMultiplier(ctx.Guild.Id, $"{crashPoint:F2}x"), "", true);

            if (won)
            {
                var winAmount = await WinAsync(betAmount, targetMultiplier, "crash");
                eb.WithDescription(Strings.CrashWin(ctx.Guild.Id, targetMultiplier, winAmount - betAmount, emote));
            }
            else
            {
                eb.WithDescription(Strings.CrashLose(ctx.Guild.Id, crashPoint, betAmount, emote));
            }

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Play Keno by selecting numbers and seeing how many match the drawn numbers.
        /// </summary>
        /// <param name="betAmount">The amount to bet.</param>
        /// <param name="numbers">Up to 10 numbers between 1 and 80, separated by spaces.</param>
        [SlashCommand("keno", "Picks up to 10 numbers and matches them against the draw")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Keno([Summary("bet", "The amount to bet")] long betAmount,
            [Summary("numbers", "Up to 10 numbers from 1 to 80, separated by spaces")]
            string numbers)
        {
            await DeferAsync();
            var selectedNumbers = CurrencyGameLogic.ParseKenoNumbers(numbers);
            if (selectedNumbers.Count is 0 or > 10)
            {
                await ErrorAsync(Strings.KenoInvalidNumbers(ctx.Guild.Id));
                return;
            }

            if (selectedNumbers.Any(n => n is < 1 or > 80))
            {
                await ErrorAsync(Strings.KenoNumbersOutOfRange(ctx.Guild.Id));
                return;
            }

            if (!await TryTakeBetAsync(betAmount, "keno"))
                return;

            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);
            var drawnNumbers = CurrencyGameLogic.GenerateKenoNumbers(20);
            var matches = selectedNumbers.Intersect(drawnNumbers).Count();
            var multiplier = CurrencyGameLogic.CalculateKenoMultiplier(selectedNumbers.Count, matches);

            var eb = new EmbedBuilder()
                .WithTitle(Strings.KenoTitle(ctx.Guild.Id))
                .WithColor(multiplier > 0 ? Color.Green : Color.Red)
                .AddField(Strings.KenoYourNumbers(ctx.Guild.Id, string.Join(", ", selectedNumbers.OrderBy(x => x))),
                    "")
                .AddField(Strings.KenoDrawnNumbers(ctx.Guild.Id, string.Join(", ", drawnNumbers.OrderBy(x => x))), "")
                .AddField(Strings.KenoMatches(ctx.Guild.Id, matches.ToString()), "", true);

            if (multiplier > 0)
            {
                var winAmount = await WinAsync(betAmount, multiplier, "keno");
                eb.WithDescription(Strings.KenoWin(ctx.Guild.Id, matches, winAmount - betAmount, emote));
            }
            else
            {
                eb.WithDescription(Strings.KenoLose(ctx.Guild.Id, matches, betAmount, emote));
            }

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Play Plinko by dropping a ball down pegs for random multipliers.
        /// </summary>
        /// <param name="betAmount">The amount to bet.</param>
        /// <param name="rows">Number of peg rows (5 to 10).</param>
        [SlashCommand("plinko", "Drops a ball down the pegs for a random multiplier")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Plinko([Summary("bet", "The amount to bet")] long betAmount,
            [Summary("rows", "Number of peg rows, 5 to 10")]
            int rows = 8)
        {
            await DeferAsync();

            if (rows is < 5 or > 10)
            {
                await ErrorAsync(Strings.PlinkoInvalidRows(ctx.Guild.Id));
                return;
            }

            if (!await TryTakeBetAsync(betAmount, "plinko"))
                return;

            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);
            var path = CurrencyGameLogic.SimulatePlinkoBall(rows);
            var slot = path.Last();
            var multiplier = CurrencyGameLogic.GetPlinkoMultiplier(rows, slot);

            using var bitmap = new SKBitmap(400, 500);
            using var canvas = new SKCanvas(bitmap);
            CurrencyGameLogic.DrawPlinkoBoard(canvas, rows, path);

            using var stream = new MemoryStream();
            bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
            stream.Seek(0, SeekOrigin.Begin);

            var returned = multiplier > 0 ? await WinAsync(betAmount, multiplier, "plinko") : 0;
            var profit = returned - betAmount;

            var eb = new EmbedBuilder()
                .WithTitle(Strings.PlinkoTitle(ctx.Guild.Id))
                .WithColor(profit > 0 ? Color.Green : Color.Red)
                .WithDescription(profit > 0
                    ? Strings.PlinkoWin(ctx.Guild.Id, multiplier, Math.Abs(profit), emote)
                    : Strings.PlinkoLose(ctx.Guild.Id, multiplier, Math.Abs(profit), emote))
                .AddField(Strings.PlinkoMultiplier(ctx.Guild.Id, $"{multiplier:F2}x"), "", true)
                .AddField(Strings.PlinkoSlot(ctx.Guild.Id, slot.ToString()), "", true)
                .WithImageUrl("attachment://plinko.png");

            await ctx.Interaction.FollowupWithFileAsync(stream, "plinko.png", embed: eb.Build());
        }

        /// <summary>
        ///     Play a simplified Minesweeper game.
        /// </summary>
        /// <param name="betAmount">The amount to bet.</param>
        /// <param name="size">Grid size: small, medium, or large.</param>
        [SlashCommand("minesweeper", "Plays a simplified game of minesweeper")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Minesweeper([Summary("bet", "The amount to bet")] long betAmount,
            [Summary("size", "The grid size")] MinesweeperSize size = MinesweeperSize.Small)
        {
            await DeferAsync();
            var gridSizes = new Dictionary<MinesweeperSize, (int size, int mines, double safeChance)>
            {
                {
                    MinesweeperSize.Small, (3, 2, 0.7)
                },
                {
                    MinesweeperSize.Medium, (5, 6, 0.6)
                },
                {
                    MinesweeperSize.Large, (7, 12, 0.5)
                }
            };

            var grid = gridSizes[size];

            if (!await TryTakeBetAsync(betAmount, "minesweeper"))
                return;

            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);
            var survived = CurrencyRng.NextDouble() < grid.safeChance;

            var multiplier = size switch
            {
                MinesweeperSize.Small => 1.5,
                MinesweeperSize.Medium => 2.5,
                MinesweeperSize.Large => 4.0,
                _ => 1.5
            };

            var eb = new EmbedBuilder()
                .WithTitle(Strings.MinesweeperTitle(ctx.Guild.Id))
                .WithDescription(Strings.MinesweeperDescription(ctx.Guild.Id))
                .WithColor(Color.Blue)
                .AddField(Strings.MinesweeperGrid(ctx.Guild.Id, grid.size, grid.size), $"{grid.size}x{grid.size}",
                    true)
                .AddField(Strings.MinesweeperMines(ctx.Guild.Id, grid.mines), grid.mines.ToString(), true);

            if (survived)
            {
                var winAmount = await WinAsync(betAmount, multiplier, "minesweeper");

                eb.WithDescription(Strings.MinesweeperAllSafe(ctx.Guild.Id, winAmount - betAmount, emote))
                    .WithColor(Color.Green)
                    .AddField(Strings.MinesweeperMultiplier(ctx.Guild.Id, multiplier), $"x{multiplier:F1}", true);
            }
            else
            {
                eb.WithDescription(Strings.MinesweeperHitMine(ctx.Guild.Id, betAmount, emote))
                    .WithColor(Color.Red);
            }

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Play a Bingo game with number calling.
        /// </summary>
        /// <param name="betAmount">The amount to bet.</param>
        /// <param name="cardType">The type of bingo card: small or large.</param>
        [SlashCommand("bingo", "Plays a round of bingo")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Bingo([Summary("bet", "The amount to bet")] long betAmount,
            [Summary("card", "The card size")] BingoCardType cardType = BingoCardType.Small)
        {
            await DeferAsync();
            var cardConfigs =
                new Dictionary<BingoCardType, (int size, int numbers, double winChance, double multiplier)>
                {
                    {
                        BingoCardType.Small, (3, 9, 0.4, 2.0)
                    },
                    {
                        BingoCardType.Large, (5, 25, 0.25, 3.5)
                    }
                };

            if (!await TryTakeBetAsync(betAmount, "bingo"))
                return;

            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);
            var config = cardConfigs[cardType];
            var playerCard = CurrencyGameLogic.GenerateBingoCard(config.size);
            var calledNumbers = CurrencyGameLogic.CallBingoNumbers(config.numbers);
            var won = CurrencyGameLogic.CheckBingoWin(playerCard, calledNumbers,
                CurrencyRng.NextDouble() < config.winChance);

            var eb = new EmbedBuilder()
                .WithTitle(Strings.BingoTitle(ctx.Guild.Id))
                .WithColor(won ? Color.Green : Color.Red)
                .AddField(Strings.BingoCardType(ctx.Guild.Id, cardType.ToString().ToUpperInvariant()), "", true)
                .AddField(
                    Strings.BingoCalledNumbers(ctx.Guild.Id,
                        string.Join(", ", calledNumbers.Take(10)) + (calledNumbers.Count > 10 ? "..." : "")), "");

            var cardDisplay = CurrencyGameLogic.FormatBingoCard(playerCard, calledNumbers);
            eb.AddField(Strings.BingoYourCard(ctx.Guild.Id, cardDisplay), "");

            if (won)
            {
                var winAmount = await WinAsync(betAmount, config.multiplier, "bingo");
                eb.WithDescription(Strings.BingoWin(ctx.Guild.Id, winAmount - betAmount, emote));
            }
            else
            {
                eb.WithDescription(Strings.BingoLose(ctx.Guild.Id, betAmount, emote));
            }

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Start a duck race similar to horse racing.
        /// </summary>
        /// <param name="betAmount">The amount to bet on the duck race.</param>
        [SlashCommand("duckrace", "Bets on a random duck in a five duck race")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task DuckRace([Summary("bet", "The amount to bet")] long betAmount)
        {
            await DeferAsync();

            if (!await TryTakeBetAsync(betAmount, "duckrace"))
                return;

            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);
            var ducks = CurrencyGameLogic.GenerateDucks(5);
            var raceResults = CurrencyGameLogic.SimulateDuckRace(ducks);

            var userDuck = CurrencyRng.Next(ducks.Count);
            var userPosition = raceResults.FindIndex(r => r.duckIndex == userDuck) + 1;

            var multiplier = userPosition switch
            {
                1 => 4.0,
                2 => 2.0,
                3 => 1.2,
                _ => 0.0
            };

            var eb = new EmbedBuilder()
                .WithTitle(Strings.DuckRaceTitle(ctx.Guild.Id))
                .WithColor(multiplier > 0 ? Color.Green : Color.Red)
                .AddField(Strings.DuckRaceYourDuck(ctx.Guild.Id, $"{ducks[userDuck]} (#{userDuck + 1})"), "", true)
                .AddField(Strings.DuckRacePosition(ctx.Guild.Id, $"{userPosition}/5"), "", true);

            var resultsText = "";
            for (var i = 0; i < raceResults.Count; i++)
            {
                var duck = ducks[raceResults[i].duckIndex];
                var line = $"{i + 1}. {duck} Duck #{raceResults[i].duckIndex + 1}";
                resultsText += raceResults[i].duckIndex == userDuck ? $"**{line}**\n" : $"{line}\n";
            }

            eb.AddField(Strings.DuckRaceResults(ctx.Guild.Id, resultsText), "");

            if (multiplier > 0)
            {
                var winAmount = await WinAsync(betAmount, multiplier, "duckrace");
                eb.WithDescription(Strings.DuckRaceWin(ctx.Guild.Id, winAmount - betAmount, emote));
            }
            else
            {
                eb.WithDescription(Strings.DuckRaceLose(ctx.Guild.Id, betAmount, emote));
            }

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Play an enhanced Wheel of Fortune with customizable segments.
        /// </summary>
        /// <param name="betAmount">The amount to bet.</param>
        /// <param name="wheelType">The type of wheel: classic, risky, or balanced.</param>
        [SlashCommand("wheel", "Spins the wheel of fortune for a multiplier")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task WheelFortune([Summary("bet", "The amount to bet")] long betAmount,
            [Summary("type", "The wheel layout")] WheelType wheelType = WheelType.Classic)
        {
            await DeferAsync();
            var wheelKey = wheelType.ToString().ToLowerInvariant();
            var wheelConfigs = CurrencyGameLogic.GetWheelConfigurations();

            if (!await TryTakeBetAsync(betAmount, "wheelfortune"))
                return;

            var config = wheelConfigs[wheelKey];
            var winningIndex = CurrencyGameLogic.GenerateWeightedRandomSegment(config.segments.Length, config.weights);
            var segment = config.segments[winningIndex];

            using var bitmap = new SKBitmap(500, 500);
            using var canvas = new SKCanvas(bitmap);
            CurrencyGameLogic.DrawWheelFortune(canvas, config.segments, winningIndex);

            using var stream = new MemoryStream();
            bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
            stream.Seek(0, SeekOrigin.Begin);

            var delta = CurrencyGameLogic.ComputeWheelBalanceChange(segment, betAmount);
            var payout = Math.Max(0, betAmount + delta);

            if (payout > 0)
                await PayoutAsync(payout, "wheelfortune");

            var balanceChange = payout - betAmount;

            var eb = new EmbedBuilder()
                .WithTitle(Strings.WheelFortuneTitle(ctx.Guild.Id))
                .WithColor(balanceChange >= 0 ? Color.Green : Color.Red)
                .WithDescription(Strings.WheelFortuneResult(ctx.Guild.Id, segment))
                .AddField(Strings.WheelFortuneType(ctx.Guild.Id, wheelKey.ToUpperInvariant()), "", true)
                .WithImageUrl("attachment://wheel.png");

            await ctx.Interaction.FollowupWithFileAsync(stream, "wheel.png", embed: eb.Build());
        }

        /// <summary>
        ///     Allows the user to spin the wheel for a chance to win or lose credits.
        /// </summary>
        /// <param name="betAmount">The amount of credits the user wants to bet.</param>
        [SlashCommand("spin", "Spins the classic wheel for a chance to win or lose credits")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task SpinWheel([Summary("bet", "The amount to bet")] long betAmount = 10)
        {
            await DeferAsync();

            if (!await TryTakeBetAsync(betAmount, "spinwheel"))
                return;

            string[] segments =
            [
                "-$10", "-10%", "+$10", "+30%", "+$30", "-5%"
            ];
            int[] weights =
            [
                2, 2, 1, 1, 1, 2
            ];

            var winningSegment = CurrencyGameLogic.GenerateWeightedRandomSegment(segments.Length, weights);

            using var bitmap = new SKBitmap(500, 500);
            using var canvas = new SKCanvas(bitmap);
            CurrencyGameLogic.DrawWheel(canvas, segments.Length, segments, winningSegment + 2);

            using var stream = new MemoryStream();
            bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
            stream.Seek(0, SeekOrigin.Begin);

            var delta = CurrencyGameLogic.ComputeSegmentDelta(segments[winningSegment], betAmount);
            var payout = Math.Max(0, betAmount + delta);

            if (payout > 0)
                await PayoutAsync(payout, "spinwheel");

            var net = payout - betAmount;
            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

            var eb = new EmbedBuilder()
                .WithTitle(net >= 0
                    ? Strings.SpinwheelWinTitle(ctx.Guild.Id)
                    : Strings.SpinwheelLossTitle(ctx.Guild.Id))
                .WithDescription(Strings.SpinwheelResult(ctx.Guild.Id, segments[winningSegment], net, emote))
                .WithColor(net >= 0 ? Color.Green : Color.Red)
                .WithImageUrl("attachment://wheelResult.png");

            await ctx.Interaction.FollowupWithFileAsync(stream, "wheelResult.png", embed: eb.Build());
        }

        /// <summary>
        ///     Play a scratch card game.
        /// </summary>
        /// <param name="cardType">The type of card to buy: bronze, silver, gold, or diamond.</param>
        [SlashCommand("scratch", "Buys and scratches a scratch card")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task ScratchCard(
            [Summary("card", "The card tier to buy")]
            ScratchCardType cardType = ScratchCardType.Bronze)
        {
            await DeferAsync();
            var cardTypes = new Dictionary<ScratchCardType, (int cost, double winChance, int minWin, int maxWin)>
            {
                {
                    ScratchCardType.Bronze, (10, 0.3, 5, 50)
                },
                {
                    ScratchCardType.Silver, (50, 0.25, 25, 200)
                },
                {
                    ScratchCardType.Gold, (200, 0.2, 100, 1000)
                },
                {
                    ScratchCardType.Diamond, (1000, 0.15, 500, 5000)
                }
            };

            var card = cardTypes[cardType];

            if (!await TryTakeBetAsync(card.cost, "scratchcard"))
                return;

            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);
            var won = CurrencyRng.NextDouble() < card.winChance;

            var eb = new EmbedBuilder()
                .WithTitle(Strings.ScratchCardTitle(ctx.Guild.Id))
                .WithColor(Color.Blue)
                .AddField(Strings.ScratchCardType(ctx.Guild.Id, cardType.ToString().ToUpperInvariant()), "", true)
                .AddField(Strings.ScratchCardCost(ctx.Guild.Id, card.cost, emote), "", true);

            if (won)
            {
                var winAmount = CurrencyRng.Next(card.minWin, card.maxWin + 1);
                winAmount = (int)await WinAsync(card.cost, (double)winAmount / card.cost, "scratchcard");

                eb.WithDescription(Strings.ScratchCardWin(ctx.Guild.Id, winAmount, emote))
                    .WithColor(Color.Green);

                var symbols = CurrencyGameLogic.GenerateScratchSymbols(true);
                eb.AddField(Strings.ScratchCardSymbols(ctx.Guild.Id, symbols), "");
            }
            else
            {
                eb.WithDescription(Strings.ScratchCardLose(ctx.Guild.Id))
                    .WithColor(Color.Red);

                var symbols = CurrencyGameLogic.GenerateScratchSymbols(false);
                eb.AddField(Strings.ScratchCardSymbols(ctx.Guild.Id, symbols), "");
            }

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Play Russian Roulette.
        /// </summary>
        /// <param name="betAmount">The amount to bet.</param>
        /// <param name="bullets">Number of bullets (1 to 5).</param>
        [SlashCommand("russianroulette", "Loads the chamber and pulls the trigger for a multiplier")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task RussianRoulette([Summary("bet", "The amount to bet")] long betAmount,
            [Summary("bullets", "Number of bullets, 1 to 5")]
            int bullets = 1)
        {
            await DeferAsync();

            if (bullets is < 1 or > 5)
                bullets = 1;

            if (!await TryTakeBetAsync(betAmount, "russianroulette"))
                return;

            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);
            var chamber = CurrencyRng.Next(1, 7);
            var bulletChambers = new HashSet<int>();

            while (bulletChambers.Count < bullets)
            {
                bulletChambers.Add(CurrencyRng.Next(1, 7));
            }

            var survived = !bulletChambers.Contains(chamber);
            var multiplier = bullets switch
            {
                1 => 1.2,
                2 => 1.5,
                3 => 2.0,
                4 => 3.0,
                5 => 5.0,
                _ => 1.2
            };

            var eb = new EmbedBuilder()
                .WithTitle(Strings.RussianRouletteTitle(ctx.Guild.Id))
                .WithDescription(Strings.RussianRouletteDescription(ctx.Guild.Id))
                .WithColor(Color.Blue)
                .AddField(Strings.RussianRouletteChambers(ctx.Guild.Id, $"{chamber}/6"), "", true)
                .AddField(Strings.RussianRouletteBullets(ctx.Guild.Id, bullets.ToString()), "", true);

            if (survived)
            {
                var winAmount = await WinAsync(betAmount, 1 + multiplier, "russianroulette");

                eb.AddField(Strings.RussianRouletteResultField(ctx.Guild.Id),
                        Strings.RussianRouletteClick(ctx.Guild.Id))
                    .AddField(Strings.RussianRouletteOutcomeField(ctx.Guild.Id),
                        Strings.RussianRouletteSurvived(ctx.Guild.Id, winAmount - betAmount, emote))
                    .WithColor(Color.Green);
            }
            else
            {
                eb.AddField(Strings.RussianRouletteOutcomeField(ctx.Guild.Id),
                        Strings.RussianRouletteDied(ctx.Guild.Id, betAmount, emote))
                    .WithColor(Color.Red);
            }

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Play a memory game by remembering sequences.
        /// </summary>
        /// <param name="betAmount">The amount to bet.</param>
        /// <param name="difficulty">The difficulty level: easy, medium, or hard.</param>
        [SlashCommand("memory", "Shows a sequence briefly and pays out if you repeat it")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Memory([Summary("bet", "The amount to bet")] long betAmount,
            [Summary("difficulty", "The difficulty level")]
            MemoryDifficulty difficulty = MemoryDifficulty.Easy)
        {
            await DeferAsync();
            var difficultyConfigs = new Dictionary<MemoryDifficulty, (int length, int showTime, double multiplier)>
            {
                {
                    MemoryDifficulty.Easy, (4, 3000, 1.5)
                },
                {
                    MemoryDifficulty.Medium, (6, 2500, 2.0)
                },
                {
                    MemoryDifficulty.Hard, (8, 2000, 3.0)
                }
            };

            if (!await TryTakeBetAsync(betAmount, "memory"))
                return;

            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);
            var config = difficultyConfigs[difficulty];
            var sequence = CurrencyGameLogic.GenerateMemorySequence(config.length);
            var emojis = CurrencyGameLogic.MemoryEmojis;

            var sequenceDisplay = string.Join(" ", sequence.Select(i => emojis[i]));

            var showEmbed = new EmbedBuilder()
                .WithTitle(Strings.MemoryTitle(ctx.Guild.Id))
                .WithDescription(Strings.MemorySequenceShow(ctx.Guild.Id, sequenceDisplay))
                .WithColor(Color.Blue)
                .AddField(Strings.MemoryDifficulty(ctx.Guild.Id, difficulty.ToString().ToUpperInvariant()), "", true)
                .AddField(Strings.MemoryShowTime(ctx.Guild.Id, $"{config.showTime / 1000} seconds"), "", true);

            var message = await ctx.Interaction.FollowupAsync(embed: showEmbed.Build());

            await Task.Delay(config.showTime);

            var hideEmbed = new EmbedBuilder()
                .WithTitle(Strings.MemoryTitle(ctx.Guild.Id))
                .WithDescription(Strings.MemorySequenceHidden(ctx.Guild.Id))
                .WithColor(Color.Orange)
                .AddField(Strings.MemoryInstructions(ctx.Guild.Id), Strings.MemoryInstructionsText(ctx.Guild.Id));

            await message.ModifyAsync(m => m.Embed = hideEmbed.Build());

            var response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);

            if (response == null)
            {
                var timeoutEmbed = new EmbedBuilder()
                    .WithTitle(Strings.MemoryTitle(ctx.Guild.Id))
                    .WithDescription(Strings.MemoryTimeout(ctx.Guild.Id, betAmount, emote))
                    .WithColor(Color.Red);

                await ctx.Interaction.FollowupAsync(embed: timeoutEmbed.Build());
                return;
            }

            var userSequence = CurrencyGameLogic.ParseMemorySequence(response, emojis);
            var isCorrect = userSequence.SequenceEqual(sequence);

            if (isCorrect)
            {
                var winAmount = await WinAsync(betAmount, config.multiplier, "memory");

                var winEmbed = new EmbedBuilder()
                    .WithTitle(Strings.MemoryTitle(ctx.Guild.Id))
                    .WithDescription(Strings.MemoryCorrect(ctx.Guild.Id, winAmount - betAmount, emote))
                    .WithColor(Color.Green);

                await ctx.Interaction.FollowupAsync(embed: winEmbed.Build());
            }
            else
            {
                var loseEmbed = new EmbedBuilder()
                    .WithTitle(Strings.MemoryTitle(ctx.Guild.Id))
                    .WithDescription(Strings.MemoryIncorrect(ctx.Guild.Id, sequenceDisplay, betAmount, emote))
                    .WithColor(Color.Red);

                await ctx.Interaction.FollowupAsync(embed: loseEmbed.Build());
            }
        }

        /// <summary>
        ///     Challenge another player to a dice duel.
        /// </summary>
        /// <param name="opponent">The user to challenge.</param>
        /// <param name="betAmount">The amount each player bets.</param>
        [SlashCommand("diceduel", "Challenges another member to a dice duel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task DiceDuel([Summary("opponent", "The user to challenge")] IUser opponent,
            [Summary("bet", "The amount each player bets")]
            long betAmount)
        {
            await DeferAsync();

            if (opponent.Id == ctx.User.Id)
            {
                await ErrorAsync(Strings.DiceDuelCannotSelf(ctx.Guild.Id));
                return;
            }

            if (opponent.IsBot)
            {
                await ErrorAsync(Strings.DiceDuelCannotBot(ctx.Guild.Id));
                return;
            }

            var opponentBalance = await Service.GetUserBalanceAsync(opponent.Id, ctx.Guild.Id);

            if (betAmount > opponentBalance)
            {
                await ErrorAsync(Strings.DiceDuelOpponentInsufficientFunds(ctx.Guild.Id,
                    await Service.GetCurrencyEmote(ctx.Guild.Id)));
                return;
            }

            var config = await ConfigService.GetConfigAsync(ctx.Guild.Id);

            switch (CurrencyConfigService.ValidateBet(config, betAmount))
            {
                case BetValidation.GamblingDisabled:
                    await ErrorAsync(Strings.GamblingDisabled(ctx.Guild.Id));
                    return;
                case BetValidation.BelowMinimum:
                    await ErrorAsync(Strings.BetBelowMinimum(ctx.Guild.Id, config.MinBet,
                        await Service.GetCurrencyEmote(ctx.Guild.Id)));
                    return;
                case BetValidation.AboveMaximum:
                    await ErrorAsync(Strings.BetAboveMaximum(ctx.Guild.Id, config.MaxBet,
                        await Service.GetCurrencyEmote(ctx.Guild.Id)));
                    return;
            }

            var challengeEmbed = new EmbedBuilder()
                .WithTitle(Strings.DiceDuelChallengeTitle(ctx.Guild.Id))
                .WithDescription(Strings.DiceDuelChallenge(ctx.Guild.Id, ctx.User.Mention, opponent.Mention,
                    betAmount, await Service.GetCurrencyEmote(ctx.Guild.Id)))
                .WithColor(Color.Orange);

            var component = new ComponentBuilder()
                .WithButton(Strings.DiceDuelAccept(ctx.Guild.Id), $"diceduel_accept_{ctx.User.Id}_{betAmount}",
                    ButtonStyle.Success)
                .WithButton(Strings.DiceDuelDecline(ctx.Guild.Id), $"diceduel_decline_{ctx.User.Id}",
                    ButtonStyle.Danger)
                .Build();

            await ctx.Interaction.FollowupAsync(embed: challengeEmbed.Build(), components: component);
        }

        /// <summary>
        ///     Determines the outcome of a rock paper scissors lizard spock round.
        /// </summary>
        /// <param name="playerChoice">The player's lowercase choice.</param>
        /// <param name="botChoice">The bot's lowercase choice.</param>
        /// <returns>The result keyword and the localized explanation.</returns>
        private (string result, string description) DetermineWinner(string playerChoice, string botChoice)
        {
            if (playerChoice == botChoice) return ("tie", Strings.RpsTie(ctx.Guild.Id));
            return (playerChoice, botChoice) switch
            {
                ("scissors", "paper") => ("win", Strings.RpsScissorsPaper(ctx.Guild.Id)),
                ("paper", "rock") => ("win", Strings.RpsPaperRock(ctx.Guild.Id)),
                ("rock", "lizard") => ("win", Strings.RpsRockLizard(ctx.Guild.Id)),
                ("lizard", "spock") => ("win", Strings.RpsLizardSpock(ctx.Guild.Id)),
                ("spock", "scissors") => ("win", Strings.RpsSpockScissors(ctx.Guild.Id)),
                ("scissors", "lizard") => ("win", Strings.RpsScissorsLizard(ctx.Guild.Id)),
                ("lizard", "paper") => ("win", Strings.RpsLizardPaper(ctx.Guild.Id)),
                ("paper", "spock") => ("win", Strings.RpsPaperSpock(ctx.Guild.Id)),
                ("spock", "rock") => ("win", Strings.RpsSpockRock(ctx.Guild.Id)),
                ("rock", "scissors") => ("win", Strings.RpsRockScissors(ctx.Guild.Id)),
                ("paper", "scissors") => ("lose", Strings.RpsScissorsPaper(ctx.Guild.Id)),
                ("rock", "paper") => ("lose", Strings.RpsPaperRock(ctx.Guild.Id)),
                ("lizard", "rock") => ("lose", Strings.RpsRockLizard(ctx.Guild.Id)),
                ("spock", "lizard") => ("lose", Strings.RpsLizardSpock(ctx.Guild.Id)),
                ("scissors", "spock") => ("lose", Strings.RpsSpockScissors(ctx.Guild.Id)),
                ("lizard", "scissors") => ("lose", Strings.RpsScissorsLizard(ctx.Guild.Id)),
                ("paper", "lizard") => ("lose", Strings.RpsLizardPaper(ctx.Guild.Id)),
                ("spock", "paper") => ("lose", Strings.RpsPaperSpock(ctx.Guild.Id)),
                ("rock", "spock") => ("lose", Strings.RpsSpockRock(ctx.Guild.Id)),
                ("scissors", "rock") => ("lose", Strings.RpsRockScissors(ctx.Guild.Id)),
                _ => throw new ArgumentException("Invalid choice combination")
            };
        }
    }
}