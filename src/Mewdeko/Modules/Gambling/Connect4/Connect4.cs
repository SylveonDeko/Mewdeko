using CommandLine;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Gambling.Connect4;

public sealed class Connect4Game : IDisposable
{
    public enum Field //temporary most likely
    {
        Empty,
        P1,
        P2
    }

    public enum Phase
    {
        Joining, // waiting for second player to join
        P1Move,
        P2Move,
        Ended
    }

    public enum Result
    {
        Draw,
        CurrentPlayerWon,
        OtherPlayerWon
    }

    public const int NUMBER_OF_COLUMNS = 7;
    public const int NUMBER_OF_ROWS = 6;
    private readonly ICurrencyService _cs;

    //state is bottom to top, left to right
    private readonly Field[] _gameState = new Field[NUMBER_OF_ROWS * NUMBER_OF_COLUMNS];

    private readonly SemaphoreSlim _locker = new(1, 1);
    private readonly Options _options;
    private readonly (ulong UserId, string Username)?[] _players = new (ulong, string)?[2];
    private readonly MewdekoRandom _rng;

    private Timer playerTimeoutTimer;

    /* [ ][ ][ ][ ][ ][ ]
     * [ ][ ][ ][ ][ ][ ]
     * [ ][ ][ ][ ][ ][ ]
     * [ ][ ][ ][ ][ ][ ]
     * [ ][ ][ ][ ][ ][ ]
     * [ ][ ][ ][ ][ ][ ]
     * [ ][ ][ ][ ][ ][ ]
     */

    public Connect4Game(ulong userId, string userName, Options options, ICurrencyService cs)
    {
        _players[0] = (userId, userName);
        _options = options;
        _cs = cs;

        _rng = new MewdekoRandom();
        for (var i = 0; i < NUMBER_OF_COLUMNS * NUMBER_OF_ROWS; i++) _gameState[i] = Field.Empty;
    }

    public Phase CurrentPhase { get; private set; } = Phase.Joining;

    public ImmutableArray<Field> GameState => _gameState.ToImmutableArray();
    public ImmutableArray<(ulong UserId, string Username)?> Players => _players.ToImmutableArray();

    public (ulong UserId, string Username) CurrentPlayer => CurrentPhase == Phase.P1Move
        ? _players[0].Value
        : _players[1].Value;

    public (ulong UserId, string Username) OtherPlayer => CurrentPhase == Phase.P2Move
        ? _players[0].Value
        : _players[1].Value;

    public void Dispose()
    {
        OnGameFailedToStart = null;
        OnGameStateUpdated = null;
        OnGameEnded = null;
        playerTimeoutTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    //public event Func<Connect4Game, Task> OnGameStarted;
    public event Func<Connect4Game, Task> OnGameStateUpdated;
    public event Func<Connect4Game, Task> OnGameFailedToStart;
    public event Func<Connect4Game, Result, Task> OnGameEnded;

    public void Initialize()
    {
        if (CurrentPhase != Phase.Joining)
            return;
        _ = Task.Run(async () =>
        {
            await Task.Delay(15000).ConfigureAwait(false);
            await _locker.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_players[1] == null)
                {
                    var __ = OnGameFailedToStart.Invoke(this);
                    CurrentPhase = Phase.Ended;
                    await _cs.AddAsync(_players[0].Value.UserId, "Connect4-refund", _options.Bet, true)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                _locker.Release();
            }
        });
    }

