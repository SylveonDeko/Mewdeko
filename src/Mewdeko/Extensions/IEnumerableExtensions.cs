using System.Security.Cryptography;
using Mewdeko.Database.Common;

namespace Mewdeko.Extensions;

/// <summary>
///     Extensions for IEnumerable objects.
/// </summary>
public static class EnumerableExtensions
{
    private static readonly Random Random = new();

    /// <summary>
    ///     Joins the elements of a sequence into a single string using the specified separator and optional mapping function.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="data">The sequence to join.</param>
    /// <param name="separator">The separator to use between elements.</param>
    /// <param name="func">An optional mapping function to apply to each element before joining.</param>
    /// <returns>The concatenated string.</returns>
    public static string Join<T>(this IEnumerable<T> data, char separator, Func<T, string>? func = null)
    {
        return string.Join(separator, data.Select(func ?? (x => x?.ToString() ?? string.Empty)));
    }

    /// <summary>
    ///     Returns a random element from the specified sequence.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="list">The sequence to select from.</param>
    /// <returns>A randomly selected element from the sequence.</returns>
    public static T GetRandomElement<T>(this IEnumerable<T> list)
    {
        return !list.Any() ? default : list.ElementAt(Random.Next(list.Count()));
    }

    /// <summary>
    ///     Joins the elements of a sequence into a single string using the specified separator and optional mapping function.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="data">The sequence to join.</param>
    /// <param name="separator">The separator to use between elements.</param>
    /// <param name="func">An optional mapping function to apply to each element before joining.</param>
    /// <returns>The concatenated string.</returns>
    public static string JoinWith<T>(this IEnumerable<T> data, char separator, Func<T, string>? func = null)
    {
        func ??= x => x?.ToString() ?? string.Empty;
        return string.Join(separator, data.Select(func));
    }

    /// <summary>
    ///     Joins the elements of a sequence into a single string using the specified separator and optional mapping function.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="data">The sequence to join.</param>
    /// <param name="separator">The separator to use between elements.</param>
    /// <param name="func">An optional mapping function to apply to each element before joining.</param>
    /// <returns>The concatenated string.</returns>
    public static string JoinWith<T>(this IEnumerable<T> data, string separator, Func<T, string>? func = null)
    {
        func ??= x => x?.ToString() ?? string.Empty;
        return string.Join(separator, data.Select(func));
    }

    /// <summary>
    ///     Returns distinct elements from a sequence by using a specified key selector function.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <typeparam name="TU">The type of the keys returned by the key selector function.</typeparam>
    /// <param name="data">The sequence to remove duplicate elements from.</param>
    /// <param name="getKey">A function to extract the key for each element.</param>
    /// <returns>An IEnumerable containing distinct elements from the source sequence.</returns>
    public static IEnumerable<T> Distinct<T, TU>(this IEnumerable<T> data, Func<T, TU> getKey)
    {
        return data.GroupBy(getKey).Select(x => x.First());
    }

    /// <summary>
    ///     Moves an item within a list to a new index.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list containing the item to move.</param>
    /// <param name="item">The item to move.</param>
    /// <param name="newIndex">The index to move the item to.</param>
    public static void Move<T>(this List<T> list, T item, int newIndex)
    {
        if (Equals(item, default(T))) return;
        var oldIndex = list.IndexOf(item);
        if (oldIndex <= -1) return;
        list.RemoveAt(oldIndex);
        if (newIndex > oldIndex) newIndex--;
        list.Insert(newIndex, item);
    }

    /// <summary>
    ///     Asynchronously waits for all tasks in the collection to complete and returns their results.
    /// </summary>
    /// <typeparam name="T">The type of results returned by the tasks.</typeparam>
    /// <param name="tasks">The collection of tasks to wait for.</param>
    /// <returns>A list containing the results of all completed tasks.</returns>
    public static async Task<List<T>> GetResults<T>(this IEnumerable<Task<T>> tasks)
    {
        var res = new List<T>();

        // Awaits each task and adds the result to the result list.
        foreach (var task in tasks)
            res.Add(await task.ConfigureAwait(false));

        return res;
    }

    /// <summary>
    ///     Randomizes the order of elements in the sequence by performing the Fisher-Yates shuffle.
    /// </summary>
    /// <typeparam name="T">The type of items in the sequence.</typeparam>
    /// <param name="items">The sequence of items to shuffle.</param>
    /// <returns>An IReadOnlyList with the elements shuffled.</returns>
    public static IReadOnlyList<T> Shuffle<T>(this IEnumerable<T> items)
    {
        using var provider = RandomNumberGenerator.Create();
        var list = items.ToList();
        var n = list.Count;
        while (n > 1)
        {
            var box = new byte[n / byte.MaxValue + 1];
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

    /// <summary>
    ///     Executes an action on each element in the sequence.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="elems">The sequence of elements.</param>
    /// <param name="exec">The action to execute on each element.</param>
    public static void ForEach<T>(this IEnumerable<T> elems, Action<T> exec)
    {
        var realElems = elems.ToList();
        foreach (var elem in realElems) exec(elem);
    }

    /// <summary>
    ///     Converts a sequence of key-value pairs into a concurrent dictionary.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <param name="dict">The sequence of key-value pairs to convert.</param>
    /// <returns>A new ConcurrentDictionary containing the key-value pairs from the sequence.</returns>
    public static ConcurrentDictionary<TKey, TValue> ToConcurrent<TKey, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> dict)
        where TKey : notnull
    {
        return new ConcurrentDictionary<TKey, TValue>(dict);
    }

    /// <summary>
    ///     Converts a sequence of items into an indexed collection.
    /// </summary>
    /// <typeparam name="T">The type of items in the sequence.</typeparam>
    /// <param name="enumerable">The sequence of items to convert.</param>
    /// <returns>An IndexedCollection containing the items from the sequence.</returns>
    public static IndexedCollection<T> ToIndexed<T>(this IEnumerable<T> enumerable)
        where T : class, IIndexed
    {
        return new IndexedCollection<T>(enumerable);
    }


    /// <summary>
    ///     Creates a task that will complete when all of the <see cref="Task{TResult}" /> objects in an enumerable
    ///     collection have completed
    /// </summary>
    /// <param name="tasks">The tasks to wait on for completion.</param>
    /// <typeparam name="TResult">The type of the completed task.</typeparam>
    /// <returns>A task that represents the completion of all of the supplied tasks.</returns>
    public static Task<TResult[]> WhenAll<TResult>(this IEnumerable<Task<TResult>> tasks)
    {
        return Task.WhenAll(tasks);
    }

    /// <summary>
    ///     Creates a task that will complete when all of the <see cref="Task" /> objects in an enumerable
    ///     collection have completed
    /// </summary>
    /// <param name="tasks">The tasks to wait on for completion.</param>
    /// <returns>A task that represents the completion of all of the supplied tasks.</returns>
    public static Task WhenAll(this IEnumerable<Task> tasks)
    {
        return Task.WhenAll(tasks);
    }
}