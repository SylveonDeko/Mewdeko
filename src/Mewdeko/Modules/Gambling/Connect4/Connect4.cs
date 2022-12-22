using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;

// ReSharper disable PossibleInvalidOperationException

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

    public const int NumberOfColumns = 7;
    public const int NumberOfRows = 6;
    private readonly ICurrencyService cs;

    //state is bottom to top, left to right
    private readonly Field[] gameState = new Field[NumberOfRows * NumberOfColumns];

    private readonly SemaphoreSlim locker = new(1, 1);
    private readonly Options options;
    private readonly (ulong UserId, string Username)?[] players = new (ulong, string)?[2];
    private readonly MewdekoRandom rng;

    private Timer playerTimeoutTimer;

    public Connect4Game(ulong userId, string userName, Options options, ICurrencyService cs)
    {
        players[0] = (userId, userName);
        this.options = options;
        this.cs = cs;

        rng = new MewdekoRandom();
        for (var i = 0; i < NumberOfColumns * NumberOfRows; i++) gameState[i] = Field.Empty;
    }

    public Phase CurrentPhase { get; private set; } = Phase.Joining;

    public ImmutableArray<Field> GameState => gameState.ToImmutableArray();
    public ImmutableArray<(ulong UserId, string Username)?> Players => players.ToImmutableArray();

    public (ulong UserId, string Username) CurrentPlayer => CurrentPhase == Phase.P1Move
        ? players[0].Value
        : players[1].Value;

    public (ulong UserId, string Username) OtherPlayer => CurrentPhase == Phase.P2Move
        ? players[0].Value
        : players[1].Value;

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
            await locker.WaitAsync().ConfigureAwait(false);
            try
            {
                if (players[1] == null)
                {
                    var __ = OnGameFailedToStart.Invoke(this);
                    CurrentPhase = Phase.Ended;
                    await cs.AddAsync(players[0].Value.UserId, "Connect4-refund", options.Bet, true)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                locker.Release();
            }
        });
    }

    public async Task<bool> Join(ulong userId, string userName, int bet)
    {
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            if (CurrentPhase != Phase.Joining) //can't join if its not a joining phase
                return false;

            if (players[0].Value.UserId == userId) // same user can't join own game
                return false;

            if (bet != options.Bet) // can't join if bet amount is not the same
                return false;

            if (!await cs.RemoveAsync(userId, "Connect4-bet", bet, true)
                    .ConfigureAwait(false)) // user doesn't have enough money to gamble
            {
                return false;
            }

            if (rng.Next(0, 2) == 0) //rolling from 0-1, if number is 0, join as first player
            {
                players[1] = players[0];
                players[0] = (userId, userName);
            }
            else //else join as a second player
            {
                players[1] = (userId, userName);
            }

            CurrentPhase = Phase.P1Move; //start the game
            playerTimeoutTimer = new Timer(async _ =>
            {
                await locker.WaitAsync().ConfigureAwait(false);
                try
                {
                    EndGame(Result.OtherPlayerWon, OtherPlayer.UserId);
                }
                finally
                {
                    locker.Release();
                }
            }, null, TimeSpan.FromSeconds(options.TurnTimer), TimeSpan.FromSeconds(options.TurnTimer));
            var __ = OnGameStateUpdated.Invoke(this);

            return true;
        }
        finally
        {
            locker.Release();
        }
    }

    public async Task<bool> Input(ulong userId, int inputCol)
    {
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            inputCol--;
            if (CurrentPhase is Phase.Ended or Phase.Joining)
                return false;

            if (!((players[0].Value.UserId == userId && CurrentPhase == Phase.P1Move)
                  || (players[1].Value.UserId == userId && CurrentPhase == Phase.P2Move)))
            {
                return false;
            }

            if (inputCol is < 0 or > NumberOfColumns) //invalid input
                return false;

            if (IsColumnFull(inputCol)) //can't play there event?
                return false;

            var start = NumberOfRows * inputCol;
            for (var i = start; i < start + NumberOfRows; i++)
            {
                if (gameState[i] == Field.Empty)
                {
                    gameState[i] = GetPlayerPiece(userId);
                    break;
                }
            }

            //check winnning condition
            // ok, i'll go from [0-2] in rows (and through all columns) and check upward if 4 are connected

            for (var i = 0; i < NumberOfRows - 3; i++)
            {
                if (CurrentPhase == Phase.Ended)
                    break;

                for (var j = 0; j < NumberOfColumns; j++)
                {
                    if (CurrentPhase == Phase.Ended)
                        break;

                    var first = gameState[i + (j * NumberOfRows)];
                    if (first == Field.Empty) continue;
                    for (var k = 1; k < 4; k++)
                    {
                        var next = gameState[i + k + (j * NumberOfRows)];
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
            for (var i = 0; i < NumberOfColumns - 3; i++)
            {
                if (CurrentPhase == Phase.Ended)
                    break;

                for (var j = 0; j < NumberOfRows; j++)
                {
                    if (CurrentPhase == Phase.Ended)
                        break;

                    var first = gameState[j + (i * NumberOfRows)];
                    if (first != Field.Empty)
                    {
                        for (var k = 1; k < 4; k++)
                        {
                            var next = gameState[j + ((i + k) * NumberOfRows)];
                            if (next != first) continue;
                            if (k == 3)
                                EndGame(Result.CurrentPlayerWon, CurrentPlayer.UserId);
                            else break;
                        }
                    }
                }
            }

            //need to check diagonal now
            for (var col = 0; col < NumberOfColumns; col++)
            {
                if (CurrentPhase == Phase.Ended)
                    break;

                for (var row = 0; row < NumberOfRows; row++)
                {
                    if (CurrentPhase == Phase.Ended)
                        break;

                    var first = gameState[row + (col * NumberOfRows)];

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
                            if (curRow is >= NumberOfRows or < 0)
                                break;
                            if (curCol is < 0 or >= NumberOfColumns)
                                break;

                            var cur = gameState[curRow + (curCol * NumberOfRows)];
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
                            if (curRow is >= NumberOfRows or < 0)
                                break;
                            if (curCol is < 0 or >= NumberOfColumns)
                                break;

                            var cur = gameState[curRow + (curCol * NumberOfRows)];
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
            if (gameState.All(x => x != Field.Empty)) EndGame(Result.Draw, null);

            if (CurrentPhase != Phase.Ended)
            {
                CurrentPhase = CurrentPhase == Phase.P1Move ? Phase.P2Move : Phase.P1Move;

                ResetTimer();
            }

            var _ = OnGameStateUpdated.Invoke(this);
            return true;
        }
        finally
        {
            locker.Release();
        }
    }

    private void ResetTimer() =>
        playerTimeoutTimer.Change(TimeSpan.FromSeconds(options.TurnTimer),
            TimeSpan.FromSeconds(options.TurnTimer));

    private void EndGame(Result result, ulong? winId)
    {
        if (CurrentPhase == Phase.Ended)
            return;
        var _ = OnGameEnded.Invoke(this, result);
        CurrentPhase = Phase.Ended;

        if (result == Result.Draw)
        {
            cs.AddAsync(CurrentPlayer.UserId, "Connect4-draw", options.Bet, true);
            cs.AddAsync(OtherPlayer.UserId, "Connect4-draw", options.Bet, true);
            return;
        }

        if (winId != null)
            cs.AddAsync(winId.Value, "Connnect4-win", (long)(options.Bet * 1.98), true);
    }

    private Field GetPlayerPiece(ulong userId) =>
        players[0].Value.UserId == userId
            ? Field.P1
            : Field.P2;

    //column is full if there are no empty fields
    private bool IsColumnFull(int column)
    {
        var start = NumberOfRows * column;
        for (var i = start; i < start + NumberOfRows; i++)
        {
            if (gameState[i] == Field.Empty)
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