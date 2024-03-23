using System.Collections.Immutable;
using System.Threading;

namespace Mewdeko.Modules.Games.Common.Nunchi;

/// <summary>
/// Represents a Nunchi game.
/// </summary>
public sealed class NunchiGame : IDisposable
{
    /// <summary>
    /// Represents the phase of a Nunchi game.
    /// </summary>
    public enum Phase
    {
        /// <summary>
        /// Indicates the phase where players are joining the game.
        /// </summary>
        Joining,

        /// <summary>
        /// Indicates the phase where the game is actively being played.
        /// </summary>
        Playing,

        /// <summary>
        /// Indicates the phase where the game is waiting for the next round to start.
        /// </summary>
        WaitingForNextRound,

        /// <summary>
        /// Indicates the phase where the game has ended.
        /// </summary>
        Ended
    }

    /// <summary>
    /// Represents the number of milliseconds after which the game forcibly ends.
    /// </summary>
    private const int KillTimeout = 20 * 1000;

    /// <summary>
    /// Represents the number of milliseconds after which the next round of the game begins.
    /// </summary>
    private const int NextRoundTimeout = 5 * 1000;

    /// <summary>
    /// Semaphore to synchronize access to game state.
    /// </summary>
    private readonly SemaphoreSlim locker = new(1, 1);

    /// <summary>
    /// HashSet containing the participants who have already passed in the game.
    /// </summary>
    private readonly HashSet<(ulong Id, string Name)> passed = new();

    /// <summary>
    /// Timer to handle forcibly ending the game.
    /// </summary>
    private Timer killTimer;

    /// <summary>
    /// HashSet containing the participants of the game.
    /// </summary>
    private HashSet<(ulong Id, string Name)> participants = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="NunchiGame"/> class.
    /// </summary>
    /// <param name="creatorId">The ID of the player who created the game.</param>
    /// <param name="creatorName">The name of the player who created the game.</param>
    public NunchiGame(ulong creatorId, string creatorName) => participants.Add((creatorId, creatorName));

    /// <summary>
    /// Gets or sets the current number in the game.
    /// </summary>
    public int CurrentNumber { get; private set; } = new MewdekoRandom().Next(0, 100);

    /// <summary>
    /// Gets or sets the current phase of the game.
    /// </summary>
    public Phase CurrentPhase { get; private set; } = Phase.Joining;

    /// <summary>
    /// Gets the participants of the game as an immutable array.
    /// </summary>
    public ImmutableArray<(ulong Id, string Name)> Participants => participants.ToImmutableArray();

    /// <summary>
    /// Gets the count of participants in the game.
    /// </summary>
    public int ParticipantCount => participants.Count;

    /// <summary>
    /// Disposes resources used by the game.
    /// </summary>
    public void Dispose()
    {
        OnGameEnded = null;
        OnGameStarted = null;
        OnRoundEnded = null;
        OnRoundStarted = null;
        OnUserGuessed = null;
    }

    /// <summary>
    /// Event triggered when the game starts.
    /// </summary>
    public event Func<NunchiGame, Task> OnGameStarted;

    /// <summary>
    /// Event triggered when a round of the game starts.
    /// </summary>
    public event Func<NunchiGame, int, Task> OnRoundStarted;

    /// <summary>
    /// Event triggered when a user guesses a number.
    /// </summary>
    public event Func<NunchiGame, Task> OnUserGuessed;

    /// <summary>
    /// Event triggered when a round of the game ends.
    /// </summary>
    public event Func<NunchiGame, (ulong Id, string Name)?, Task> OnRoundEnded; // tuple of the user who failed

    /// <summary>
    /// Event triggered when the game ends.
    /// </summary>
    public event Func<NunchiGame, string, Task> OnGameEnded; // name of the user who won

