using Mewdeko.Database.Common;

namespace Mewdeko.Modules.Permissions.Common;

/// <summary>
///     Represents a collection of permissions with indexed access, supporting synchronization for concurrent operations.
/// </summary>
/// <typeparam name="T">
///     The type of permissions in the collection, constrained to types that implement
///     <see cref="IIndexed" />.
/// </typeparam>
public class PermissionsCollection<T> : IndexedCollection<T> where T : class, IIndexed
{
    private readonly object localLocker = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="PermissionsCollection{T}" /> class with the specified source
    ///     collection.
    /// </summary>
    /// <param name="source">The collection of permissions to initialize the collection with.</param>
    public PermissionsCollection(IEnumerable<T> source) : base(source)
    {
    }

    /// <summary>
    ///     Gets or sets the element at the specified index.
    /// </summary>
    /// <value>The element at the specified index.</value>
    /// <param name="index">The zero-based index of the element to get or set.</param>
    /// <returns>The element at the specified index.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when attempting to set the first element, which is reserved.</exception>
    public override T this[int index]
    {
        get
        {
            return Source[index];
        }
        set
        {
            lock (localLocker)
            {
                if (index == 0) // can't set first element. It's always allow all
                    throw new IndexOutOfRangeException(nameof(index));
                base[index] = value;
            }
        }
    }

    /// <summary>
    ///     Defines an implicit conversion of a <see cref="PermissionsCollection{T}" /> to a <see cref="List{T}" />.
    /// </summary>
    /// <param name="x">The <see cref="PermissionsCollection{T}" /> to convert.</param>
    public static implicit operator List<T>(PermissionsCollection<T> x)
    {
        return x.Source;
    }

    /// <summary>
    ///     Removes all items from the collection, except the first item, which is always allowed.
    /// </summary>
    public override void Clear()
    {
        lock (localLocker)
        {
            var first = Source[0];
            base.Clear();
            Source[0] = first;
        }
    }

    /// <summary>
    ///     Removes the first occurrence of a specific object from the collection.
    /// </summary>
    /// <param name="item">The object to remove from the collection.</param>
    /// <returns>true if <paramref name="item" /> was successfully removed from the collection; otherwise, false.</returns>
    /// <exception cref="ArgumentException">Thrown when attempting to remove the first element, which is reserved.</exception>
    public override bool Remove(T item)
    {
        bool removed;
        lock (localLocker)
        {
            if (Source.IndexOf(item) == 0)
                throw new ArgumentException("You can't remove first permission (allow all)");
            removed = base.Remove(item);
        }

        return removed;
    }

    /// <summary>
    ///     Inserts an element into the collection at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
    /// <param name="item">The object to insert.</param>
    /// <exception cref="IndexOutOfRangeException">Thrown when attempting to insert at the first position, which is reserved.</exception>
    public override void Insert(int index, T item)
    {
        lock (localLocker)
        {
            if (index == 0) // can't insert in the first place. The first item is always allow all.
                throw new IndexOutOfRangeException(nameof(index));
            base.Insert(index, item);
        }
    }

    /// <summary>
    ///     Removes the element at the specified index of the collection.
    /// </summary>
    /// <param name="index">The zero-based index of the element to remove.</param>
    /// <exception cref="IndexOutOfRangeException">Thrown when attempting to remove the first element, which is reserved.</exception>
    public override void RemoveAt(int index)
    {
        lock (localLocker)
        {
            if (index == 0) // you can't remove the first permission (allow all)
                throw new IndexOutOfRangeException(nameof(index));

            base.RemoveAt(index);
        }
    }
}