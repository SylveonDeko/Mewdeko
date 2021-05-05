using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Common.Attributes;
using NadekoBot.Core.Common;
using NadekoBot.Core.Modules.Gambling.Common;
using NadekoBot.Core.Services;
using NadekoBot.Extensions;
using NadekoBot.Modules.Gambling.Common.Connect4;
using NadekoBot.Modules.Gambling.Services;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
        public class Connect4Commands : GamblingSubmodule<GamblingService>
        {
            private readonly DiscordSocketClient _client;
            private readonly ICurrencyService _cs;
            private static readonly string[] numbers = new string[] { ":one:", ":two:", ":three:", ":four:", ":five:", ":six:", ":seven:", ":eight:" };

            public Connect4Commands(DiscordSocketClient client, ICurrencyService cs)
            {
                _client = client;
                _cs = cs;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [NadekoOptionsAttribute(typeof(Connect4Game.Options))]
            public async Task Connect4(params string[] args)
            {
                var (options, _) = OptionsParser.ParseFrom(new Connect4Game.Options(), args);
                if (!await CheckBetOptional(options.Bet).ConfigureAwait(false))
                    return;

                var newGame = new Connect4Game(ctx.User.Id, ctx.User.ToString(), options, _cs);
                Connect4Game game;
                if ((game = _service.Connect4Games.GetOrAdd(ctx.Channel.Id, newGame)) != newGame)
                {
                    if (game.CurrentPhase != Connect4Game.Phase.Joining)
                        return;

                    newGame.Dispose();
                    //means game already exists, try to join
                    var joined = await game.Join(ctx.User.Id, ctx.User.ToString(), options.Bet).ConfigureAwait(false);
                    return;
                }

                if (options.Bet > 0)
                {
                    if (!await _cs.RemoveAsync(ctx.User.Id, "Connect4-bet", options.Bet, true).ConfigureAwait(false))
                    {
                        await ReplyErrorLocalizedAsync("not_enough", Bc.BotConfig.CurrencySign).ConfigureAwait(false);
                        _service.Connect4Games.TryRemove(ctx.Channel.Id, out _);
                        game.Dispose();
                        return;
                    }
                }

                game.OnGameStateUpdated += Game_OnGameStateUpdated;
                game.OnGameFailedToStart += Game_OnGameFailedToStart;
                game.OnGameEnded += Game_OnGameEnded;
                _client.MessageReceived += _client_MessageReceived;

                game.Initialize();
                if (options.Bet == 0)
                {
                    await ReplyConfirmLocalizedAsync("connect4_created").ConfigureAwait(false);
                }
                else
                {
                    await ReplyConfirmLocalizedAsync("connect4_created_bet", options.Bet + Bc.BotConfig.CurrencySign).ConfigureAwait(false);
                }

                Task _client_MessageReceived(SocketMessage arg)
                {
                    if (ctx.Channel.Id != arg.Channel.Id)
                        return Task.CompletedTask;

                    var _ = Task.Run(async () =>
                    {
                        bool success = false;
                        if (int.TryParse(arg.Content, out var col))
                        {
                            success = await game.Input(arg.Author.Id, col).ConfigureAwait(false);
                        }

                        if (success)
                            try { await arg.DeleteAsync().ConfigureAwait(false); } catch { }
                        else
                        {
                            if (game.CurrentPhase == Connect4Game.Phase.Joining
                                || game.CurrentPhase == Connect4Game.Phase.Ended)
                            {
                                return;
                            }
                            RepostCounter++;
                            if (RepostCounter == 0)
                                try { msg = await ctx.Channel.SendMessageAsync("", embed: (Embed)msg.Embeds.First()).ConfigureAwait(false); } catch { }
                        }
                    });
                    return Task.CompletedTask;
                }

                Task Game_OnGameFailedToStart(Connect4Game arg)
                {
                    if (_service.Connect4Games.TryRemove(ctx.Channel.Id, out var toDispose))
                    {
                        _client.MessageReceived -= _client_MessageReceived;
                        toDispose.Dispose();
                    }
                    return ErrorLocalizedAsync("connect4_failed_to_start");
                }

                Task Game_OnGameEnded(Connect4Game arg, Connect4Game.Result result)
                {
                    if (_service.Connect4Games.TryRemove(ctx.Channel.Id, out var toDispose))
                    {
                        _client.MessageReceived -= _client_MessageReceived;
                        toDispose.Dispose();
                    }

                    string title;
                    if (result == Connect4Game.Result.CurrentPlayerWon)
                    {
                        title = GetText("connect4_won", Format.Bold(arg.CurrentPlayer.Username), Format.Bold(arg.OtherPlayer.Username));
                    }
                    else if (result == Connect4Game.Result.OtherPlayerWon)
                    {
                        title = GetText("connect4_won", Format.Bold(arg.OtherPlayer.Username), Format.Bold(arg.CurrentPlayer.Username));
                    }
                    else
                        title = GetText("connect4_draw");

                    return msg.ModifyAsync(x => x.Embed = new EmbedBuilder()
                        .WithTitle(title)
                        .WithDescription(GetGameStateText(game))
                        .WithOkColor()
                        .Build());
                }
            }

            private IUserMessage msg;

            private int _repostCounter = 0;
            private int RepostCounter
            {
                get => _repostCounter;
                set
                {
                    if (value < 0 || value > 7)
                        _repostCounter = 0;
                    else _repostCounter = value;
                }
            }

            private async Task Game_OnGameStateUpdated(Connect4Game game)
            {
                var embed = new EmbedBuilder()
                    .WithTitle($"{game.CurrentPlayer.Username} vs {game.OtherPlayer.Username}")
                    .WithDescription(GetGameStateText(game))
                    .WithOkColor();


                if (msg == null)
                    msg = await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
                else
                    await msg.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
            }

            private string GetGameStateText(Connect4Game game)
            {
                var sb = new StringBuilder();

                if (game.CurrentPhase == Connect4Game.Phase.P1Move ||
                    game.CurrentPhase == Connect4Game.Phase.P2Move)
                    sb.AppendLine(GetText("connect4_player_to_move", Format.Bold(game.CurrentPlayer.Username)));

                for (int i = Connect4Game.NumberOfRows; i > 0; i--)
                {
                    for (int j = 0; j < Connect4Game.NumberOfColumns; j++)
                    {
                        var cur = game.GameState[i + (j * Connect4Game.NumberOfRows) - 1];

                        if (cur == Connect4Game.Field.Empty)
                            sb.Append("⚫"); //black circle
                        else if (cur == Connect4Game.Field.P1)
                            sb.Append("🔴"); //red circle
                        else
                            sb.Append("🔵"); //blue circle
                    }
                    sb.AppendLine();
                }

                for (int i = 0; i < Connect4Game.NumberOfColumns; i++)
                {
                    sb.Append(numbers[i]);
                }
                return sb.ToString();
            }
        }
    }
}
