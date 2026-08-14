using System.Security.Cryptography;

namespace Mewdeko.Modules.Currency.Common;

/// <summary>
///     Random number source for everything in the currency module that decides an outcome involving money.
/// </summary>
/// <remarks>
///     The games previously constructed a fresh <see cref="Random" /> per command. That is seeded from
///     the system clock, so calls landing in the same tick produced identical results, and a player who
///     could time their commands could see the same roll twice. Drawing from the system CSPRNG removes
///     both the seeding problem and any question of predictability, at a cost that is irrelevant next to
///     the surrounding database and Discord calls.
/// </remarks>
public static class CurrencyRng
{
    /// <summary>
    ///     Returns a non-negative random integer below <paramref name="maxExclusive" />.
    /// </summary>
    /// <param name="maxExclusive">The exclusive upper bound. Must be positive.</param>
    /// <returns>A random integer in [0, <paramref name="maxExclusive" />).</returns>
    public static int Next(int maxExclusive)
    {
        return maxExclusive <= 1 ? 0 : RandomNumberGenerator.GetInt32(maxExclusive);
    }

    /// <summary>
    ///     Returns a random integer in the given range.
    /// </summary>
    /// <param name="minInclusive">The inclusive lower bound.</param>
    /// <param name="maxExclusive">The exclusive upper bound.</param>
    /// <returns>A random integer in [<paramref name="minInclusive" />, <paramref name="maxExclusive" />).</returns>
    public static int Next(int minInclusive, int maxExclusive)
    {
        return maxExclusive <= minInclusive ? minInclusive : RandomNumberGenerator.GetInt32(minInclusive, maxExclusive);
    }

    /// <summary>
    ///     Returns a random amount of currency within an inclusive range.
    /// </summary>
    /// <param name="minInclusive">The smallest permitted value.</param>
    /// <param name="maxInclusive">The largest permitted value.</param>
    /// <returns>A random value in [<paramref name="minInclusive" />, <paramref name="maxInclusive" />].</returns>
    public static long NextAmount(long minInclusive, long maxInclusive)
    {
        if (maxInclusive <= minInclusive)
            return minInclusive;

        var span = (ulong)(maxInclusive - minInclusive) + 1;
        return minInclusive + (long)(NextULong() % span);
    }

    /// <summary>
    ///     Returns a random double in [0, 1).
    /// </summary>
    /// <returns>A uniformly distributed value in [0, 1).</returns>
    public static double NextDouble()
    {
        return (NextULong() >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>
    ///     Rolls against a percentage chance.
    /// </summary>
    /// <param name="percent">The chance of success, from 0 to 100.</param>
    /// <returns><see langword="true" /> with probability <paramref name="percent" />/100.</returns>
    public static bool Chance(int percent)
    {
        return percent > 0 && (percent >= 100 || Next(100) < percent);
    }

    /// <summary>
    ///     Picks a uniformly random element from a list.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The list to pick from. Must not be empty.</param>
    /// <returns>A randomly chosen element.</returns>
    public static T Pick<T>(IReadOnlyList<T> items)
    {
        return items[Next(items.Count)];
    }

    /// <summary>
    ///     Shuffles a list in place using a Fisher-Yates pass.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The list to shuffle.</param>
    public static void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private static ulong NextULong()
    {
        Span<byte> buffer = stackalloc byte[8];
        RandomNumberGenerator.Fill(buffer);
        return BitConverter.ToUInt64(buffer);
    }
}