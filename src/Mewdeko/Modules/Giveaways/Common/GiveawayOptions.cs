namespace Mewdeko.Modules.Giveaways.Common;

/// <summary>
///     A prize paid out automatically to a giveaway's winners.
/// </summary>
public enum GiveawayPrizeType
{
    /// <summary>
    ///     No automatic prize.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Experience points.
    /// </summary>
    Xp = 1,

    /// <summary>
    ///     Server currency.
    /// </summary>
    Coins = 2
}

/// <summary>
///     Values of a giveaway's <c>Ended</c> column.
/// </summary>
public static class GiveawayStatus
{
    /// <summary>
    ///     The giveaway is running.
    /// </summary>
    public const int Active = 0;

    /// <summary>
    ///     The giveaway has finished.
    /// </summary>
    public const int Ended = 1;

    /// <summary>
    ///     The giveaway is saved but has not been started.
    /// </summary>
    public const int Draft = 2;
}

/// <summary>
///     Reads and writes the role based extra entries stored on a giveaway.
/// </summary>
public static class GiveawayExtraEntries
{
    /// <summary>
    ///     Parses space separated <c>roleId:extraEntries</c> pairs.
    /// </summary>
    /// <param name="value">The stored value.</param>
    /// <returns>The extra entries each role grants.</returns>
    public static Dictionary<ulong, int> Parse(string? value)
    {
        var result = new Dictionary<ulong, int>();

        if (string.IsNullOrWhiteSpace(value))
            return result;

        foreach (var pair in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split(':');

            if (parts.Length == 2 && ulong.TryParse(parts[0], out var roleId) &&
                int.TryParse(parts[1], out var extra) && extra > 0)
                result[roleId] = extra;
        }

        return result;
    }

    /// <summary>
    ///     Turns extra entries back into the stored form.
    /// </summary>
    /// <param name="entries">The extra entries each role grants.</param>
    /// <returns>The stored value, or <see langword="null" /> when there are none.</returns>
    public static string? Format(IReadOnlyDictionary<ulong, int> entries)
    {
        return entries.Count == 0 ? null : string.Join(' ', entries.Select(x => $"{x.Key}:{x.Value}"));
    }

    /// <summary>
    ///     Works out how many entries a member holds.
    /// </summary>
    /// <param name="roleIds">The roles the member has.</param>
    /// <param name="entries">The extra entries each role grants.</param>
    /// <returns>One entry plus every extra entry their roles grant.</returns>
    public static int WeightFor(IEnumerable<ulong> roleIds, IReadOnlyDictionary<ulong, int> entries)
    {
        return 1 + roleIds.Where(entries.ContainsKey).Sum(x => entries[x]);
    }

    /// <summary>
    ///     Picks winners at random, giving members with extra entries proportionally better odds.
    /// </summary>
    /// <param name="candidates">Each eligible member with the number of entries they hold.</param>
    /// <param name="count">How many winners to pick.</param>
    /// <typeparam name="T">The candidate type.</typeparam>
    /// <returns>The winners, each member at most once.</returns>
    public static List<T> PickWinners<T>(IEnumerable<(T Candidate, int Weight)> candidates, int count)
    {
        var pool = candidates.Select(x => (x.Candidate, Weight: Math.Max(1, x.Weight))).ToList();
        var winners = new List<T>();

        while (winners.Count < count && pool.Count > 0)
        {
            var roll = Random.Shared.Next(pool.Sum(x => x.Weight));
            var index = 0;

            while (roll >= pool[index].Weight)
            {
                roll -= pool[index].Weight;
                index++;
            }

            winners.Add(pool[index].Candidate);
            pool.RemoveAt(index);
        }

        return winners;
    }
}
