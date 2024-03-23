using System.Collections.Immutable;
using System.Threading;

namespace Mewdeko.Modules.Games.Common.Hangman;

/// <summary>
/// Represents a Hangman game.
/// </summary>
public sealed class Hangman : IDisposable
{
    private readonly TaskCompletionSource<bool> endingCompletionSource = new();

    private readonly SemaphoreSlim locker = new(1, 1);

    private readonly HashSet<char> previousGuesses = new();

    private readonly HashSet<ulong> recentUsers = new();

    private Phase currentPhase = Phase.Active;

    /// <summary>
    /// Initializes a new instance of the <see cref="Hangman"/> class.
    /// </summary>
    /// <param name="type">Type of game</param>
    /// <param name="tp">The terms this game will use</param>
    public Hangman(string type, TermPool? tp = null)
    {
        TermType = type.Trim().ToLowerInvariant().ToTitleCase();
        TermPool = tp ?? new TermPool();
        Term = TermPool.GetTerm(type);
    }

    /// <summary>
    /// Gets the type of the term used in the Hangman game.
    /// </summary>
    public string TermType { get; }

    /// <summary>
    /// Gets the term pool used in the Hangman game.
    /// </summary>
    public TermPool TermPool { get; }

    /// <summary>
    /// Gets the Hangman object representing the term.
    /// </summary>
    public HangmanObject Term { get; }

    /// <summary>
    /// Gets the scrambled word for display during the game.
    /// </summary>
    /// <remarks>
    /// The scrambled word replaces unguessed characters with a placeholder.
    /// </remarks>
    public string ScrambledWord =>
        $"`{string.Concat(Term.Word.Select(c => { if (c == ' ') return " \u2000"; if (!(char.IsLetter(c) || char.IsDigit(c))) return $" {c}"; c = char.ToLowerInvariant(c); return previousGuesses.Contains(c) ? $" {c}" : " ◯"; }))}`";

    /// <summary>
    /// Gets or sets the current phase of the Hangman game.
    /// </summary>
    public Phase CurrentPhase
    {
        get => currentPhase;
        set
        {
            if (value == Phase.Ended)
                endingCompletionSource.TrySetResult(true);

            currentPhase = value;
        }
    }

    /// <summary>
    /// Gets the number of errors made during the game.
    /// </summary>
    public uint Errors { get; private set; }

    /// <summary>
    /// Gets the maximum number of errors allowed during the game.
    /// </summary>
    public uint MaxErrors { get; } = 6;

    /// <summary>
    /// Gets the previous guesses made during the game.
    /// </summary>
    public ImmutableArray<char> PreviousGuesses => previousGuesses.ToImmutableArray();

    /// <summary>
    /// Gets the task representing the end of the Hangman game.
    /// </summary>
    public Task EndedTask => endingCompletionSource.Task;

    /// <summary>
    /// Disposes of the Hangman instance.
    /// </summary>
    public void Dispose()
    {
        OnGameEnded = null;
        OnGuessFailed = null;
        OnGuessSucceeded = null;
        OnLetterAlreadyUsed = null;
        previousGuesses.Clear();
        recentUsers.Clear();
        // _locker.Dispose();
    }

    /// <summary>
    /// Event triggered when the Hangman game ends.
    /// </summary>
    public event Func<Hangman, string, Task> OnGameEnded = delegate { return Task.CompletedTask; };

    /// <summary>
    /// Event triggered when a letter is guessed but it has already been used.
    /// </summary>
    public event Func<Hangman, string, char, Task> OnLetterAlreadyUsed = delegate { return Task.CompletedTask; };

    /// <summary>
    /// Event triggered when a guess fails.
    /// </summary>
    public event Func<Hangman, string, char, Task> OnGuessFailed = delegate { return Task.CompletedTask; };

    /// <summary>
    /// Event triggered when a guess succeeds.
    /// </summary>
    public event Func<Hangman, string, char, Task> OnGuessSucceeded = delegate { return Task.CompletedTask; };

    private void AddError()
    {
        if (++Errors > MaxErrors)
        {
            var _ = OnGameEnded(this, null);
            CurrentPhase = Phase.Ended;
        }
    }

    /// <summary>
    /// Generates the ASCII art representation of the hangman based on the current errors.
    /// </summary>
    /// <returns>The ASCII art representation of the hangman.</returns>
    public string GetHangman() =>
        $@". ┌─────┐
.┃...............┋
.┃...............┋
.┃{(Errors > 0 ? ".............😲" : "")}
.┃{(Errors > 1 ? "............./" : "")} {(Errors > 2 ? "|" : "")} {(Errors > 3 ? "\\" : "")}
.┃{(Errors > 4 ? "............../" : "")} {(Errors > 5 ? "\\" : "")}
/-\";

    /// <summary>
    /// Handles user input during the game.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="userName">The name of the user.</param>
    /// <param name="input">The user's input.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Input(ulong userId, string userName, string input)
    {
        if (CurrentPhase == Phase.Ended)
            return;

        if (string.IsNullOrWhiteSpace(input))
            return;

        input = input.Trim().ToLowerInvariant();

        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            if (CurrentPhase == Phase.Ended)
                return;

            if (input.Length > 1) // tried to guess the whole word
            {
                if (input != Term.Word) // failed
                    return;

                var _ = OnGameEnded.Invoke(this, userName);
                CurrentPhase = Phase.Ended;
                return;
            }

            var ch = input[0];

            if (!char.IsLetterOrDigit(ch))
                return;

            if (!recentUsers.Add(userId)) // don't let a single user spam guesses
                return;

            if (!previousGuesses.Add(ch)) // that letter was already guessed
            {
                var _ = OnLetterAlreadyUsed.Invoke(this, userName, ch);
            }
            else if (!Term.Word.Contains(ch)) // guessed letter doesn't exist
            {
                var _ = OnGuessFailed.Invoke(this, userName, ch);
                AddError();
            }
            else if (Term.Word.All(_ => previousGuesses.IsSupersetOf(Term.Word.ToLowerInvariant()
                         .Where(char.IsLetterOrDigit))))
            {
                var _ = OnGameEnded.Invoke(this, userName); // if all letters are guessed
                CurrentPhase = Phase.Ended;
            }
            else // guessed but not last letter
            {
                var _ = OnGuessSucceeded.Invoke(this, userName, ch);
                recentUsers.Remove(userId); // he can guess again right away
                return;
            }

            await Task.Run(async () =>
            {
                await Task.Delay(3000).ConfigureAwait(false); // remove the user from the spamlist after 5 seconds
                recentUsers.Remove(userId);
            });
        }
        finally
        {
            locker.Release();
        }
    }

    /// <summary>
    /// Stops the game.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Stop()
    {
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            CurrentPhase = Phase.Ended;
        }
        finally
        {
            locker.Release();
        }
    }
}