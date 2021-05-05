using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NadekoBot.Extensions;

namespace NadekoBot.Modules.Games.Common.Hangman
{
    public sealed class Hangman : IDisposable
    {
        public string TermType { get; }
        public TermPool TermPool { get; }
        public HangmanObject Term { get; }

        public string ScrambledWord => "`" + String.Concat(Term.Word.Select(c =>
        {
            if (c == ' ')
                return " \u2000";
            if (!(char.IsLetter(c) || char.IsDigit(c)))
                return $" {c}";

            c = char.ToLowerInvariant(c);
            return _previousGuesses.Contains(c) ? $" {c}" : " ◯";
        })) + "`";

        private Phase _currentPhase = Phase.Active;
        public Phase CurrentPhase
        {
            get => _currentPhase;
            set
            {
                if (value == Phase.Ended)
                    _endingCompletionSource.TrySetResult(true);

                _currentPhase = value;
            }
        }

        private readonly SemaphoreSlim _locker = new SemaphoreSlim(1, 1);

        private readonly HashSet<ulong> _recentUsers = new HashSet<ulong>();

        public uint Errors { get; private set; } = 0;
        public uint MaxErrors { get; } = 6;

        public event Func<Hangman, string, Task> OnGameEnded = delegate { return Task.CompletedTask; };
        public event Func<Hangman, string, char, Task> OnLetterAlreadyUsed = delegate { return Task.CompletedTask; };
        public event Func<Hangman, string, char, Task> OnGuessFailed = delegate { return Task.CompletedTask; };
        public event Func<Hangman, string, char, Task> OnGuessSucceeded = delegate { return Task.CompletedTask; };

        private readonly HashSet<char> _previousGuesses = new HashSet<char>();
        public ImmutableArray<char> PreviousGuesses => _previousGuesses.ToImmutableArray();

        private readonly TaskCompletionSource<bool> _endingCompletionSource = new TaskCompletionSource<bool>();

        public Task EndedTask => _endingCompletionSource.Task;

        public Hangman(string type, TermPool tp = null)
        {
            this.TermType = type.Trim().ToLowerInvariant().ToTitleCase();
            this.TermPool = tp ?? new TermPool();
            this.Term = this.TermPool.GetTerm(type);
        }

        private void AddError()
        {
            if (++Errors > MaxErrors)
            {
                var _ = OnGameEnded(this, null);
                CurrentPhase = Phase.Ended;
            }
        }

        public string GetHangman() => $@". ┌─────┐
.┃...............┋
.┃...............┋
.┃{(Errors > 0 ? ".............😲" : "")}
.┃{(Errors > 1 ? "............./" : "")} {(Errors > 2 ? "|" : "")} {(Errors > 3 ? "\\" : "")}
.┃{(Errors > 4 ? "............../" : "")} {(Errors > 5 ? "\\" : "")}
/-\";

        public async Task Input(ulong userId, string userName, string input)
        {
            if (CurrentPhase == Phase.Ended)
                return;

            if (string.IsNullOrWhiteSpace(input))
                return;

            input = input.Trim().ToLowerInvariant();

            await _locker.WaitAsync().ConfigureAwait(false);
            try
            {
                if (CurrentPhase == Phase.Ended)
                    return;

                if (input.Length > 1) // tried to guess the whole word
                {
                    if (input != Term.Word) // failed
                        return;

                    var _ = OnGameEnded?.Invoke(this, userName);
                    CurrentPhase = Phase.Ended;
                    return;
                }

                var ch = input[0];

                if (!char.IsLetterOrDigit(ch))
                    return;

                if (!_recentUsers.Add(userId)) // don't let a single user spam guesses
                    return;

                if (!_previousGuesses.Add(ch)) // that letter was already guessed
                {
                    var _ = OnLetterAlreadyUsed?.Invoke(this, userName, ch);
                }
                else if (!Term.Word.Contains(ch)) // guessed letter doesn't exist
                {
                    var _ = OnGuessFailed?.Invoke(this, userName, ch);
                    AddError();
                }
                else if (Term.Word.All(x => _previousGuesses.IsSupersetOf(Term.Word.ToLowerInvariant()
                                                                            .Where(char.IsLetterOrDigit))))
                {
                    var _ = OnGameEnded.Invoke(this, userName); // if all letters are guessed
                    CurrentPhase = Phase.Ended;
                }
                else // guessed but not last letter
                {
                    var _ = OnGuessSucceeded?.Invoke(this, userName, ch);
                    _recentUsers.Remove(userId); // he can guess again right away
                    return;
                }

                var clearSpam = Task.Run(async () =>
                {
                    await Task.Delay(3000).ConfigureAwait(false); // remove the user from the spamlist after 5 seconds
                    _recentUsers.Remove(userId);
                });
            }
            finally { _locker.Release(); }
        }

        public async Task Stop()
        {
            await _locker.WaitAsync().ConfigureAwait(false);
            try
            {
                CurrentPhase = Phase.Ended;
            }
            finally { _locker.Release(); }
        }

        public void Dispose()
        {
            OnGameEnded = null;
            OnGuessFailed = null;
            OnGuessSucceeded = null;
            OnLetterAlreadyUsed = null;
            _previousGuesses.Clear();
            _recentUsers.Clear();
            // _locker.Dispose();
        }
    }
}
