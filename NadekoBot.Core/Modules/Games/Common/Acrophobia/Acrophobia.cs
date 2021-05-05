using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using NadekoBot.Common;
using NadekoBot.Core.Common;
using NadekoBot.Extensions;

namespace NadekoBot.Modules.Games.Common.Acrophobia
{
    public sealed class AcrophobiaGame : IDisposable
    {
        public class Options : INadekoCommandOptions
        {
            [Option('s', "submission-time", Required = false, Default = 60, HelpText = "Time after which the submissions are closed and voting starts.")]
            public int SubmissionTime { get; set; } = 60;

            [Option('v', "vote-time", Required = false, Default = 60, HelpText = "Time after which the voting is closed and the winner is declared.")]
            public int VoteTime { get; set; } = 30;

            public void NormalizeOptions()
            {
                if (SubmissionTime < 15 || SubmissionTime > 300)
                    SubmissionTime = 60;
                if (VoteTime < 15 || VoteTime > 120)
                    VoteTime = 30;
            }
        }

        public enum Phase
        {
            Submission,
            Voting,
            Ended
        }

        public enum UserInputResult
        {
            Submitted,
            SubmissionFailed,
            Voted,
            VotingFailed,
            Failed
        }

        public Phase CurrentPhase { get; private set; } = Phase.Submission;
        public ImmutableArray<char> StartingLetters { get; private set; }

        private readonly Dictionary<AcrophobiaUser, int> submissions = new Dictionary<AcrophobiaUser, int>();
        private readonly SemaphoreSlim locker = new SemaphoreSlim(1, 1);
        public Options Opts { get; }
        private readonly NadekoRandom _rng;

        public event Func<AcrophobiaGame, Task> OnStarted = delegate { return Task.CompletedTask; };
        public event Func<AcrophobiaGame, ImmutableArray<KeyValuePair<AcrophobiaUser, int>>, Task> OnVotingStarted = delegate { return Task.CompletedTask; };
        public event Func<string, Task> OnUserVoted = delegate { return Task.CompletedTask; };
        public event Func<AcrophobiaGame, ImmutableArray<KeyValuePair<AcrophobiaUser, int>>, Task> OnEnded = delegate { return Task.CompletedTask; };

        private readonly HashSet<ulong> _usersWhoVoted = new HashSet<ulong>();

        public AcrophobiaGame(Options options)
        {
            Opts = options;
            _rng = new NadekoRandom();
            InitializeStartingLetters();
        }

        public async Task Run()
        {
            await OnStarted(this).ConfigureAwait(false);
            await Task.Delay(Opts.SubmissionTime * 1000).ConfigureAwait(false);
            await locker.WaitAsync().ConfigureAwait(false);
            try
            {
                if (submissions.Count == 0)
                {
                    CurrentPhase = Phase.Ended;
                    await OnVotingStarted(this, ImmutableArray.Create<KeyValuePair<AcrophobiaUser, int>>()).ConfigureAwait(false);
                    return;
                }
                if (submissions.Count == 1)
                {
                    CurrentPhase = Phase.Ended;
                    await OnVotingStarted(this, submissions.ToArray().ToImmutableArray()).ConfigureAwait(false);
                    return;
                }

                CurrentPhase = Phase.Voting;

                await OnVotingStarted(this, submissions.ToArray().ToImmutableArray()).ConfigureAwait(false);
            }
            finally { locker.Release(); }

            await Task.Delay(Opts.VoteTime * 1000).ConfigureAwait(false);
            await locker.WaitAsync().ConfigureAwait(false);
            try
            {
                CurrentPhase = Phase.Ended;
                await OnEnded(this, submissions.ToArray().ToImmutableArray()).ConfigureAwait(false);
            }
            finally { locker.Release(); }
        }

        private void InitializeStartingLetters()
        {
            var wordCount = _rng.Next(3, 6);

            var lettersArr = new char[wordCount];

            for (int i = 0; i < wordCount; i++)
            {
                var randChar = (char)_rng.Next(65, 91);
                lettersArr[i] = randChar == 'X' ? (char)_rng.Next(65, 88) : randChar;
            }
            StartingLetters = lettersArr.ToImmutableArray();
        }

        public async Task<bool> UserInput(ulong userId, string userName, string input)
        {
            var user = new AcrophobiaUser(userId, userName, input.ToLowerInvariant().ToTitleCase());

            await locker.WaitAsync().ConfigureAwait(false);
            try
            {
                switch (CurrentPhase)
                {
                    case Phase.Submission:
                        if (submissions.ContainsKey(user) || !IsValidAnswer(input))
                            break;

                        submissions.Add(user, 0);
                        return true;
                    case Phase.Voting:
                        AcrophobiaUser toVoteFor;
                        if (!int.TryParse(input, out var index)
                            || --index < 0
                            || index >= submissions.Count
                            || (toVoteFor = submissions.ToArray()[index].Key).UserId == user.UserId
                            || !_usersWhoVoted.Add(userId))
                            break;
                        ++submissions[toVoteFor];
                        var _ = Task.Run(() => OnUserVoted(userName));
                        return true;
                    default:
                        break;
                }
                return false;
            }
            finally
            {
                locker.Release();
            }
        }

        private bool IsValidAnswer(string input)
        {
            input = input.ToUpperInvariant();

            var inputWords = input.Split(' ');

            if (inputWords.Length != StartingLetters.Length) // number of words must be the same as the number of the starting letters
                return false;

            for (int i = 0; i < StartingLetters.Length; i++)
            {
                var letter = StartingLetters[i];

                if (!inputWords[i].StartsWith(letter.ToString(), StringComparison.InvariantCulture)) // all first letters must match
                    return false;
            }

            return true;
        }

        public void Dispose()
        {
            this.CurrentPhase = Phase.Ended;
            OnStarted = null;
            OnEnded = null;
            OnUserVoted = null;
            OnVotingStarted = null;
            _usersWhoVoted.Clear();
            submissions.Clear();
            locker.Dispose();
        }
    }
}
