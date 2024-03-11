using System.Diagnostics;

namespace Mewdeko.Common.Collections;

/// <summary>
/// Represents a thread-safe hash-based unique collection.
/// </summary>
/// <typeparam name="T">The type of the items in the collection.</typeparam>
/// <remarks>
/// All public members of <see cref="ConcurrentHashSet{T}"/> are thread-safe and may be used
/// concurrently from multiple threads. The Add method returns true if the item was added to the set; false if it already exists.
/// </remarks>
[DebuggerDisplay("Count = {Count}")]
public sealed class ConcurrentHashSet<T> : IReadOnlyCollection<T>, ISet<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, bool> backingStore;

    public ConcurrentHashSet()
        => backingStore = new ConcurrentDictionary<T, bool>();

    public ConcurrentHashSet(IEnumerable<T> values, IEqualityComparer<T>? comparer = null)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        backingStore =
            new ConcurrentDictionary<T, bool>(values.Select(x => new KeyValuePair<T, bool>(x, true)), comparer);
    }

    public IEnumerator<T> GetEnumerator()
        => backingStore.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public bool Add(T item)
        => backingStore.TryAdd(item, true);

    void ICollection<T>.Add(T item)
    {
        Add(item);
    }

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

    public IEqualityComparer<T> Comparer => backingStore.Comparer;

    // Members of ISet<T>
    public void ExceptWith(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        foreach (var item in other)
            TryRemove(item);
    }

    public void IntersectWith(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        RemoveWhere(x => !other.Contains(x));
    }

    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var otherSet = new HashSet<T>(other);
        return otherSet.IsProperSupersetOf(this);
    }

    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var otherSet = new HashSet<T>(other);
        return otherSet.IsProperSubsetOf(this);
    }

    public bool IsSubsetOf(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var otherSet = new HashSet<T>(other);
        return otherSet.IsSupersetOf(this);
    }

    public bool IsSupersetOf(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var otherSet = new HashSet<T>(other);
        return otherSet.IsSubsetOf(this);
    }

    public bool Overlaps(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        return this.Any(other.Contains);
    }

    public bool SetEquals(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var otherSet = new HashSet<T>(other);
        return otherSet.SetEquals(this);
    }

    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        foreach (var item in other)
        {
            if (Contains(item))
                TryRemove(item);
            else
                Add(item);
        }
    }

    public void UnionWith(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        foreach (var item in other)
        {
            Add(item);
        }
    }
}