    public async Task<bool> Join(ulong userId, string userName, int bet)
    {
        await _locker.WaitAsync().ConfigureAwait(false);
        try
        {
            if (CurrentPhase != Phase.Joining) //can't join if its not a joining phase
                return false;

            if (_players[0].Value.UserId == userId) // same user can't join own game
                return false;

            if (bet != _options.Bet) // can't join if bet amount is not the same
                return false;

            if (!await _cs.RemoveAsync(userId, "Connect4-bet", bet, true)
                    .ConfigureAwait(false)) // user doesn't have enough money to gamble
            {
                return false;
            }

            if (_rng.Next(0, 2) == 0) //rolling from 0-1, if number is 0, join as first player
            {
                _players[1] = _players[0];
                _players[0] = (userId, userName);
            }
            else //else join as a second player
            {
                _players[1] = (userId, userName);
            }

            CurrentPhase = Phase.P1Move; //start the game
            playerTimeoutTimer = new Timer(async _ =>
            {
                await _locker.WaitAsync().ConfigureAwait(false);
                try
                {
                    EndGame(Result.OtherPlayerWon, OtherPlayer.UserId);
                }
                finally
                {
                    _locker.Release();
                }
            }, null, TimeSpan.FromSeconds(_options.TurnTimer), TimeSpan.FromSeconds(_options.TurnTimer));
            var __ = OnGameStateUpdated.Invoke(this);

            return true;
        }
        finally
        {
            _locker.Release();
        }
    }