    /// <summary>
    /// Allows a user to join the game.
    /// </summary>
    /// <param name="userId">The ID of the user joining the game.</param>
    /// <param name="userName">The name of the user joining the game.</param>
    /// <returns>True if the user successfully joined, otherwise false.</returns>
    public async Task<bool> Join(ulong userId, string userName)
    {
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            if (CurrentPhase != Phase.Joining)
                return false;

            return participants.Add((userId, userName));
        }
        finally
        {
            locker.Release();
        }
    }

    /// <summary>
    /// Initializes the game, allowing it to start.
    /// </summary>
    /// <returns>True if the game was successfully initialized, otherwise false.</returns>
    public async Task<bool> Initialize()
    {
        CurrentPhase = Phase.Joining;
        await Task.Delay(30000).ConfigureAwait(false);
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            if (participants.Count < 3)
            {
                CurrentPhase = Phase.Ended;
                return false;
            }

            killTimer = new Timer(async _ =>
            {
                await locker.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (CurrentPhase != Phase.Playing)
                        return;

                    //if some players took too long to type a number, boot them all out and start a new round
                    participants = new HashSet<(ulong, string)>(passed);
                    EndRound();
                }
                finally
                {
                    locker.Release();
                }
            }, null, KillTimeout, KillTimeout);

            CurrentPhase = Phase.Playing;
            var _ = OnGameStarted.Invoke(this);
            var __ = OnRoundStarted.Invoke(this, CurrentNumber);
            return true;
        }
        finally
        {
            locker.Release();
        }
    }

    /// <summary>
    /// Processes the input of a user during the game.
    /// </summary>
    /// <param name="userId">The ID of the user providing the input.</param>
    /// <param name="userName">The name of the user providing the input.</param>
    /// <param name="input">The input number provided by the user.</param>
    public async Task Input(ulong userId, string userName, int input)
    {
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            if (CurrentPhase != Phase.Playing)
                return;

            var userTuple = (Id: userId, Name: userName);

            // if the user is not a member of the race,
            // or he already successfully typed the number
            // ignore the input
            if (!participants.Contains(userTuple) || !passed.Add(userTuple))
                return;

            //if the number is correct
            if (CurrentNumber == input - 1)
            {
                //increment current number
                ++CurrentNumber;
                if (passed.Count == participants.Count - 1)
                {
                    // if only n players are left, and n - 1 type the correct number, round is over

                    // if only 2 players are left, game is over
                    if (participants.Count == 2)
                    {
                        killTimer.Change(Timeout.Infinite, Timeout.Infinite);
                        CurrentPhase = Phase.Ended;
                        var _ = OnGameEnded.Invoke(this, userTuple.Name);
                    }
                    else // else just start the new round without the user who was the last
                    {
                        var failure = participants.Except(passed).First();

                        await OnUserGuessed.Invoke(this);
                        EndRound(failure);
                        return;
                    }
                }

                await OnUserGuessed.Invoke(this);
            }
            else
            {
                //if the user failed

                EndRound(userTuple);
            }
        }
        finally
        {
            locker.Release();
        }
    }

    private void EndRound((ulong, string)? failure = null)
    {
        killTimer.Change(KillTimeout, KillTimeout);
        CurrentNumber = new MewdekoRandom().Next(0, 100); // reset the counter
        passed.Clear(); // reset all users who passed (new round starts)
        if (failure != null)
            participants.Remove(failure.Value); // remove the dude who failed from the list of players

        var __ = OnRoundEnded.Invoke(this, failure);
        if (participants.Count <= 1) // means we have a winner or everyone was booted out
        {
            killTimer.Change(Timeout.Infinite, Timeout.Infinite);
            CurrentPhase = Phase.Ended;
            var _ = OnGameEnded.Invoke(this, participants.Count > 0 ? participants.First().Name : null);
            return;
        }

        CurrentPhase = Phase.WaitingForNextRound;
        Task.Run(async () =>
        {
            await Task.Delay(NextRoundTimeout).ConfigureAwait(false);
            CurrentPhase = Phase.Playing;
            var ___ = OnRoundStarted.Invoke(this, CurrentNumber);
        });
    }
}