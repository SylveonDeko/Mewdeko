using CommandLine;
using Mewdeko.Services.strings;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Games.Common;

public class TicTacToe
{
    private readonly ITextChannel _channel;
    private readonly DiscordSocketClient _client;
    private readonly SemaphoreSlim _moveLock;

    private readonly string[] _numbers =
        {":one:", ":two:", ":three:", ":four:", ":five:", ":six:", ":seven:", ":eight:", ":nine:"};

    private readonly Options _options;
    private readonly int?[,] _state;
    private readonly IBotStrings _strings;
    private readonly IGuildUser?[] _users;
    private int curUserIndex;
    private Phase phase;

    private IUserMessage? previousMessage;
    private Timer timeoutTimer;

    private IGuildUser? winner;

    public TicTacToe(IBotStrings strings, DiscordSocketClient client, ITextChannel channel,
        IGuildUser firstUser, Options options)
    {
        _channel = channel;
        _strings = strings;
        _client = client;
        _options = options;

        _users = new[] { firstUser, null };
        _state = new int?[,]
        {
            {null, null, null},
            {null, null, null},
            {null, null, null}
        };

        phase = Phase.Starting;
        _moveLock = new SemaphoreSlim(1, 1);
    }

    public event Action<TicTacToe> OnEnded;

    private string? GetText(string? key, params object?[] replacements) => _strings.GetText(key, _channel.GuildId, replacements);

