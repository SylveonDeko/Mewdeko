using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Currency.Services;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Currency;

public partial class Currency
{
    /// <summary>
    ///     Submodule for horse racing commands.
    /// </summary>
    [Group]
    public class HorseRacing(ICurrencyService cs) : MewdekoSubmodule<HorseRacingService>
    {
        /// <summary>
        ///     Joins or starts a horse race with a specified bet amount.
        /// </summary>
        /// <param name="betAmount">The amount of currency to bet on the race.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public async Task HorseRace(int betAmount)
        {
            if (betAmount <= 0)
            {
                await ReplyErrorAsync(Strings.HorseRaceInvalidBet(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (!await cs.TryDebitAsync(ctx.User.Id, betAmount, Strings.HorseRaceTransactionStake(ctx.Guild.Id),
                    CurrencyCategory.GameBet, ctx.Guild.Id, "horserace"))
            {
                await ReplyErrorAsync(Strings.HorseRaceInsufficientFunds(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var result = await Service.JoinRace(ctx.User, ctx.Guild.Id, betAmount);
            if (!result.Success)
            {
                await cs.CreditAsync(ctx.User.Id, betAmount, Strings.HorseRaceTransactionRefund(ctx.Guild.Id),
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
                    if (await Service.UpdateRaceProgress(ctx.Guild.Id) != null)
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
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task RunRaceUpdateLoop(IUserMessage raceMessage)
        {
            for (var i = 0; i < 6; i++)
            {
                await Task.Delay(1000);
                var raceProgress = await Service.UpdateRaceProgress(ctx.Guild.Id);
                await raceMessage.ModifyAsync(m => m.Embed = CreateRaceEmbed(raceProgress));
            }

            var finalResult = await Service.FinishRace(ctx.Guild.Id);
            await raceMessage.ModifyAsync(m => m.Embed = CreateFinalRaceEmbed(finalResult));

            foreach (var winner in finalResult.Winners)
            {
                await ReplyConfirmAsync(Strings.HorseRaceWinner(ctx.Guild.Id, winner.Username, winner.Winnings))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Creates the race embed showing the current progress of all racers.
        /// </summary>
        /// <param name="progress">The current progress of all racers. If null, creates an initial embed.</param>
        /// <returns>An embed displaying the race progress.</returns>
        private Embed CreateRaceEmbed(List<RacerProgress> progress = null)
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
    }
}