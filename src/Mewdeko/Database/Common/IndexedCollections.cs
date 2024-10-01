using System.Diagnostics;

namespace Mewdeko.Database.Common;

/// <summary>
///     Represents a collection of indexed items that implements the IList<T> interface.
/// </summary>
/// <typeparam name="T">The type of elements in the collection. Must be a class and implement IIndexed interface.</typeparam>
public class IndexedCollection<T> : IList<T> where T : class, IIndexed
{
    /// <summary>
    ///     Object used for locking to ensure thread-safety.
    /// </summary>
    private readonly object locker = new();

    /// <summary>
    ///     Initializes a new instance of the IndexedCollection<T> class with an empty list.
    /// </summary>
    public IndexedCollection()
    {
        Source = [];
    }

    /// <summary>
    ///     Initializes a new instance of the IndexedCollection<T> class with the specified source collection.
    /// </summary>
    /// <param name="source">The collection whose elements are copied to the new list.</param>
    public IndexedCollection(IEnumerable<T> source)
    {
        lock (locker)
        {
            Source = source.OrderBy(x => x.Index).ToList();
            UpdateIndexes();
        }
    }

    /// <summary>
    ///     Gets the internal list that stores the collection items.
    /// </summary>
    public List<T> Source { get; }

    /// <summary>
    ///     Gets the number of elements contained in the IndexedCollection<T>.
    /// </summary>
    public int Count
    {
        get
        {
            return Source.Count;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the IndexedCollection<T> is read-only.
    /// </summary>
    public bool IsReadOnly
    {
        get
        {
            return false;
        }
    }

    /// <summary>
    ///     Determines the index of a specific item in the IndexedCollection<T>.
    /// </summary>
    /// <param name="item">The object to locate in the IndexedCollection<T>.</param>
    /// <returns>The index of item if found in the list; otherwise, -1.</returns>
    public int IndexOf(T item)
    {
        Debug.Assert(item != null, $"{nameof(item)} != null");
        return item.Index;
    }

    /// <summary>
    ///     Returns an enumerator that iterates through the IndexedCollection<T>.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        return Source.GetEnumerator();
    }

    /// <summary>
    ///     Returns an enumerator that iterates through the IndexedCollection<T>.
    /// </summary>
    /// <returns>An IEnumerator that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return Source.GetEnumerator();
    }

    /// <summary>
    ///     Adds an item to the IndexedCollection<T>.
    /// </summary>
    /// <param name="item">The object to add to the IndexedCollection<T>.</param>
    public void Add(T item)
    {
        lock (locker)
        {
            Debug.Assert(item != null, $"{nameof(item)} != null");
            item.Index = Source.Count;
            Source.Add(item);
        }
    }

    /// <summary>
    ///     Removes all items from the IndexedCollection<T>.
    /// </summary>
    public virtual void Clear()
    {
        lock (locker)
        {
            Source.Clear();
        }
    }

    /// <summary>
    ///     Determines whether the IndexedCollection<T> contains a specific value.
    /// </summary>
    /// <param name="item">The object to locate in the IndexedCollection<T>.</param>
    /// <returns>true if item is found in the IndexedCollection<T>; otherwise, false.</returns>
    public bool Contains(T item)
    {
        lock (locker)
        {
            return Source.Contains(item);
        }
    }

    /// <summary>
    ///     Copies the elements of the IndexedCollection<T> to an Array, starting at a particular Array index.
    /// </summary>
    /// <param name="array">
    ///     The one-dimensional Array that is the destination of the elements copied from IndexedCollection<T>.
    /// </param>
    /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
        lock (locker)
        {
            Source.CopyTo(array, arrayIndex);
        }
    }

    /// <summary>
    ///     Removes the first occurrence of a specific object from the IndexedCollection<T>.
    /// </summary>
    /// <param name="item">The object to remove from the IndexedCollection<T>.</param>
    /// <returns>true if item was successfully removed from the IndexedCollection<T>; otherwise, false.</returns>
    public virtual bool Remove(T item)
    {
        bool removed;
        lock (locker)
        {
            // ReSharper disable once AssignmentInConditionalExpression
            if (removed = Source.Remove(item))
            {
                for (var i = 0; i < Source.Count; i++)
                    if (Source[i].Index != i)
                        Source[i].Index = i;
            }
        }

        return removed;
    }

    /// <summary>
    ///     Inserts an item to the IndexedCollection<T> at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which item should be inserted.</param>
    /// <param name="item">The object to insert into the IndexedCollection<T>.</param>
    public virtual void Insert(int index, T item)
    {
        lock (locker)
        {
            Source.Insert(index, item);
            for (var i = index; i < Source.Count; i++) Source[i].Index = i;
        }
    }

    /// <summary>
    ///     Removes the IndexedCollection<T> item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to remove.</param>
    public virtual void RemoveAt(int index)
    {
        lock (locker)
        {
            Source.RemoveAt(index);
            for (var i = index; i < Source.Count; i++) Source[i].Index = i;
        }
    }

    /// <summary>
    ///     Gets or sets the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get or set.</param>
    /// <returns>The element at the specified index.</returns>
    public virtual T this[int index]
    {
        get
        {
            return Source[index];
        }
        set
        {
            lock (locker)
            {
                value.Index = index;
                Source[index] = value;
            }
        }
    }

    /// <summary>
    ///     Updates the indexes of all items in the collection to match their position in the list.
    /// </summary>
    public void UpdateIndexes()
    {
        lock (locker)
        {
            for (var i = 0; i < Source.Count; i++)
            {
                if (Source[i].Index != i)
                    Source[i].Index = i;
            }
        }
    }

    /// <summary>
    ///     Implicitly converts an IndexedCollection<T> to a List<T>.
    /// </summary>
    /// <param name="x">The IndexedCollection<T> to convert.</param>
    /// <returns>A List<T> containing the elements of the IndexedCollection<T>.</returns>
    public static implicit operator List<T>(IndexedCollection<T> x)
    {
        return x.Source;
    }

    /// <summary>
    ///     Creates a new List<T> from the IndexedCollection<T>.
    /// </summary>
    /// <returns>A new List<T> containing elements copied from the IndexedCollection<T>.</returns>
    public List<T> ToList()
    {
        return Source.ToList();
    }
}