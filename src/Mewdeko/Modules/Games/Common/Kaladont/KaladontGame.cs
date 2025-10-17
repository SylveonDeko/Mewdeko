using System.Collections.Immutable;
using System.Threading;
using CommandLine;

namespace Mewdeko.Modules.Games.Common.Kaladont;

/// <summary>
///     Represents a Kaladont word game.
/// </summary>
public sealed class KaladontGame : IDisposable
{
    /// <summary>
    ///     Represents the phase of a Kaladont game.
    /// </summary>
    public enum Phase
    {
        /// <summary>
        ///     Indicates the phase where players are joining the game.
        /// </summary>
        Joining,

        /// <summary>
        ///     Indicates the phase where the game is actively being played.
        /// </summary>
        Playing,

        /// <summary>
        ///     Indicates the phase where the game has ended.
        /// </summary>
        Ended
    }

    /// <summary>
    ///     Represents the result of a word validation.
    /// </summary>
    public enum ValidationResult
    {
        /// <summary>
        ///     The word is valid.
        /// </summary>
        Valid,

        /// <summary>
        ///     The word is too short (less than 3 characters).
        /// </summary>
        TooShort,

        /// <summary>
        ///     The word was already used in this game.
        /// </summary>
        AlreadyUsed,

        /// <summary>
        ///     The word doesn't start with the required 2 letters.
        /// </summary>
        WrongLetters,

        /// <summary>
        ///     The word creates a loop (starts and ends with same 2 letters).
        /// </summary>
        KaladontLoop,

        /// <summary>
        ///     The word was not found in the dictionary.
        /// </summary>
        NotInDictionary
    }

    private readonly HashSet<string> dictionary;

    private readonly SemaphoreSlim locker = new(1, 1);
    private readonly List<KaladontPlayer> players = [];
    private readonly HashSet<string> usedWords = new(StringComparer.OrdinalIgnoreCase);
    private int currentPlayerIndex;
    private Timer? turnTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="KaladontGame" /> class.
    /// </summary>
    /// <param name="options">The game options.</param>
    /// <param name="startingWord">The starting word for the game.</param>
    /// <param name="dictionaryWords">The dictionary of valid words.</param>
    public KaladontGame(Options options, string startingWord, HashSet<string> dictionaryWords)
    {
        Opts = options;
        CurrentWord = startingWord.Trim().ToLowerInvariant();
        usedWords.Add(CurrentWord);
        dictionary = dictionaryWords;
    }

    /// <summary>
    ///     Gets the current phase of the game.
    /// </summary>
    public Phase CurrentPhase { get; private set; } = Phase.Joining;

    /// <summary>
    ///     Gets the current word in play.
    /// </summary>
    public string CurrentWord { get; private set; } = string.Empty;

    /// <summary>
    ///     Gets the current player.
    /// </summary>
    public KaladontPlayer? CurrentPlayer
    {
        get
        {
            return players.Count > 0 && currentPlayerIndex < players.Count ? players[currentPlayerIndex] : null;
        }
    }

    /// <summary>
    ///     Gets the game options.
    /// </summary>
    public Options Opts { get; }

    /// <summary>
    ///     Gets the list of players in the game.
    /// </summary>
    public ImmutableArray<KaladontPlayer> Players
    {
        get
        {
            return [..players];
        }
    }

    /// <summary>
    ///     Gets the count of used words.
    /// </summary>
    public int UsedWordsCount
    {
        get
        {
            return usedWords.Count;
        }
    }

    /// <summary>
    ///     Gets the last few used words for display.
    /// </summary>
    public ImmutableArray<string> RecentWords
    {
        get
        {
            return usedWords.TakeLast(5).ToImmutableArray();
        }
    }

    /// <summary>
    ///     Disposes resources used by the game.
    /// </summary>
    public void Dispose()
    {
        CurrentPhase = Phase.Ended;
        turnTimer?.Dispose();
        turnTimer = null;
        OnGameStarted = null;
        OnPlayerTurn = null;
        OnWordPlayed = null;
        OnPlayerEliminated = null;
        OnGameEnded = null;
        players.Clear();
        usedWords.Clear();
        locker.Dispose();
    }

    /// <summary>
    ///     Event triggered when the game starts.
    /// </summary>
    public event Func<KaladontGame, Task>? OnGameStarted;

    /// <summary>
    ///     Event triggered when a player's turn begins.
    /// </summary>
    public event Func<KaladontGame, KaladontPlayer, Task>? OnPlayerTurn;

    /// <summary>
    ///     Event triggered when a word is successfully played.
    /// </summary>
    public event Func<KaladontGame, KaladontPlayer, string, Task>? OnWordPlayed;

