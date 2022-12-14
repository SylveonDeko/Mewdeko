using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
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
        private readonly ICurrencyService cs;
        private readonly GamesConfigService gamesConf;
        private readonly GuildSettingsService guildSettings;
        private readonly EventHandler eventHandler;

        private IUserMessage? raceMessage;

        public AnimalRacingCommands(ICurrencyService cs,
            GamblingConfigService gamblingConf, GamesConfigService gamesConf,
            GuildSettingsService guildSettings,
            EventHandler eventHandler) : base(gamblingConf)
        {
            this.cs = cs;
            this.gamesConf = gamesConf;
            this.guildSettings = guildSettings;
            this.eventHandler = eventHandler;
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         MewdekoOptions(typeof(RaceOptions))]
        public async Task<Task<IUserMessage>> Race(params string[] args)
        {
            var (options, _) = OptionsParser.ParseFrom(new RaceOptions(), args);

            var ar = new AnimalRace(options, cs, gamesConf.Data.RaceAnimals.Shuffle());
            if (!Service.AnimalRaces.TryAdd(ctx.Guild.Id, ar))
                return ctx.Channel.SendErrorAsync(GetText("animal_race"), GetText("animal_race_already_started"));

            ar.Initialize();

            var count = 0;

            Task ClientMessageReceived(SocketMessage arg)
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        if (arg.Channel.Id != ctx.Channel.Id) return;
                        if (ar.CurrentPhase == AnimalRace.Phase.Running && ++count % 9 == 0)
                            raceMessage = null;
                    }
                    catch
                    {
                        // ignored
                    }
                });
                return Task.CompletedTask;
            }

            async Task<IUserMessage> ArOnEnded(AnimalRace race)
            {
                eventHandler.MessageReceived -= ClientMessageReceived;
                Service.AnimalRaces.TryRemove(ctx.Guild.Id, out _);
                var winner = race.FinishedUsers[0];
                if (race.FinishedUsers[0].Bet > 0)
                {
                    return await ctx.Channel.SendConfirmAsync(GetText("animal_race"),
                        GetText("animal_race_won_money", Format.Bold(winner.Username),
                            winner.Animal.Icon, (race.FinishedUsers[0].Bet * (race.Users.Count - 1)) + CurrencySign));
                }

                return await ctx.Channel.SendConfirmAsync(GetText("animal_race"),
                    GetText("animal_race_won", Format.Bold(winner.Username), winner.Animal.Icon));
            }

            ar.OnStartingFailed += Ar_OnStartingFailed;
            ar.OnStateUpdate += Ar_OnStateUpdate;
            ar.OnEnded += ArOnEnded;
            ar.OnStarted += Ar_OnStarted;
            eventHandler.MessageReceived += ClientMessageReceived;

            return ctx.Channel.SendConfirmAsync(GetText("animal_race"),
                GetText("animal_race_starting", options.StartTime),
                footer: GetText("animal_race_join_instr", await guildSettings.GetPrefix(ctx.Guild)));
        }

        private Task Ar_OnStarted(AnimalRace race)
            => ctx.Channel.SendConfirmAsync(GetText("animal_race"),
                race.Users.Count == race.MaxUsers ? GetText("animal_race_full") : GetText("animal_race_starting_with_x", race.Users.Count));

        private async Task Ar_OnStateUpdate(AnimalRace race)
        {
            var text = $@"|🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🔚|
{string.Join("\n", race.Users.Select(p =>
{
    var index = race.FinishedUsers.IndexOf(p);
    var extra = index == -1 ? "" : $"#{index + 1} {(index == 0 ? "🏆" : "")}";
    return $"{(int)(p.Progress / 60f * 100),-2}%|{new string('‣', p.Progress) + p.Animal.Icon + extra}";
}))}
|🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🏁🔚|";

            var msg = raceMessage;

            if (msg == null)
            {
                raceMessage = await ctx.Channel.SendConfirmAsync(text)
                    .ConfigureAwait(false);
            }
            else
            {
                await msg.ModifyAsync(x => x.Embed = new EmbedBuilder()
                        .WithTitle(GetText("animal_race"))
                        .WithDescription(text)
                        .WithOkColor()
                        .Build())
                    .ConfigureAwait(false);
            }
        }

        private Task Ar_OnStartingFailed(AnimalRace race)
        {
            Service.AnimalRaces.TryRemove(ctx.Guild.Id, out _);
            return ReplyErrorLocalizedAsync("animal_race_failed");
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
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
                {
                    await ctx.Channel.SendConfirmAsync(GetText("animal_race_join_bet", ctx.User.Mention,
                        user.Animal.Icon, amount + CurrencySign)).ConfigureAwait(false);
                }
                else
                {
                    await ctx.Channel
                        .SendConfirmAsync(GetText("animal_race_join", ctx.User.Mention, user.Animal.Icon))
                        .ConfigureAwait(false);
                }
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