    public string GetState()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < _state.GetLength(0); i++)
        {
            for (var j = 0; j < _state.GetLength(1); j++)
            {
                sb.Append(_state[i, j] == null ? _numbers[(i * 3) + j] : GetIcon(_state[i, j]));
                if (j < _state.GetLength(1) - 1)
                    sb.Append('┃');
            }

            if (i < _state.GetLength(0) - 1)
                sb.AppendLine("\n──────────");
        }

        return sb.ToString();
    }

    public EmbedBuilder GetEmbed(string? title = null)
    {
        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Environment.NewLine + GetState())
            .WithAuthor(eab => eab.WithName(GetText("vs", _users[0], _users[1])));

        if (!string.IsNullOrWhiteSpace(title))
            embed.WithTitle(title);

        if (winner == null)
        {
            if (phase == Phase.Ended)
                embed.WithFooter(efb => efb.WithText(GetText("ttt_no_moves")));
            else
                embed.WithFooter(efb => efb.WithText(GetText("ttt_users_move", _users[curUserIndex])));
        }
        else
        {
            embed.WithFooter(efb => efb.WithText(GetText("ttt_has_won", winner)));
        }

        return embed;
    }

    private static string GetIcon(int? val) =>
        val switch
        {
            0 => "❌",
            1 => "⭕",
            2 => "❎",
            3 => "🅾",
            _ => "⬛"
        };

    public async Task Start(IGuildUser? user)
    {
        if (phase is Phase.Started or Phase.Ended)
        {
            await _channel.SendErrorAsync(user.Mention + GetText("ttt_already_running")).ConfigureAwait(false);
            return;
        }

        if (_users[0] == user)
        {
            await _channel.SendErrorAsync(user.Mention + GetText("ttt_against_yourself")).ConfigureAwait(false);
            return;
        }

        _users[1] = user;

        phase = Phase.Started;

        timeoutTimer = new Timer(async _ =>
        {
            await _moveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (phase == Phase.Ended)
                    return;

                phase = Phase.Ended;
                if (_users[1].Username != null)
                {
                    winner = _users[curUserIndex ^= 1];
                    var del = previousMessage?.DeleteAsync();
                    try
                    {
                        await _channel.EmbedAsync(GetEmbed(GetText("ttt_time_expired"))).ConfigureAwait(false);
                        if (del != null)
                            await del.ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                OnEnded.Invoke(this);
            }
            catch
            {
                // ignored
            }
            finally
            {
                _moveLock.Release();
            }
        }, null, _options.TurnTimer * 1000, Timeout.Infinite);

        _client.MessageReceived += Client_MessageReceived;

        previousMessage = await _channel.EmbedAsync(GetEmbed(GetText("game_started"))).ConfigureAwait(false);
    }

    private bool IsDraw()
    {
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                if (_state[i, j] == null)
                    return false;
            }
        }

        return true;
    }

    private Task Client_MessageReceived(SocketMessage msg)
    {
        _ = Task.Run(async () =>
        {
            await _moveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var curUser = _users[curUserIndex];
                if (phase == Phase.Ended || msg.Author?.Id != curUser.Id)
                    return;

                if (int.TryParse(msg.Content, out var index) &&
                    --index >= 0 &&
                    index <= 9 &&
                    _state[index / 3, index % 3] == null)
                {
                    _state[index / 3, index % 3] = curUserIndex;

                    // i'm lazy
                    if (_state[index / 3, 0] == _state[index / 3, 1] &&
                        _state[index / 3, 1] == _state[index / 3, 2])
                    {
                        _state[index / 3, 0] = curUserIndex + 2;
                        _state[index / 3, 1] = curUserIndex + 2;
                        _state[index / 3, 2] = curUserIndex + 2;

                        phase = Phase.Ended;
                    }
                    else if (_state[0, index % 3] == _state[1, index % 3] &&
                             _state[1, index % 3] == _state[2, index % 3])
                    {
                        _state[0, index % 3] = curUserIndex + 2;
                        _state[1, index % 3] = curUserIndex + 2;
                        _state[2, index % 3] = curUserIndex + 2;

                        phase = Phase.Ended;
                    }
                    else if (curUserIndex == _state[0, 0] && _state[0, 0] == _state[1, 1] &&
                             _state[1, 1] == _state[2, 2])
                    {
                        _state[0, 0] = curUserIndex + 2;
                        _state[1, 1] = curUserIndex + 2;
                        _state[2, 2] = curUserIndex + 2;

                        phase = Phase.Ended;
                    }
                    else if (curUserIndex == _state[0, 2] && _state[0, 2] == _state[1, 1] &&
                             _state[1, 1] == _state[2, 0])
                    {
                        _state[0, 2] = curUserIndex + 2;
                        _state[1, 1] = curUserIndex + 2;
                        _state[2, 0] = curUserIndex + 2;

                        phase = Phase.Ended;
                    }

                    var reason = "";

                    if (phase == Phase.Ended) // if user won, stop receiving moves
                    {
                        reason = GetText("ttt_matched_three");
                        winner = _users[curUserIndex];
                        _client.MessageReceived -= Client_MessageReceived;
                        OnEnded.Invoke(this);
                    }
                    else if (IsDraw())
                    {
                        reason = GetText("ttt_a_draw");
                        phase = Phase.Ended;
                        _client.MessageReceived -= Client_MessageReceived;
                        OnEnded.Invoke(this);
                    }

                    await Task.Run(async () =>
                    {
                        var del1 = msg.DeleteAsync();
                        var del2 = previousMessage?.DeleteAsync();
                        try
                        {
                            previousMessage = await _channel.EmbedAsync(GetEmbed(reason)).ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }

                        try
                        {
                            await del1.ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }

                        try
                        {
                            if (del2 != null) await del2.ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }
                    });
                    curUserIndex ^= 1;

                    timeoutTimer.Change(_options.TurnTimer * 1000, Timeout.Infinite);
                }
            }
            finally
            {
                _moveLock.Release();
            }
        });

        return Task.CompletedTask;
    }

    public class Options : IMewdekoCommandOptions
    {
        [Option('t', "turn-timer", Required = false, Default = 15, HelpText = "Turn time in seconds. Default 15.")]
        public int TurnTimer { get; set; } = 15;

        public void NormalizeOptions()
        {
            if (TurnTimer is < 5 or > 60)
                TurnTimer = 15;
        }
    }

    private enum Phase
    {
        Starting,
        Started,
        Ended
    }
}