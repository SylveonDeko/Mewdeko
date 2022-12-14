using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;

namespace Mewdeko.Modules.Games.Common.Acrophobia;

public sealed class AcrophobiaGame : IDisposable
{
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

    private readonly MewdekoRandom rng;

    private readonly HashSet<ulong> usersWhoVoted = new();
    private readonly SemaphoreSlim locker = new(1, 1);

    private readonly Dictionary<AcrophobiaUser, int> submissions = new();

    public AcrophobiaGame(Options options)
    {
        Opts = options;
        rng = new MewdekoRandom();
        InitializeStartingLetters();
    }

    public Phase CurrentPhase { get; private set; } = Phase.Submission;
    public ImmutableArray<char> StartingLetters { get; private set; }
    public Options Opts { get; }

    public void Dispose()
    {
        CurrentPhase = Phase.Ended;
        OnStarted = null;
        OnEnded = null;
        OnUserVoted = null;
        OnVotingStarted = null;
        usersWhoVoted.Clear();
        submissions.Clear();
        locker.Dispose();
    }

    public event Func<AcrophobiaGame, Task> OnStarted = delegate { return Task.CompletedTask; };

    public event Func<AcrophobiaGame, ImmutableArray<KeyValuePair<AcrophobiaUser, int>>, Task> OnVotingStarted =
        delegate { return Task.CompletedTask; };

    public event Func<string, Task> OnUserVoted = delegate { return Task.CompletedTask; };

    public event Func<AcrophobiaGame, ImmutableArray<KeyValuePair<AcrophobiaUser, int>>, Task> OnEnded = delegate
    {
        return Task.CompletedTask;
    };

    public async Task Run()
    {
        await OnStarted(this).ConfigureAwait(false);
        await Task.Delay(Opts.SubmissionTime * 1000).ConfigureAwait(false);
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            switch (submissions.Count)
            {
                case 0:
                    CurrentPhase = Phase.Ended;
                    await OnVotingStarted(this, ImmutableArray.Create<KeyValuePair<AcrophobiaUser, int>>())
                        .ConfigureAwait(false);
                    return;
                case 1:
                    CurrentPhase = Phase.Ended;
                    await OnVotingStarted(this, submissions.ToArray().ToImmutableArray()).ConfigureAwait(false);
                    return;
                default:
                    CurrentPhase = Phase.Voting;

                    await OnVotingStarted(this, submissions.ToArray().ToImmutableArray()).ConfigureAwait(false);
                    break;
            }
        }
        finally
        {
            locker.Release();
        }

        await Task.Delay(Opts.VoteTime * 1000).ConfigureAwait(false);
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            CurrentPhase = Phase.Ended;
            await OnEnded(this, submissions.ToArray().ToImmutableArray()).ConfigureAwait(false);
        }
        finally
        {
            locker.Release();
        }
    }

    private void InitializeStartingLetters()
    {
        var wordCount = rng.Next(3, 6);

        var lettersArr = new char[wordCount];

        for (var i = 0; i < wordCount; i++)
        {
            var randChar = (char)rng.Next(65, 91);
            lettersArr[i] = randChar == 'X' ? (char)rng.Next(65, 88) : randChar;
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
                        || !usersWhoVoted.Add(userId))
                    {
                        break;
                    }

                    ++submissions[toVoteFor];
                    _ = Task.Run(() => OnUserVoted(userName));
                    return true;
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

        if (inputWords.Length !=
            StartingLetters.Length) // number of words must be the same as the number of the starting letters
        {
            return false;
        }

        for (var i = 0; i < StartingLetters.Length; i++)
        {
            var letter = StartingLetters[i];

            if (!inputWords[i]
                    .StartsWith(letter.ToString(), StringComparison.InvariantCulture)) // all first letters must match
            {
                return false;
            }
        }

        return true;
    }

    public class Options : IMewdekoCommandOptions
    {
        [Option('s', "submission-time", Required = false, Default = 60,
            HelpText = "Time after which the submissions are closed and voting starts.")]
        public int SubmissionTime { get; set; } = 60;

        [Option('v', "vote-time", Required = false, Default = 60,
            HelpText = "Time after which the voting is closed and the winner is declared.")]
        public int VoteTime { get; set; } = 30;

        public void NormalizeOptions()
        {
            if (SubmissionTime is < 15 or > 300)
                SubmissionTime = 60;
            if (VoteTime is < 15 or > 120)
                VoteTime = 30;
        }
    }
}