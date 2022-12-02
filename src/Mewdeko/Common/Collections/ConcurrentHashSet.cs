using System.Diagnostics;

namespace Mewdeko.Common.Collections;

/// <summary>
/// Represents a thread-safe hash-based unique collection.
/// </summary>
/// <typeparam name="T">The type of the items in the collection.</typeparam>
/// <remarks>
/// All public members of <see cref="ConcurrentHashSet{T}"/> are thread-safe and may be used
/// concurrently from multiple threads.
/// </remarks>
[DebuggerDisplay("Count = {Count}")]
public sealed class ConcurrentHashSet<T> : IReadOnlyCollection<T>, ICollection<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, bool> backingStore;

    public ConcurrentHashSet()
        => backingStore = new ConcurrentDictionary<T, bool>();

    public ConcurrentHashSet(IEnumerable<T> values, IEqualityComparer<T>? comparer = null)
        => backingStore = new ConcurrentDictionary<T, bool>(values.Select(x => new KeyValuePair<T, bool>(x, true)), comparer);

    public IEnumerator<T> GetEnumerator()
        => backingStore.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    /// <summary>
    ///     Adds the specified item to the <see cref="ConcurrentHashSet{T}" />.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <returns>
    ///     true if the items was added to the <see cref="ConcurrentHashSet{T}" />
    ///     successfully; false if it already exists.
    /// </returns>
    /// <exception cref="T:System.OverflowException">
    ///     The <see cref="ConcurrentHashSet{T}" />
    ///     contains too many items.
    /// </exception>
    public bool Add(T item)
        => backingStore.TryAdd(item, true);

    void ICollection<T>.Add(T item)
        => Add(item);

    public void Clear()
        => backingStore.Clear();

    public bool Contains(T item)
        => backingStore.ContainsKey(item);

    public void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        if (arrayIndex >= array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        CopyToInternal(array, arrayIndex);
    }

    private void CopyToInternal(T[] array, int arrayIndex)
    {
        var len = array.Length;
        foreach (var (k, _) in backingStore)
        {
            if (arrayIndex >= len)
                throw new IndexOutOfRangeException(nameof(arrayIndex));

            array[arrayIndex++] = k;
        }
    }

    bool ICollection<T>.Remove(T item)
        => TryRemove(item);

    public bool TryRemove(T item)
        => backingStore.TryRemove(item, out _);

    public void RemoveWhere(Func<T, bool> predicate)
    {
        foreach (var elem in this.Where(predicate))
            TryRemove(elem);
    }

    public int Count
        => backingStore.Count;

    public bool IsReadOnly
        => false;
}