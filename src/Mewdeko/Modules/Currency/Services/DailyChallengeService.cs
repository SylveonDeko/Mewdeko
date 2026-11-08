using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Database.DbContextStuff;
using Serilog;
using System.Threading;

namespace Mewdeko.Modules.Currency.Services;

/// <summary>
///     Service for managing daily challenges in the currency system.
/// </summary>
public class DailyChallengeService : INService
{
    private static readonly string[] SpecificGames =
    [
        "blackjack", "roulette", "slots", "crash", "keno", "plinko", "bingo", "memory", "trivia"
    ];

    private readonly IDataConnectionFactory connectionFactory;

    private readonly SemaphoreSlim[] gates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    /// <summary>
    ///     Initializes a new instance of the <see cref="DailyChallengeService" /> class.
    /// </summary>
    /// <param name="connectionFactory">The database connection factory.</param>
    public DailyChallengeService(IDataConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    /// <summary>
    ///     Gets today's daily challenge for a user, creating it on first use.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The daily challenge. <see cref="DailyChallenge.IsCompleted" /> is set once it has been claimed.</returns>
    public async Task<DailyChallenge> GetCurrentChallenge(ulong userId, ulong guildId)
    {
        var gate = GateFor(userId);
        await gate.WaitAsync();
        try
        {
            await using var ctx = await connectionFactory.CreateConnectionAsync();
            return await GetOrCreateAsync(ctx, userId, guildId);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    ///     Records the stake of a game that was just started for today's challenge.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="gameKey">The game identifier used on the ledger.</param>
    /// <param name="bet">The amount wagered.</param>
    public Task RecordBet(ulong userId, ulong guildId, string gameKey, long bet)
    {
        return RecordAsync(userId, guildId, challenge => challenge.ChallengeType switch
        {
            DailyChallengeType.PlayGames => 1,
            DailyChallengeType.SpendCurrency => bet,
            DailyChallengeType.PlaySpecificGame => MatchesSpecificGame(challenge.Description, gameKey) ? 1 : 0,
            DailyChallengeType.WinGames or DailyChallengeType.EarnCurrency => 0,
            _ => 1
        });
    }

    /// <summary>
    ///     Records the payout of a finished game for today's challenge.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="bet">The amount wagered.</param>
    /// <param name="payout">The total amount returned to the player, stake included.</param>
    public Task RecordPayout(ulong userId, ulong guildId, long bet, long payout)
    {
        var winnings = Math.Max(0, payout - bet);

        return RecordAsync(userId, guildId, challenge => challenge.ChallengeType switch
        {
            DailyChallengeType.WinGames => winnings > 0 ? 1 : 0,
            DailyChallengeType.EarnCurrency => winnings,
            _ => 0
        });
    }

    /// <summary>
    ///     Claims today's challenge once its progress is full.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The reward amount, or 0 when the challenge is unfinished or already claimed.</returns>
    public async Task<long> ClaimChallenge(ulong userId, ulong guildId)
    {
        var gate = GateFor(userId);
        await gate.WaitAsync();
        try
        {
            await using var ctx = await connectionFactory.CreateConnectionAsync();
            var challenge = await GetOrCreateAsync(ctx, userId, guildId);

            if (challenge.IsCompleted || challenge.Progress < challenge.RequiredAmount)
                return 0;

            var claimed = await ctx.DailyChallenges
                .Where(dc => dc.Id == challenge.Id && !dc.IsCompleted)
                .Set(dc => dc.IsCompleted, true)
                .Set(dc => dc.CompletedAt, DateTime.UtcNow)
                .UpdateAsync();

            return claimed > 0 ? challenge.RewardAmount : 0;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RecordAsync(ulong userId, ulong guildId, Func<DailyChallenge, long> amountFor)
    {
        try
        {
            var gate = GateFor(userId);
            await gate.WaitAsync();
            try
            {
                await using var ctx = await connectionFactory.CreateConnectionAsync();
                var challenge = await GetOrCreateAsync(ctx, userId, guildId);

                if (challenge.IsCompleted || challenge.Progress >= challenge.RequiredAmount)
                    return;

                var amount = amountFor(challenge);
                if (amount <= 0)
                    return;

                var progress = (int)Math.Min(challenge.RequiredAmount, challenge.Progress + amount);

                await ctx.DailyChallenges
                    .Where(dc => dc.Id == challenge.Id)
                    .Set(dc => dc.Progress, progress)
                    .UpdateAsync();
            }
            finally
            {
                gate.Release();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to record daily challenge progress for {UserId} in {GuildId}", userId, guildId);
        }
    }

    private static async Task<DailyChallenge> GetOrCreateAsync(MewdekoDb ctx, ulong userId, ulong guildId)
    {
        var today = DateTime.UtcNow.Date;

        var existing = await ctx.DailyChallenges
            .FirstOrDefaultAsync(dc => dc.UserId == userId && dc.GuildId == guildId && dc.Date == today);

        if (existing != null)
            return existing;

        var challenge = GenerateNewChallenge(userId, guildId, today);
        challenge.Id = await ctx.InsertWithInt32IdentityAsync(challenge);

        return challenge;
    }

    private SemaphoreSlim GateFor(ulong userId)
    {
        return gates[(int)(userId % (ulong)gates.Length)];
    }

    private static bool MatchesSpecificGame(string description, string gameKey)
    {
        var game = SpecificGames.FirstOrDefault(g => description == SpecificGameDescription(g));
        return game != null && gameKey.StartsWith(game, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Gets the leaderboard for daily challenge completions.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>List of users and their completion counts.</returns>
    public async Task<List<(ulong UserId, int CompletionCount)>> GetLeaderboard(ulong guildId)
    {
        await using var ctx = await connectionFactory.CreateConnectionAsync();
        var thirtyDaysAgo = DateTime.UtcNow.Date.AddDays(-30);

        return await ctx.DailyChallenges
            .Where(dc => dc.GuildId == guildId && dc.Date >= thirtyDaysAgo && dc.IsCompleted)
            .GroupBy(dc => dc.UserId)
            .Select(g => new
            {
                UserId = g.Key, Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .Select(x => new ValueTuple<ulong, int>(x.UserId, x.Count))
            .ToListAsync();
    }

    private static DailyChallenge GenerateNewChallenge(ulong userId, ulong guildId, DateTime date)
    {
        var rand = new Random();
        var challengeTypes = Enum.GetValues<DailyChallengeType>();
        var challengeType = challengeTypes[rand.Next(challengeTypes.Length)];

        var (requiredAmount, baseReward, description) = challengeType switch
        {
            DailyChallengeType.PlayGames => (5, 100, "Play 5 currency games"),
            DailyChallengeType.WinGames => (3, 200, "Win 3 currency games"),
            DailyChallengeType.SpendCurrency => (500, 150, "Spend 500 currency on games"),
            DailyChallengeType.EarnCurrency => (300, 100, "Earn 300 currency from games"),
            DailyChallengeType.PlaySpecificGame => (3, 150, GetSpecificGameChallenge(rand)),
            _ => (1, 50, "Complete any game")
        };

        return new DailyChallenge
        {
            UserId = userId,
            GuildId = guildId,
            Date = date,
            ChallengeType = challengeType,
            Description = description,
            RequiredAmount = requiredAmount,
            RewardAmount = baseReward + rand.Next(0, 51), // Add up to 50 bonus
            Progress = 0,
            IsCompleted = false
        };
    }

    private static string GetSpecificGameChallenge(Random rand)
    {
        return SpecificGameDescription(SpecificGames[rand.Next(SpecificGames.Length)]);
    }

    private static string SpecificGameDescription(string game)
    {
        return $"Play {game} 3 times";
    }
}

/// <summary>
///     Types of daily challenges.
/// </summary>
public enum DailyChallengeType
{
    /// <summary>Play any currency games.</summary>
    PlayGames,

    /// <summary>Win currency games.</summary>
    WinGames,

    /// <summary>Spend currency on games.</summary>
    SpendCurrency,

    /// <summary>Earn currency from games.</summary>
    EarnCurrency,

    /// <summary>Play a specific game type.</summary>
    PlaySpecificGame
}