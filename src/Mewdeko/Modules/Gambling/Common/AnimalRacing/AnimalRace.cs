using System.Threading;
using System.Threading.Tasks;
using Mewdeko.Modules.Gambling.Common.AnimalRacing.Exceptions;
using Mewdeko.Modules.Games.Common;

namespace Mewdeko.Modules.Gambling.Common.AnimalRacing;

public sealed class AnimalRace : IDisposable
{
    public enum Phase
    {
        WaitingForPlayers,
        Running,
        Ended
    }

    private readonly Queue<RaceAnimal?> animalsQueue;
    private readonly ICurrencyService currency;

    private readonly SemaphoreSlim locker = new(1, 1);
    private readonly RaceOptions options;
    private readonly HashSet<AnimalRacingUser> users = new();

    public AnimalRace(RaceOptions options, ICurrencyService currency, IEnumerable<RaceAnimal> availableAnimals)
    {
        this.currency = currency;
        this.options = options;
        animalsQueue = new Queue<RaceAnimal?>(availableAnimals);
        MaxUsers = animalsQueue.Count;

        if (animalsQueue.Count == 0)
            CurrentPhase = Phase.Ended;
    }

    public Phase CurrentPhase { get; private set; } = Phase.WaitingForPlayers;

    public IReadOnlyCollection<AnimalRacingUser> Users => users.ToList();
    public List<AnimalRacingUser> FinishedUsers { get; } = new();
    public int MaxUsers { get; }

    public void Dispose()
    {
        CurrentPhase = Phase.Ended;
        OnStarted = null;
        OnEnded = null;
        OnStartingFailed = null;
        OnStateUpdate = null;
        locker.Dispose();
        users.Clear();
    }

    public event Func<AnimalRace, Task> OnStarted = delegate { return Task.CompletedTask; };
    public event Func<AnimalRace, Task> OnStartingFailed = delegate { return Task.CompletedTask; };
    public event Func<AnimalRace, Task> OnStateUpdate = delegate { return Task.CompletedTask; };
    public event Func<AnimalRace, Task> OnEnded = delegate { return Task.CompletedTask; };

    public void Initialize() //lame name
        =>
            Task.Run(async () =>
            {
                await Task.Delay(options.StartTime * 1000).ConfigureAwait(false);

                await locker.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (CurrentPhase != Phase.WaitingForPlayers)
                        return;

                    await Start().ConfigureAwait(false);
                }
                finally
                {
                    locker.Release();
                }
            });

    public async Task<AnimalRacingUser> JoinRace(ulong userId, string userName, long bet = 0)
    {
        if (bet < 0)
            throw new ArgumentOutOfRangeException(nameof(bet));

        var user = new AnimalRacingUser(userName, userId, bet);

        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            if (users.Count == MaxUsers)
                throw new AnimalRaceFullException();

            if (CurrentPhase != Phase.WaitingForPlayers)
                throw new AlreadyStartedException();

            if (!await currency.RemoveAsync(userId, "BetRace", bet).ConfigureAwait(false))
                throw new NotEnoughFundsException();

            if (users.Contains(user))
                throw new AlreadyJoinedException();

            user.Animal = animalsQueue.Dequeue();
            users.Add(user);

            if (animalsQueue.Count == 0) //start if no more spots left
                await Start().ConfigureAwait(false);

            return user;
        }
        finally
        {
            locker.Release();
        }
    }

    private async Task Start()
    {
        CurrentPhase = Phase.Running;
        if (users.Count <= 1)
        {
            foreach (var user in users)
            {
                if (user.Bet > 0)
                    await currency.AddAsync(user.UserId, "Race refund", user.Bet).ConfigureAwait(false);
            }

            await OnStartingFailed.Invoke(this);
            CurrentPhase = Phase.Ended;
            return;
        }

        _ = OnStarted.Invoke(this);
        await Task.Run(async () =>
        {
            var rng = new MewdekoRandom();
            while (!users.All(x => x.Progress >= 60))
            {
                foreach (var user in users)
                {
                    user.Progress += rng.Next(1, 11);
                    if (user.Progress >= 60)
                        user.Progress = 60;
                }

                var finished = users.Where(x => x.Progress >= 60 && !FinishedUsers.Contains(x))
                    .Shuffle();

                FinishedUsers.AddRange(finished);

                await OnStateUpdate.Invoke(this);
                await Task.Delay(2500).ConfigureAwait(false);
            }

            if (FinishedUsers[0].Bet > 0)
            {
                await currency.AddAsync(FinishedUsers[0].UserId, "Won a Race",
                        FinishedUsers[0].Bet * (users.Count - 1))
                    .ConfigureAwait(false);
            }

            await OnEnded.Invoke(this);
        });
    }
}