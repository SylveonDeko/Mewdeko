using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Gambling.Common;
using Mewdeko.Modules.Gambling.Common.AnimalRacing;
using Mewdeko.Modules.Gambling.Common.AnimalRacing.Exceptions;
using Mewdeko.Modules.Gambling.Services;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Gambling;

// wth is this, needs full rewrite
public partial class Gambling
{
    [Group]
    public class AnimalRacingCommands : GamblingSubmodule<AnimalRaceService>
    {
        private readonly DiscordSocketClient _client;
        private readonly ICurrencyService _cs;
        private readonly GamesConfigService _gamesConf;

        private IUserMessage raceMessage;

        public AnimalRacingCommands(ICurrencyService cs, DiscordSocketClient client,
            GamblingConfigService gamblingConf, GamesConfigService gamesConf) : base(gamblingConf)
        {
            _cs = cs;
            _client = client;
            _gamesConf = gamesConf;
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [MewdekoOptionsAttribute(typeof(RaceOptions))]
        public Task Race(params string[] args)
        {
            var (options, success) = OptionsParser.ParseFrom(new RaceOptions(), args);

            var ar = new AnimalRace(options, _cs, _gamesConf.Data.RaceAnimals.Shuffle());
            if (!Service.AnimalRaces.TryAdd(ctx.Guild.Id, ar))
                return ctx.Channel.SendErrorAsync(GetText("animal_race"), GetText("animal_race_already_started"));

            ar.Initialize();

            var count = 0;

            Task _client_MessageReceived(SocketMessage arg)
            {
                var _ = Task.Run(() =>
                {
                    try
                    {
                        if (arg.Channel.Id == ctx.Channel.Id)
                            if (ar.CurrentPhase == AnimalRace.Phase.Running && ++count % 9 == 0)
                                raceMessage = null;
                    }
                    catch
                    {
                    }
                });
                return Task.CompletedTask;
            }

            Task Ar_OnEnded(AnimalRace race)
            {
                _client.MessageReceived -= _client_MessageReceived;
                Service.AnimalRaces.TryRemove(ctx.Guild.Id, out _);
                var winner = race.FinishedUsers[0];
                if (race.FinishedUsers[0].Bet > 0)
                    return ctx.Channel.SendConfirmAsync(GetText("animal_race"),
                        GetText("animal_race_won_money", Format.Bold(winner.Username),
                            winner.Animal.Icon, race.FinishedUsers[0].Bet * (race.Users.Count - 1) + CurrencySign));
                return ctx.Channel.SendConfirmAsync(GetText("animal_race"),
                    GetText("animal_race_won", Format.Bold(winner.Username), winner.Animal.Icon));
            }

            ar.OnStartingFailed += Ar_OnStartingFailed;
            ar.OnStateUpdate += Ar_OnStateUpdate;
            ar.OnEnded += Ar_OnEnded;
            ar.OnStarted += Ar_OnStarted;
            _client.MessageReceived += _client_MessageReceived;

            return ctx.Channel.SendConfirmAsync(GetText("animal_race"),
                GetText("animal_race_starting", options.StartTime),
                footer: GetText("animal_race_join_instr", Prefix));
        }

        private Task Ar_OnStarted(AnimalRace race)
        {
            if (race.Users.Count == race.MaxUsers)
                return ctx.Channel.SendConfirmAsync(GetText("animal_race"), GetText("animal_race_full"));
            return ctx.Channel.SendConfirmAsync(GetText("animal_race"),
                GetText("animal_race_starting_with_x", race.Users.Count));
        }

        private async Task Ar_OnStateUpdate(AnimalRace race)
        {
            var text = $@"|🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🔚|
{string.Join("\n", race.Users.Select(p =>
{
    var index = race.FinishedUsers.IndexOf(p);
    var extra = index == -1 ? "" : $"#{index + 1} {(index == 0 ? "🏆" : "")}";
    return $"{(int) (p.Progress / 60f * 100),-2}%|{new string('‣', p.Progress) + p.Animal.Icon + extra}";
}))}
|🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🔚|";

            var msg = raceMessage;

            if (msg == null)
                raceMessage = await ctx.Channel.SendConfirmAsync(text)
                    .ConfigureAwait(false);
            else
                await msg.ModifyAsync(x => x.Embed = new EmbedBuilder()
                        .WithTitle(GetText("animal_race"))
                        .WithDescription(text)
                        .WithOkColor()
                        .Build())
                    .ConfigureAwait(false);
        }

        private Task Ar_OnStartingFailed(AnimalRace race)
        {
            Service.AnimalRaces.TryRemove(ctx.Guild.Id, out _);
            return ReplyErrorLocalizedAsync("animal_race_failed");
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task JoinRace(ShmartNumber amount = default)
        {
            if (!await CheckBetOptional(amount).ConfigureAwait(false))
                return;

            if (!Service.AnimalRaces.TryGetValue(ctx.Guild.Id, out var ar))
            {
                await ReplyErrorLocalizedAsync("race_not_exist").ConfigureAwait(false);
                return;
            }

            try
            {
                var user = await ar.JoinRace(ctx.User.Id, ctx.User.ToString(), amount)
                    .ConfigureAwait(false);
                if (amount > 0)
                    await ctx.Channel.SendConfirmAsync(GetText("animal_race_join_bet", ctx.User.Mention,
                        user.Animal.Icon, amount + CurrencySign)).ConfigureAwait(false);
                else
                    await ctx.Channel
                        .SendConfirmAsync(GetText("animal_race_join", ctx.User.Mention, user.Animal.Icon))
                        .ConfigureAwait(false);
            }
            catch (ArgumentOutOfRangeException)
            {
                //ignore if user inputed an invalid amount
            }
            catch (AlreadyJoinedException)
            {
                // just ignore this
            }
            catch (AlreadyStartedException)
            {
                //ignore
            }
            catch (AnimalRaceFullException)
            {
                await ctx.Channel.SendConfirmAsync(GetText("animal_race"), GetText("animal_race_full"))
                    .ConfigureAwait(false);
            }
            catch (NotEnoughFundsException)
            {
                await ctx.Channel.SendErrorAsync(GetText("not_enough", CurrencySign)).ConfigureAwait(false);
            }
        }
    }
}