    public async Task<bool> Input(ulong userId, int inputCol)
    {
        await _locker.WaitAsync().ConfigureAwait(false);
        try
        {
            inputCol--;
            if (CurrentPhase is Phase.Ended or Phase.Joining)
                return false;

            if (!((_players[0].Value.UserId == userId && CurrentPhase == Phase.P1Move)
                  || (_players[1].Value.UserId == userId && CurrentPhase == Phase.P2Move)))
            {
                return false;
            }

            if (inputCol is < 0 or > NUMBER_OF_COLUMNS) //invalid input
                return false;

            if (IsColumnFull(inputCol)) //can't play there event?
                return false;

            var start = NUMBER_OF_ROWS * inputCol;
            for (var i = start; i < start + NUMBER_OF_ROWS; i++)
            {
                if (_gameState[i] == Field.Empty)
                {
                    _gameState[i] = GetPlayerPiece(userId);
                    break;
                }
            }

            //check winnning condition
            // ok, i'll go from [0-2] in rows (and through all columns) and check upward if 4 are connected

            for (var i = 0; i < NUMBER_OF_ROWS - 3; i++)
            {
                if (CurrentPhase == Phase.Ended)
                    break;

                for (var j = 0; j < NUMBER_OF_COLUMNS; j++)
                {
                    if (CurrentPhase == Phase.Ended)
                        break;

                    var first = _gameState[i + (j * NUMBER_OF_ROWS)];
                    if (first == Field.Empty) continue;
                    for (var k = 1; k < 4; k++)
                    {
                        var next = _gameState[i + k + (j * NUMBER_OF_ROWS)];
                        if (next == first)
                        {
                            if (k == 3)
                                EndGame(Result.CurrentPlayerWon, CurrentPlayer.UserId);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            // i'll go [0-1] in columns (and through all rows) and check to the right if 4 are connected
            for (var i = 0; i < NUMBER_OF_COLUMNS - 3; i++)
            {
                if (CurrentPhase == Phase.Ended)
                    break;

                for (var j = 0; j < NUMBER_OF_ROWS; j++)
                {
                    if (CurrentPhase == Phase.Ended)
                        break;

                    var first = _gameState[j + (i * NUMBER_OF_ROWS)];
                    if (first != Field.Empty)
                    {
                        for (var k = 1; k < 4; k++)
                        {
                            var next = _gameState[j + ((i + k) * NUMBER_OF_ROWS)];
                            if (next != first) continue;
                            if (k == 3)
                                EndGame(Result.CurrentPlayerWon, CurrentPlayer.UserId);
                            else break;
                        }
                    }
                }
            }

            //need to check diagonal now
            for (var col = 0; col < NUMBER_OF_COLUMNS; col++)
            {
                if (CurrentPhase == Phase.Ended)
                    break;

                for (var row = 0; row < NUMBER_OF_ROWS; row++)
                {
                    if (CurrentPhase == Phase.Ended)
                        break;

                    var first = _gameState[row + (col * NUMBER_OF_ROWS)];

                    if (first != Field.Empty)
                    {
                        var same = 1;

                        //top left
                        for (var i = 1; i < 4; i++)
                        {
                            //while going top left, rows are increasing, columns are decreasing
                            var curRow = row + i;
                            var curCol = col - i;

                            //check if current values are in range
                            if (curRow is >= NUMBER_OF_ROWS or < 0)
                                break;
                            if (curCol is < 0 or >= NUMBER_OF_COLUMNS)
                                break;

                            var cur = _gameState[curRow + (curCol * NUMBER_OF_ROWS)];
                            if (cur == first)
                                same++;
                            else break;
                        }

                        if (same == 4)
                        {
                            EndGame(Result.CurrentPlayerWon, CurrentPlayer.UserId);
                            break;
                        }

                        same = 1;

                        //top right
                        for (var i = 1; i < 4; i++)
                        {
                            //while going top right, rows are increasing, columns are increasing
                            var curRow = row + i;
                            var curCol = col + i;

                            //check if current values are in range
                            if (curRow is >= NUMBER_OF_ROWS or < 0)
                                break;
                            if (curCol is < 0 or >= NUMBER_OF_COLUMNS)
                                break;

                            var cur = _gameState[curRow + (curCol * NUMBER_OF_ROWS)];
                            if (cur == first)
                                same++;
                            else break;
                        }

                        if (same == 4)
                        {
                            EndGame(Result.CurrentPlayerWon, CurrentPlayer.UserId);
                            break;
                        }
                    }
                }
            }

            //check draw? if it's even possible
            if (_gameState.All(x => x != Field.Empty)) EndGame(Result.Draw, null);

            if (CurrentPhase != Phase.Ended)
            {
                if (CurrentPhase == Phase.P1Move)
                    CurrentPhase = Phase.P2Move;
                else
                    CurrentPhase = Phase.P1Move;

                ResetTimer();
            }

            var _ = OnGameStateUpdated.Invoke(this);
            return true;
        }
        finally
        {
            _locker.Release();
        }
    }

    private void ResetTimer() =>
        playerTimeoutTimer.Change(TimeSpan.FromSeconds(_options.TurnTimer),
            TimeSpan.FromSeconds(_options.TurnTimer));

    private void EndGame(Result result, ulong? winId)
    {
        if (CurrentPhase == Phase.Ended)
            return;
        var _ = OnGameEnded.Invoke(this, result);
        CurrentPhase = Phase.Ended;

        if (result == Result.Draw)
        {
            _cs.AddAsync(CurrentPlayer.UserId, "Connect4-draw", _options.Bet, true);
            _cs.AddAsync(OtherPlayer.UserId, "Connect4-draw", _options.Bet, true);
            return;
        }

        if (winId != null)
            _cs.AddAsync(winId.Value, "Connnect4-win", (long)(_options.Bet * 1.98), true);
    }

    private Field GetPlayerPiece(ulong userId) =>
        _players[0].Value.UserId == userId
            ? Field.P1
            : Field.P2;

    //column is full if there are no empty fields
    private bool IsColumnFull(int column)
    {
        var start = NUMBER_OF_ROWS * column;
        for (var i = start; i < start + NUMBER_OF_ROWS; i++)
        {
            if (_gameState[i] == Field.Empty)
                return false;
        }

        return true;
    }

    public class Options : IMewdekoCommandOptions
    {
        [Option('t', "turn-timer", Required = false, Default = 15,
            HelpText = "Turn time in seconds. It has to be between 5 and 60. Default 15.")]
        public int TurnTimer { get; set; } = 15;

        [Option('b', "bet", Required = false, Default = 0, HelpText = "Amount you bet. Default 0.")]
        public int Bet { get; set; }

        public void NormalizeOptions()
        {
            if (TurnTimer is < 5 or > 60)
                TurnTimer = 15;

            if (Bet < 0)
                Bet = 0;
        }
    }
}