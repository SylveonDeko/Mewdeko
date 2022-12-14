using Mewdeko.Database.Common;

namespace Mewdeko.Modules.Permissions.Common;

public class PermissionsCollection<T> : IndexedCollection<T> where T : class, IIndexed
{
    private readonly object localLocker = new();

    public PermissionsCollection(IEnumerable<T> source) : base(source)
    {
    }

    public override T this[int index]
    {
        get => Source[index];
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

    public static implicit operator List<T>(PermissionsCollection<T> x) => x.Source;

    public override void Clear()
    {
        lock (localLocker)
        {
            var first = Source[0];
            base.Clear();
            Source[0] = first;
        }
    }

    public override bool Remove(T item)
    {
        bool removed;
        lock (localLocker)
        {
            if (Source.IndexOf(item) == 0)
                throw new ArgumentException("You can't remove first permsission (allow all)");
            removed = base.Remove(item);
        }

        return removed;
    }

    public override void Insert(int index, T item)
    {
        lock (localLocker)
        {
            if (index == 0) // can't insert on first place. Last item is always allow all.
                throw new IndexOutOfRangeException(nameof(index));
            base.Insert(index, item);
        }
    }

    public override void RemoveAt(int index)
    {
        lock (localLocker)
        {
            if (index == 0) // you can't remove first permission (allow all)
                throw new IndexOutOfRangeException(nameof(index));

            base.RemoveAt(index);
        }
    }
}