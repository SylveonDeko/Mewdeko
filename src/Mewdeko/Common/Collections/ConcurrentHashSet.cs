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
    /// <summary>
    /// The backing store for the set, represented as a concurrent dictionary.
    /// </summary>
    private readonly ConcurrentDictionary<T, bool> backingStore;

    /// <summary>
    /// Initializes a new instance of the ConcurrentHashSet class.
    /// </summary>
    public ConcurrentHashSet()
        => backingStore = new ConcurrentDictionary<T, bool>();

    /// <summary>
    /// Initializes a new instance of the ConcurrentHashSet class with the specified values and comparer.
    /// </summary>
    /// <param name="values">The values to initialize the set with.</param>
    /// <param name="comparer">The comparer to use for item equality.</param>
    public ConcurrentHashSet(IEnumerable<T> values, IEqualityComparer<T>? comparer = null)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        backingStore =
            new ConcurrentDictionary<T, bool>(values.Select(x => new KeyValuePair<T, bool>(x, true)), comparer);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the set.
    /// </summary>
    /// <returns>An enumerator for the set.</returns>
    public IEnumerator<T> GetEnumerator()
        => backingStore.Keys.GetEnumerator();

    /// <summary>
    /// Returns an enumerator that iterates through the set.
    /// </summary>
    /// <returns>An enumerator for the set.</returns>
    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    /// <summary>
    /// Adds an item to the set.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <returns>true if the item was added to the set; false if the item already exists.</returns>
    public bool Add(T item)
        => backingStore.TryAdd(item, true);

    /// <summary>
    /// Adds an item to the set.
    /// </summary>
    /// <param name="item">The item to add.</param>
    void ICollection<T>.Add(T item)
    {
        Add(item);
    }

    /// <summary>
    /// Removes all items from the set.
    /// </summary>
    public void Clear()
        => backingStore.Clear();

    /// <summary>
    /// Determines whether the set contains a specific item.
    /// </summary>
    /// <param name="item">The item to locate in the set.</param>
    /// <returns>true if the item is found in the set; otherwise, false.</returns>
    public bool Contains(T item)
        => backingStore.ContainsKey(item);

    /// <summary>
    /// Copies the elements of the set to an array, starting at a particular array index.
    /// </summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the set.</param>
    /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        if (arrayIndex >= array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        CopyToInternal(array, arrayIndex);
    }

    /// <summary>
    /// Copies the elements of the set to an array, starting at a particular array index.
    /// </summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the set.</param>
    /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
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

    /// <summary>
    /// Removes the first occurrence of a specific object from the set.
    /// </summary>
    /// <param name="item">The object to remove from the set.</param>
    /// <returns>true if item was successfully removed from the set; otherwise, false. This method also returns false if item is not found in the set.</returns>
    bool ICollection<T>.Remove(T item)
        => TryRemove(item);

    /// <summary>
    /// Removes the first occurrence of a specific object from the set.
    /// </summary>
    /// <param name="item">The object to remove from the set.</param>
    /// <returns>true if item was successfully removed from the set; otherwise, false. This method also returns false if item is not found in the set.</returns>
    public bool TryRemove(T item)
        => backingStore.TryRemove(item, out _);

    /// <summary>
    /// Removes all items from the set that satisfy the specified predicate.
    /// </summary>
    /// <param name="predicate">The predicate to determine which items to remove.</param>
    public void RemoveWhere(Func<T, bool> predicate)
    {
        foreach (var elem in this.Where(predicate))
            TryRemove(elem);
    }

    /// <summary>
    /// Gets the number of elements contained in the set.
    /// </summary>
    public int Count
        => backingStore.Count;

    /// <summary>
    /// Gets a value indicating whether the set is read-only.
    /// </summary>
    public bool IsReadOnly
        => false;

    /// <summary>
    /// Gets the equality comparer that is used to determine equality of keys in the set.
    /// </summary>
    public IEqualityComparer<T> Comparer => backingStore.Comparer;

    // Members of ISet<T>
    /// <summary>
    /// Removes all elements in the specified collection from the current set.
    /// </summary>
    /// <param name="other">The collection of items to remove from the set.</param>
    public void ExceptWith(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        foreach (var item in other)
            TryRemove(item);
    }

    /// <summary>
    /// Modifies the current set so that it contains only elements that are also in a specified collection.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    public void IntersectWith(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        RemoveWhere(x => !other.Contains(x));
    }

    /// <summary>
    /// Determines whether the current set is a proper (strict) subset of a specified collection.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>true if the current set is a proper subset of other; otherwise, false.</returns>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var otherSet = new HashSet<T>(other);
        return otherSet.IsProperSupersetOf(this);
    }

    /// <summary>
    /// Determines whether the current set is a proper (strict) superset of a specified collection.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>true if the current set is a proper superset of other; otherwise, false.</returns>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var otherSet = new HashSet<T>(other);
        return otherSet.IsProperSubsetOf(this);
    }

    /// <summary>
    /// Determines whether a set is a subset of a specified collection.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>true if the current set is a subset of other; otherwise, false.</returns>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var otherSet = new HashSet<T>(other);
        return otherSet.IsSupersetOf(this);
    }

    /// <summary>
    /// Determines whether the current set is a superset of a specified collection.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>true if the current set is a superset of other; otherwise, false.</returns>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var otherSet = new HashSet<T>(other);
        return otherSet.IsSubsetOf(this);
    }

    /// <summary>
    /// Determines whether the current set overlaps with the specified collection.
    /// </summary>
    /// <param name="other">The collection to check for overlap with the current set.</param>
    /// <returns>true if the current set and other share at least one common element; otherwise, false.</returns>
    public bool Overlaps(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        return this.Any(other.Contains);
    }

    /// <summary>
    /// Determines whether the current set and the specified collection contain the same elements.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    /// <returns>true if the current set is equal to other; otherwise, false.</returns>
    public bool SetEquals(IEnumerable<T> other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var otherSet = new HashSet<T>(other);
        return otherSet.SetEquals(this);
    }

    /// <summary>
    /// Modifies the current set so that it contains only elements that are present either in the current set or in the specified collection, but not both.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
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

    /// <summary>
    /// Modifies the current set so that it contains all elements that are present in the current set, in the specified collection, or in both.
    /// </summary>
    /// <param name="other">The collection to compare to the current set.</param>
    public void UnionWith(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var item in other)
        {
            Add(item);
        }
    }
}