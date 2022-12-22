using System.Security.Cryptography;
using System.Threading.Tasks;
using Mewdeko.Database.Common;

namespace Mewdeko.Extensions;

public static class EnumerableExtensions
{
    private static readonly Random Random = new();

    public static string Join<T>(this IEnumerable<T> data, char separator, Func<T, string>? func = null)
        => string.Join(separator, data.Select(func ?? (x => x?.ToString() ?? string.Empty)));

    public static T GetRandomElement<T>(this IEnumerable<T> list)
        => !list.Any() ? default(T) : list.ElementAt(Random.Next(list.Count()));

    public static string JoinWith<T>(this IEnumerable<T> data, char separator, Func<T, string>? func = null)
    {
        func ??= x => x?.ToString() ?? string.Empty;

        return string.Join(separator, data.Select(func));
    }

    public static string JoinWith<T>(this IEnumerable<T> data, string separator, Func<T, string>? func = null)
    {
        func ??= x => x?.ToString() ?? string.Empty;

        return string.Join(separator, data.Select(func));
    }

    public static IEnumerable<T> Distinct<T, TU>(this IEnumerable<T> data, Func<T, TU> getKey) =>
        data.GroupBy(getKey)
            .Select(x => x.First());

    public static void Move<T>(this List<T> list, T item, int newIndex)
    {
        if (Equals(item, default(T))) return;
        var oldIndex = list.IndexOf(item);
        if (oldIndex <= -1) return;
        list.RemoveAt(oldIndex);
        if (newIndex > oldIndex) newIndex--;
        list.Insert(newIndex, item);
    }

    public static async Task<List<T>> GetResults<T>(this IEnumerable<Task<T>> tasks)
    {
        var res = new List<T>();

        // Awaits each task and adds the result to the result list.
        foreach (var task in tasks)
            res.Add(await task.ConfigureAwait(false));

        return res;
    }

    /// <summary>
    ///     Randomize element order by performing the Fisher-Yates shuffle
    /// </summary>
    /// <typeparam name="T">Item type</typeparam>
    /// <param name="items">Items to shuffle</param>
    public static IReadOnlyList<T> Shuffle<T>(this IEnumerable<T> items)
    {
        using var provider = RandomNumberGenerator.Create();
        var list = items.ToList();
        var n = list.Count;
        while (n > 1)
        {
            var box = new byte[(n / byte.MaxValue) + 1];
            int boxSum;
            do
            {
                provider.GetBytes(box);
                boxSum = box.Sum(b => b);
            } while (!(boxSum < n * (byte.MaxValue * box.Length / n)));

            var k = boxSum % n;
            n--;
            (list[k], list[n]) = (list[n], list[k]);
        }

        return list;
    }

    public static void ForEach<T>(this IEnumerable<T> elems, Action<T> exec)
    {
        var realElems = elems.ToList();
        foreach (var elem in realElems) exec(elem);
    }

    public static ConcurrentDictionary<TKey, TValue> ToConcurrent<TKey, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> dict)
        where TKey : notnull => new(dict);

    public static IndexedCollection<T> ToIndexed<T>(this IEnumerable<T> enumerable)
        where T : class, IIndexed => new(enumerable);

    /// <summary>
    ///     Creates a task that will complete when all of the <see cref="Task{TResult}" /> objects in an enumerable
    ///     collection have completed
    /// </summary>
    /// <param name="tasks">The tasks to wait on for completion.</param>
    /// <typeparam name="TResult">The type of the completed task.</typeparam>
    /// <returns>A task that represents the completion of all of the supplied tasks.</returns>
    public static Task<TResult[]> WhenAll<TResult>(this IEnumerable<Task<TResult>> tasks)
        => Task.WhenAll(tasks);

    /// <summary>
    ///     Creates a task that will complete when all of the <see cref="Task" /> objects in an enumerable
    ///     collection have completed
    /// </summary>
    /// <param name="tasks">The tasks to wait on for completion.</param>
    /// <returns>A task that represents the completion of all of the supplied tasks.</returns>
    public static Task WhenAll(this IEnumerable<Task> tasks)
        => Task.WhenAll(tasks);
}