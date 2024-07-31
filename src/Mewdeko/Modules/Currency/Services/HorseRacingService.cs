namespace Mewdeko.Modules.Currency.Services;

/// <summary>
/// Service for managing horse racing games.
/// </summary>
public class HorseRacingService : INService
{
    private readonly ICurrencyService cs;
    private readonly ConcurrentDictionary<ulong, RaceData> races = new();
    private static readonly string[] Animals = ["🐎", "🐪", "🦏", "🦛", "🐘", "🦒", "🦘", "🐅", "🐆", "🦓"];

    /// <summary>
    /// Initializes a new instance of the <see cref="HorseRacingService"/> class.
    /// </summary>
    /// <param name="cs">The currency service.</param>
    public HorseRacingService(ICurrencyService cs)
    {
        this.cs = cs;
    }

    /// <summary>
    /// Allows a user to join a race or starts a new race if none exists.
    /// </summary>
    /// <param name="userId">The ID of the user joining the race.</param>
    /// <param name="guildId">The ID of the guild where the race is taking place.</param>
    /// <param name="betAmount">The amount of currency the user is betting.</param>
    /// <returns>A tuple indicating the success of joining, a message, and whether the race has started.</returns>
    /// <summary>
    /// Allows a user to join a race or starts a new race if none exists.
    /// </summary>
    /// <param name="user">The user joining the race.</param>
    /// <param name="guildId">The ID of the guild where the race is taking place.</param>
    /// <param name="betAmount">The amount of currency the user is betting.</param>
    /// <returns>A tuple indicating the success of joining, a message, and whether the race has started.</returns>
    public async Task<(bool Success, string Message, bool RaceStarted)> JoinRace(IUser user, ulong guildId, int betAmount)
    {
        var race = races.GetOrAdd(guildId, _ => new RaceData());

        lock (race)
        {
            if (race.Participants.Count >= 10)
                return (false, "horse_race_full", false);

            if (race.Participants.Any(p => p.UserId == user.Id))
                return (false, "horse_race_already_joined", false);

            race.Participants.Add(new Racer(user.Id, betAmount, Animals[race.Participants.Count], user.ToString()));

            if (race.Participants.Count == 10)
            {
                race.IsActive = true;
                return (true, "", true);
            }

            if (race.Participants.Count == 1)
            {
                // Start a timer to add AI players if no one else joins
                Task.Delay(10000).ContinueWith(_ => AddAiPlayersIfNeeded(guildId));
            }

            return (true, "", false);
        }
    }

    /// <summary>
    /// Adds AI players to the race if needed after 10 seconds.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the race is taking place.</param>
    private async Task AddAiPlayersIfNeeded(ulong guildId)
    {
        if (races.TryGetValue(guildId, out var race))
        {
            var rand  = new Random();
            lock (race)
            {
                if (race.Participants.Count < 5 && !race.IsActive)
                {
                    var aiPlayersToAdd = 5 - race.Participants.Count;
                    for (var i = 0; i < aiPlayersToAdd; i++)
                    {
                        var aiId = ulong.MaxValue - (ulong)i;  // Use high ulong values for AI players
                        var betAmount = rand.Next(10, 101);  // Random bet between 10 and 100
                        race.Participants.Add(new Racer(aiId, betAmount, Animals[race.Participants.Count], $"AI Player {i + 1}"));
                    }
                    race.IsActive = true;
                }
            }
        }
    }

    /// <summary>
    /// Updates the progress of all racers in a given race.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the race is taking place.</param>
    /// <returns>A list of updated racer progress.</returns>
    public async Task<List<RacerProgress>> UpdateRaceProgress(ulong guildId)
    {
        if (!races.TryGetValue(guildId, out var race))
            return [];

        var random = new Random();
        foreach (var racer in race.Participants)
        {
            racer.Progress += random.Next(0, 3);
            racer.Progress = Math.Min(racer.Progress, 10);
        }

        return race.Participants.Select(r => new RacerProgress(r.UserId, r.Animal, r.Progress, r.Username)).ToList();
    }

    /// <summary>
    /// Finishes a race and calculates the winners and their winnings.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the race is taking place.</param>
    /// <returns>The final result of the race, including winners and final positions.</returns>
    public async Task<RaceResult> FinishRace(ulong guildId)
    {
        if (!races.TryRemove(guildId, out var race))
            return new RaceResult([], []);

        var orderedRacers = race.Participants.OrderByDescending(r => r.Progress).ThenBy(r => r.BetAmount).ToList();
        var totalPool = race.Participants.Sum(r => r.BetAmount);
        var winners = new List<RaceWinner>();
        var finalPositions = new List<FinalPosition>();

        for (var i = 0; i < orderedRacers.Count; i++)
        {
            var racer = orderedRacers[i];
            var winnings = 0;

            switch (i)
            {
                case 0:
                    winnings = (int)(totalPool * 0.5);
                    winners.Add(new RaceWinner(racer.UserId, winnings, racer.Username));
                    break;
                case 1 when orderedRacers.Count > 3:
                    winnings = (int)(totalPool * 0.3);
                    winners.Add(new RaceWinner(racer.UserId, winnings, racer.Username));
                    break;
                case 2 when orderedRacers.Count > 4:
                    winnings = (int)(totalPool * 0.2);
                    winners.Add(new RaceWinner(racer.UserId, winnings, racer.Username));
                    break;
            }

            if (racer.UserId < ulong.MaxValue - 4)  // Only process transactions for real players
            {
                await cs.AddTransactionAsync(racer.UserId, winnings - racer.BetAmount, new("Horse Race"));
            }
            finalPositions.Add(new FinalPosition(i + 1, racer.UserId, racer.Animal, winnings, racer.Username));
        }

        return new RaceResult(winners, finalPositions);
    }

