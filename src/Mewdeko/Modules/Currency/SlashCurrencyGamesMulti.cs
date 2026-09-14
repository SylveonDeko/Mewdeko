using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Currency.Common;
using Mewdeko.Modules.Currency.Services;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Currency;

/// <summary>
///     The multi-step games (horse racing, trivia chains, the lottery) and the daily challenge subgroup.
/// </summary>
public partial class SlashCurrency
{
    /// <summary>
    ///     Gambling games played against the house or other members.
    /// </summary>
    public partial class CurrencyGames
    {
        /// <summary>
        ///     Joins or starts a horse race with a specified bet amount.
        /// </summary>
        /// <param name="betAmount">The amount of currency to bet on the race.</param>
        [SlashCommand("horserace", "Joins or starts a horse race")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task HorseRace([Summary("bet", "The amount to bet on the race")] int betAmount)
        {
            await DeferAsync();

            if (betAmount <= 0)
            {
                await ReplyErrorAsync(Strings.HorseRaceInvalidBet(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (!await Service.TryDebitAsync(ctx.User.Id, betAmount, Strings.HorseRaceTransactionStake(ctx.Guild.Id),
                    CurrencyCategory.GameBet, ctx.Guild.Id, "horserace"))
            {
                await ReplyErrorAsync(Strings.HorseRaceInsufficientFunds(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var result = await horseRacingService.JoinRace(ctx.User, ctx.Guild.Id, betAmount);
            if (!result.Success)
            {
                await Service.CreditAsync(ctx.User.Id, betAmount, Strings.HorseRaceTransactionRefund(ctx.Guild.Id),
                    CurrencyCategory.GamePayout, ctx.Guild.Id, "horserace");

                await ReplyErrorAsync(result.Message switch
                {
                    "horse_race_full" => Strings.HorseRaceFull(ctx.Guild.Id),
                    "horse_race_already_joined" => Strings.HorseRaceAlreadyJoined(ctx.Guild.Id),
                    _ => result.Message
                }).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.HorseRaceJoined(ctx.Guild.Id, betAmount)).ConfigureAwait(false);

            if (result.RaceStarted)
            {
                await StartRace();
            }
            else
            {
                _ = Task.Delay(10000).ContinueWith(async _ =>
                {
                    if (await horseRacingService.UpdateRaceProgress(ctx.Guild.Id) != null)
                    {
                        await StartRace();
                    }
                });
            }
        }

        /// <summary>
        ///     Starts the race and runs the update loop.
        /// </summary>
        private async Task StartRace()
        {
            var raceMessage = await ctx.Channel.SendMessageAsync(embed: CreateRaceEmbed());
            await RunRaceUpdateLoop(raceMessage);
        }

        /// <summary>
        ///     Runs the race update loop, updating the race embed every second for 6 seconds.
        /// </summary>
        /// <param name="raceMessage">The message containing the race embed to update.</param>
        private async Task RunRaceUpdateLoop(IUserMessage raceMessage)
        {
            for (var i = 0; i < 6; i++)
            {
                await Task.Delay(1000);
                var raceProgress = await horseRacingService.UpdateRaceProgress(ctx.Guild.Id);
                await raceMessage.ModifyAsync(m => m.Embed = CreateRaceEmbed(raceProgress));
            }

            var finalResult = await horseRacingService.FinishRace(ctx.Guild.Id);
            await raceMessage.ModifyAsync(m => m.Embed = CreateFinalRaceEmbed(finalResult));

            foreach (var winner in finalResult.Winners)
            {
                await ctx.Channel.SendConfirmAsync(Strings.HorseRaceWinner(ctx.Guild.Id, winner.Username,
                    winner.Winnings)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Creates the race embed showing the current progress of all racers.
        /// </summary>
        /// <param name="progress">The current progress of all racers. If null, creates an initial embed.</param>
        /// <returns>An embed displaying the race progress.</returns>
        private Embed CreateRaceEmbed(List<RacerProgress>? progress = null)
        {
            var eb = new EmbedBuilder()
                .WithTitle(Strings.HorseRaceInProgress(ctx.Guild.Id))
                .WithDescription(Strings.HorseRaceDescription(ctx.Guild.Id));

            if (progress == null) return eb.Build();
            foreach (var racer in progress)
            {
                eb.AddField($"{racer.Animal} {racer.Username}",
                    $"{new string('▓', racer.Progress)}{new string('░', 10 - racer.Progress)}");
            }

            return eb.Build();
        }

        /// <summary>
        ///     Creates the final race embed showing the results of the race.
        /// </summary>
        /// <param name="result">The final result of the race.</param>
        /// <returns>An embed displaying the final race results.</returns>
        private Embed CreateFinalRaceEmbed(RaceResult result)
        {
            var eb = new EmbedBuilder()
                .WithTitle(Strings.HorseRaceFinished(ctx.Guild.Id))
                .WithDescription(Strings.HorseRaceWinnerAnnouncement(ctx.Guild.Id, result.Winners.First().Username));

            foreach (var racer in result.FinalPositions)
            {
                eb.AddField($"{racer.Position}. {racer.Animal} {racer.Username}",
                    Strings.HorseRaceFinalStatus(ctx.Guild.Id, racer.Winnings));
            }

            return eb.Build();
        }

        /// <summary>
        ///     Play a trivia chain where you answer questions in succession for increasing rewards.
        /// </summary>
        /// <param name="betAmount">The amount to bet.</param>
        /// <param name="category">The trivia category.</param>
        [SlashCommand("triviachain", "Answers trivia questions in a row for growing rewards")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task TriviaChain([Summary("bet", "The amount to bet")] long betAmount,
            [Summary("category", "The trivia category")]
            TriviaCategory category = TriviaCategory.General)
        {
            await DeferAsync();
            var categoryKey = category.ToString().ToLowerInvariant();

            if (triviaChainService.GetTriviaChainState(ctx.User.Id) != null)
            {
                await ErrorAsync(Strings.TriviaChainActiveGame(ctx.Guild.Id));
                return;
            }

            if (!await TryTakeBetAsync(betAmount, "triviachain"))
                return;

            var chainState =
                await triviaChainService.StartTriviaChainAsync(ctx.User.Id, ctx.Guild.Id, betAmount, categoryKey);

            var currentMultiplier = chainState.CurrentMultiplier;
            var potentialWin = (long)(betAmount * currentMultiplier);

            var eb = new EmbedBuilder()
                .WithTitle(Strings.TriviaChainTitle(ctx.Guild.Id))
                .WithColor(Color.Blue)
                .WithDescription(chainState.CurrentQuestion)
                .AddField(Strings.TriviaChainQuestion(ctx.Guild.Id, 1, chainState.CurrentQuestion), "_ _", true)
                .AddField(Strings.TriviaChainMultiplier(ctx.Guild.Id, $"{currentMultiplier:F1}x"), "_ _", true)
                .AddField(
                    Strings.TriviaChainPotentialWin(ctx.Guild.Id, potentialWin,
                        await Service.GetCurrencyEmote(ctx.Guild.Id)), "_ _", true)
                .AddField(
                    Strings.TriviaChainCurrentWinnings(ctx.Guild.Id, 0, await Service.GetCurrencyEmote(ctx.Guild.Id)),
                    "_ _", true);

            var selectMenuBuilder = new SelectMenuBuilder()
                .WithPlaceholder(Strings.TriviaChainChooseAnswer(ctx.Guild.Id))
                .WithCustomId($"triviachain_answer_{ctx.User.Id}_0");

            for (var i = 0; i < chainState.CurrentOptions.Length; i++)
            {
                selectMenuBuilder.AddOption(chainState.CurrentOptions[i], i.ToString(),
                    Strings.TriviaChainOption(ctx.Guild.Id, i + 1));
            }

            var componentBuilder = new ComponentBuilder()
                .WithSelectMenu(selectMenuBuilder);

            await ctx.Interaction.FollowupAsync(embed: eb.Build(), components: componentBuilder.Build());
        }

        /// <summary>
        ///     Buy lottery tickets.
        /// </summary>
        /// <param name="ticketCount">Number of tickets to buy (1 to 10).</param>
        [SlashCommand("lottery", "Buys lottery tickets for a chance at a big payout")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Lottery([Summary("tickets", "Number of tickets to buy, 1 to 10")] int ticketCount = 1)
        {
            await DeferAsync();

            if (ticketCount is < 1 or > 10)
            {
                await ErrorAsync(Strings.LotteryTicketInvalid(ctx.Guild.Id));
                return;
            }

            const int ticketCost = 50;
            var totalCost = ticketCost * ticketCount;

            if (!await TryTakeBetAsync(totalCost, "lottery"))
                return;

            var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);
            var won = false;
            var winAmount = 0L;

            var winChance = Math.Min(0.1 + ticketCount * 0.05, 0.4);

            if (CurrencyRng.NextDouble() < winChance)
            {
                won = true;
                var multiplier = ticketCount switch
                {
                    1 => CurrencyRng.Next(2, 5),
                    2 => CurrencyRng.Next(3, 7),
                    3 => CurrencyRng.Next(4, 9),
                    4 => CurrencyRng.Next(5, 11),
                    5 => CurrencyRng.Next(6, 13),
                    _ => CurrencyRng.Next(7, 15)
                };
                winAmount = totalCost * multiplier;
            }

            var eb = new EmbedBuilder()
                .WithTitle(Strings.LotteryDrawTitle(ctx.Guild.Id, CurrencyRng.Next(1000, 9999)))
                .WithColor(won ? Color.Green : Color.Red)
                .AddField(Strings.LotteryTicketCost(ctx.Guild.Id, ticketCost, emote), $"{ticketCost} {emote}", true)
                .AddField(Strings.LotteryYourTickets(ctx.Guild.Id, ticketCount.ToString()), "", true);

            if (won)
            {
                winAmount = await WinAsync(totalCost, (double)winAmount / totalCost, "lottery");

                eb.WithDescription(Strings.LotteryWinner(ctx.Guild.Id, ctx.User.Mention, CurrencyRng.Next(1, 1000),
                    winAmount, emote));
            }
            else
            {
                eb.WithDescription(Strings.LotteryNoWinner(ctx.Guild.Id, totalCost + CurrencyRng.Next(100, 500),
                    emote));
            }

            await ctx.Interaction.FollowupAsync(
                Strings.LotteryTicketsBought(ctx.Guild.Id, ticketCount, totalCost, emote),
                embed: eb.Build());
        }
    }

    /// <summary>
    ///     Daily challenge commands.
    /// </summary>
    [Group("challenge", "Daily challenges")]
    public class CurrencyChallenge(DailyChallengeService dailyChallengeService)
        : MewdekoSlashSubmodule<ICurrencyService>
    {
        /// <summary>
        ///     Check your current daily challenge.
        /// </summary>
        [SlashCommand("daily", "Shows your current daily challenge")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task DailyChallenge()
        {
            var challenge = await dailyChallengeService.GetCurrentChallenge(ctx.User.Id, ctx.Guild.Id);

            if (challenge == null)
            {
                await ErrorAsync(Strings.DailyChallengeAlreadyCompleted(ctx.Guild.Id));
                return;
            }

            var eb = new EmbedBuilder()
                .WithTitle(Strings.DailyChallengeTitle(ctx.Guild.Id))
                .WithColor(Color.Blue)
                .WithDescription(challenge.Description)
                .AddField(Strings.DailyChallengeProgress(ctx.Guild.Id, challenge.Progress, challenge.RequiredAmount),
                    "", true)
                .AddField(
                    Strings.DailyChallengeReward(ctx.Guild.Id, challenge.RewardAmount,
                        await Service.GetCurrencyEmote(ctx.Guild.Id)), "", true);

            if (challenge.Progress >= challenge.RequiredAmount)
            {
                eb.WithColor(Color.Green)
                    .AddField(Strings.DailyChallengeStatus(ctx.Guild.Id),
                        Strings.DailyChallengeReadyToClaim(ctx.Guild.Id));
            }

            await ctx.Interaction.RespondAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Claim your completed daily challenge reward.
        /// </summary>
        [SlashCommand("claim", "Claims your completed daily challenge reward")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task ClaimChallenge()
        {
            var challenge = await dailyChallengeService.GetCurrentChallenge(ctx.User.Id, ctx.Guild.Id);

            if (challenge == null)
            {
                await ErrorAsync(Strings.DailyChallengeNoneAvailable(ctx.Guild.Id));
                return;
            }

            if (challenge.Progress < challenge.RequiredAmount)
            {
                await ErrorAsync(Strings.DailyChallengeNotCompleted(ctx.Guild.Id));
                return;
            }

            var reward = await dailyChallengeService.CompleteChallenge(ctx.User.Id, ctx.Guild.Id,
                challenge.ChallengeType);

            if (reward > 0)
            {
                await Service.CreditAsync(ctx.User.Id, reward, Strings.DailyChallengeTransactionReward(ctx.Guild.Id),
                    CurrencyCategory.Challenge, ctx.Guild.Id, "challenge");

                await ReplyConfirmAsync(Strings.DailyChallengeClaimed(ctx.Guild.Id, reward,
                    await Service.GetCurrencyEmote(ctx.Guild.Id)));
            }
            else
            {
                await ReplyErrorAsync(Strings.DailyChallengeAlreadyClaimed(ctx.Guild.Id));
            }
        }

        /// <summary>
        ///     View the daily challenge leaderboard.
        /// </summary>
        [SlashCommand("leaderboard", "Shows who has completed the most daily challenges")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task ChallengeLeaderboard()
        {
            await DeferAsync();
            var leaderboard = await dailyChallengeService.GetLeaderboard(ctx.Guild.Id);

            if (leaderboard.Count == 0)
            {
                await ErrorAsync(Strings.DailyChallengeLeaderboardEmpty(ctx.Guild.Id));
                return;
            }

            var eb = new EmbedBuilder()
                .WithTitle(Strings.DailyChallengeLeaderboardTitle(ctx.Guild.Id))
                .WithColor(Color.Gold)
                .WithDescription(Strings.DailyChallengeLeaderboardDescription(ctx.Guild.Id));

            for (var i = 0; i < leaderboard.Count; i++)
            {
                var (userId, completionCount) = leaderboard[i];
                var user = await ctx.Guild.GetUserAsync(userId) ?? (IUser)await ctx.Client.GetUserAsync(userId);
                var position = i + 1;
                var username = user?.Username ?? "Unknown User";

                eb.AddField($"{position}. {username}",
                    Strings.DailyChallengeLeaderboardEntry(ctx.Guild.Id, i + 1, username, completionCount), true);
            }

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }
    }
}