    /// <summary>
    ///     Event triggered when a player is eliminated.
    /// </summary>
    public event Func<KaladontGame, KaladontPlayer, string, Task>? OnPlayerEliminated;

    /// <summary>
    ///     Event triggered when the game ends.
    /// </summary>
    public event Func<KaladontGame, KaladontPlayer?, Task>? OnGameEnded;

    /// <summary>
    ///     Allows a player to join the game during the joining phase.
    /// </summary>
    /// <param name="userId">The user ID of the player.</param>
    /// <param name="userName">The username of the player.</param>
    /// <returns>True if the player successfully joined; otherwise, false.</returns>
    public async Task<bool> Join(ulong userId, string userName)
    {
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            if (CurrentPhase != Phase.Joining)
                return false;

            var player = new KaladontPlayer(userId, userName);

            // Check if player already joined
            if (players.Any(p => p.UserId == userId))
                return false;

            players.Add(player);
            return true;
        }
        finally
        {
            locker.Release();
        }
    }

    /// <summary>
    ///     Initializes the game after the joining phase.
    /// </summary>
    /// <returns>True if the game was successfully initialized; otherwise, false.</returns>
    public async Task<bool> Initialize()
    {
        CurrentPhase = Phase.Joining;
        await Task.Delay(Opts.JoinTime * 1000).ConfigureAwait(false);

        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            if (players.Count < Opts.MinPlayers)
            {
                CurrentPhase = Phase.Ended;
                return false;
            }

            CurrentPhase = Phase.Playing;
            currentPlayerIndex = 0;

            // Start turn timer
            turnTimer = new Timer(async _ => await HandleTurnTimeout().ConfigureAwait(false),
                null, Opts.TurnTime * 1000, Timeout.Infinite);

            if (OnGameStarted != null)
                await OnGameStarted.Invoke(this).ConfigureAwait(false);

            if (OnPlayerTurn != null && CurrentPlayer != null)
                await OnPlayerTurn.Invoke(this, CurrentPlayer).ConfigureAwait(false);

            return true;
        }
        finally
        {
            locker.Release();
        }
    }

    /// <summary>
    ///     Processes a word input from a player.
    /// </summary>
    /// <param name="userId">The user ID of the player.</param>
    /// <param name="word">The word to play.</param>
    /// <returns>A tuple containing success status and validation result.</returns>
    public async Task<(bool Success, ValidationResult Result)> PlayWord(ulong userId, string word)
    {
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            if (CurrentPhase != Phase.Playing)
                return (false, ValidationResult.Valid);

            // Check if it's this player's turn
            if (CurrentPlayer == null || CurrentPlayer.UserId != userId)
                return (false, ValidationResult.Valid);

            // Validate the word
            var validationResult = ValidateWord(word);
            if (validationResult != ValidationResult.Valid)
            {
                // Player is eliminated for invalid word
                var player = CurrentPlayer;
                await EliminateCurrentPlayer(GetReasonForValidation(validationResult)).ConfigureAwait(false);
                return (false, validationResult);
            }

            // Word is valid - update game state
            var normalizedWord = word.Trim().ToLowerInvariant();
            usedWords.Add(normalizedWord);
            CurrentWord = normalizedWord;

            var currentPlayerForEvent = CurrentPlayer;

            // Invoke word played event
            if (OnWordPlayed != null)
                await OnWordPlayed.Invoke(this, currentPlayerForEvent, normalizedWord).ConfigureAwait(false);

            // Move to next player
            await NextTurn().ConfigureAwait(false);

            return (true, ValidationResult.Valid);
        }
        finally
        {
            locker.Release();
        }
    }

    /// <summary>
    ///     Allows a player to say "kaladont" to give up.
    /// </summary>
    /// <param name="userId">The user ID of the player.</param>
    /// <returns>True if the player successfully gave up; otherwise, false.</returns>
    public async Task<bool> SayKaladont(ulong userId)
    {
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            if (CurrentPhase != Phase.Playing)
                return false;

            // Check if it's this player's turn
            if (CurrentPlayer == null || CurrentPlayer.UserId != userId)
                return false;

            await EliminateCurrentPlayer("kaladont").ConfigureAwait(false);
            return true;
        }
        finally
        {
            locker.Release();
        }
    }

    /// <summary>
    ///     Stops the game immediately.
    /// </summary>
    public async Task StopGame()
    {
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            if (CurrentPhase == Phase.Ended)
                return;

            CurrentPhase = Phase.Ended;
            turnTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            if (OnGameEnded != null)
                await OnGameEnded.Invoke(this, null).ConfigureAwait(false);
        }
        finally
        {
            locker.Release();
        }
    }

    private ValidationResult ValidateWord(string word)
    {
        var normalized = word.Trim().ToLowerInvariant();

        // Check minimum length
        if (normalized.Length < 3)
            return ValidationResult.TooShort;

        // Check if already used
        if (usedWords.Contains(normalized))
            return ValidationResult.AlreadyUsed;

        // Check if starts with last 2 letters of current word
        var lastTwo = CurrentWord[^2..];
        var firstTwo = normalized[..2];

        if (lastTwo != firstTwo)
            return ValidationResult.WrongLetters;

        // Check for kaladont loop (word ends with same 2 letters it starts with)
        var endTwo = normalized[^2..];
        if (firstTwo == endTwo)
            return ValidationResult.KaladontLoop;

        // Check dictionary
        if (!dictionary.Contains(normalized))
            return ValidationResult.NotInDictionary;

        return ValidationResult.Valid;
    }

    private async Task NextTurn()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;

        // Reset timer for next player
        turnTimer?.Change(Opts.TurnTime * 1000, Timeout.Infinite);

        if (OnPlayerTurn != null && CurrentPlayer != null)
            await OnPlayerTurn.Invoke(this, CurrentPlayer).ConfigureAwait(false);
    }

    private async Task HandleTurnTimeout()
    {
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            if (CurrentPhase != Phase.Playing)
                return;

            await EliminateCurrentPlayer("timeout").ConfigureAwait(false);
        }
        finally
        {
            locker.Release();
        }
    }

    private async Task EliminateCurrentPlayer(string reason)
    {
        if (CurrentPlayer == null)
            return;

        var eliminatedPlayer = CurrentPlayer;
        players.RemoveAt(currentPlayerIndex);

        // Adjust current player index after removal
        if (currentPlayerIndex >= players.Count && players.Count > 0)
            currentPlayerIndex = 0;

        if (OnPlayerEliminated != null)
            await OnPlayerEliminated.Invoke(this, eliminatedPlayer, reason).ConfigureAwait(false);

        // Check if game is over
        if (players.Count <= 1)
        {
            CurrentPhase = Phase.Ended;
            turnTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            var winner = players.Count == 1 ? players[0] : null;
            if (OnGameEnded != null)
                await OnGameEnded.Invoke(this, winner).ConfigureAwait(false);
        }
        else
        {
            // Continue with next player
            await NextTurn().ConfigureAwait(false);
        }
    }

    private static string GetReasonForValidation(ValidationResult result)
    {
        return result switch
        {
            ValidationResult.TooShort => "too_short",
            ValidationResult.AlreadyUsed => "already_used",
            ValidationResult.WrongLetters => "wrong_letters",
            ValidationResult.KaladontLoop => "kaladont_loop",
            ValidationResult.NotInDictionary => "not_in_dictionary",
            _ => "unknown"
        };
    }

    /// <summary>
    ///     Represents the options for a Kaladont game.
    /// </summary>
    public class Options : IMewdekoCommandOptions
    {
        /// <summary>
        ///     Gets or sets the time in seconds for players to join the game.
        /// </summary>
        [Option('j', "join-time", Required = false, Default = 15,
            HelpText = "Time in seconds for players to join the game.")]
        public int JoinTime { get; set; } = 15;

        /// <summary>
        ///     Gets or sets the time in seconds for each player's turn.
        /// </summary>
        [Option('t', "turn-time", Required = false, Default = 30,
            HelpText = "Time in seconds for each player's turn.")]
        public int TurnTime { get; set; } = 30;

        /// <summary>
        ///     Gets or sets the minimum number of players required to start.
        /// </summary>
        [Option('m', "min-players", Required = false, Default = 2,
            HelpText = "Minimum number of players required to start the game.")]
        public int MinPlayers { get; set; } = 2;

        /// <summary>
        ///     Gets or sets the language for the game dictionary.
        /// </summary>
        [Option('l', "language", Required = false, Default = "en",
            HelpText = "Language for the game dictionary (en or sr).")]
        public string Language { get; set; } = "en";

        /// <summary>
        ///     Normalizes the options by ensuring they are within acceptable ranges.
        /// </summary>
        public void NormalizeOptions()
        {
            if (JoinTime is < 10 or > 60)
                JoinTime = 15;

            if (TurnTime is < 15 or > 120)
                TurnTime = 30;

            if (MinPlayers is < 2 or > 10)
                MinPlayers = 2;

            Language = Language.ToLowerInvariant() switch
            {
                "sr" or "serbian" => "sr",
                _ => "en"
            };
        }
    }
}