    /// <summary>
    /// Represents the data for a single race.
    /// </summary>
    private class RaceData
    {
        /// <summary>
        /// Gets the list of participants in the race.
        /// </summary>
        public List<Racer> Participants { get; } = [];

        /// <summary>
        /// Gets the start time of the race.
        /// </summary>
        public DateTime StartTime { get; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets a value indicating whether the race is active.
        /// </summary>
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Represents a single racer in the race.
    /// </summary>
    private class Racer
    {
        /// <summary>
        /// Gets the ID of the user participating in the race.
        /// </summary>
        public ulong UserId { get; }

        /// <summary>
        /// Gets the amount of currency the user bet on the race.
        /// </summary>
        public int BetAmount { get; }

        /// <summary>
        /// Gets the animal emoji representing the racer.
        /// </summary>
        public string Animal { get; }

        /// <summary>
        /// Gets or sets the current progress of the racer.
        /// </summary>
        public int Progress { get; set; }

        public string Username { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Racer"/> class.
        /// </summary>
        /// <param name="userId">The ID of the user participating in the race.</param>
        /// <param name="betAmount">The amount of currency the user bet on the race.</param>
        /// <param name="animal">The animal emoji representing the racer.</param>
        public Racer(ulong userId, int betAmount, string animal, string username)
        {
            UserId = userId;
            BetAmount = betAmount;
            Animal = animal;
            Username = username;
        }
    }
}

/// <summary>
/// Represents the progress of a racer during the race.
/// </summary>
public record RacerProgress
{
    /// <summary>
    /// Gets the ID of the user.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// Gets the animal emoji representing the racer.
    /// </summary>
    public string Animal { get; init; }

    /// <summary>
    /// Gets the current progress of the racer.
    /// </summary>
    public int Progress { get; init; }

    /// <summary>
    /// Username
    /// </summary>
    public string Username { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RacerProgress"/> class.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="animal">The animal emoji representing the racer.</param>
    /// <param name="progress">The current progress of the racer.</param>
    public RacerProgress(ulong userId, string animal, int progress, string username)
    {
        UserId = userId;
        Animal = animal;
        Progress = progress;
        Username = username;
    }
}

/// <summary>
/// Represents a winner of the race.
/// </summary>
public record RaceWinner
{
    /// <summary>
    /// Gets the ID of the winning user.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// Gets the amount of currency won.
    /// </summary>
    public int Winnings { get; init; }

    /// <summary>
    /// Username
    /// </summary>
    public string Username { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RaceWinner"/> class.
    /// </summary>
    /// <param name="userId">The ID of the winning user.</param>
    /// <param name="winnings">The amount of currency won.</param>
    public RaceWinner(ulong userId, int winnings, string username)
    {
        UserId = userId;
        Winnings = winnings;
        Username = username;
    }
}

/// <summary>
/// Represents the final position of a racer in the race.
/// </summary>
public record FinalPosition
{
    /// <summary>
    /// Gets the final position of the racer.
    /// </summary>
    public int Position { get; init; }

    /// <summary>
    /// Gets the ID of the user.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// Gets the animal emoji representing the racer.
    /// </summary>
    public string Animal { get; init; }

    /// <summary>
    /// Gets the amount of currency won (or lost if negative).
    /// </summary>
    public int Winnings { get; init; }

    /// <summary>
    /// Username
    /// </summary>
    public string Username { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FinalPosition"/> class.
    /// </summary>
    /// <param name="position">The final position of the racer.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="animal">The animal emoji representing the racer.</param>
    /// <param name="winnings">The amount of currency won (or lost if negative).</param>
    public FinalPosition(int position, ulong userId, string animal, int winnings, string username)
    {
        Position = position;
        UserId = userId;
        Animal = animal;
        Winnings = winnings;
        Username = username;
    }
}

/// <summary>
/// Represents the final result of a race.
/// </summary>
public record RaceResult
{
    /// <summary>
    /// Gets the list of winners in the race.
    /// </summary>
    public List<RaceWinner> Winners { get; init; }

    /// <summary>
    /// Gets the list of final positions for all racers.
    /// </summary>
    public List<FinalPosition> FinalPositions { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RaceResult"/> class.
    /// </summary>
    /// <param name="winners">The list of winners in the race.</param>
    /// <param name="finalPositions">The list of final positions for all racers.</param>
    public RaceResult(List<RaceWinner> winners, List<FinalPosition> finalPositions)
    {
        Winners = winners;
        FinalPositions = finalPositions;
    }
}