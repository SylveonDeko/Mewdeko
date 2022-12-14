using System.Collections;
using System.Diagnostics;
using Mewdeko.Database.Models;

namespace Mewdeko.Database.Common;

public class IndexedCollection<T> : IList<T> where T : class, IIndexed
{
    private readonly object _locker = new();

    public IndexedCollection() => Source = new List<T>();

    public IndexedCollection(IEnumerable<T> source)
    {
        lock (_locker)
        {
            Source = source.OrderBy(x => x.Index).ToList();
            UpdateIndexes();
        }
    }

    public List<T> Source { get; }

    public int Count => Source.Count;
    public bool IsReadOnly => false;

    public int IndexOf(T item)
    {
        Debug.Assert(item != null, $"{nameof(item)} != null");
        return item.Index;
    }

    public IEnumerator<T> GetEnumerator() => Source.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Source.GetEnumerator();

    public void Add(T item)
    {
        lock (_locker)
        {
            Debug.Assert(item != null, $"{nameof(item)} != null");
            item.Index = Source.Count;
            Source.Add(item);
        }
    }

    public virtual void Clear()
    {
        lock (_locker)
        {
            Source.Clear();
        }
    }

    public bool Contains(T item)
    {
        lock (_locker)
        {
            return Source.Contains(item);
        }
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        lock (_locker)
        {
            Source.CopyTo(array, arrayIndex);
        }
    }

    public virtual bool Remove(T item)
    {
        bool removed;
        lock (_locker)
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

    public virtual void Insert(int index, T item)
    {
        lock (_locker)
        {
            Source.Insert(index, item);
            for (var i = index; i < Source.Count; i++) Source[i].Index = i;
        }
    }

    public virtual void RemoveAt(int index)
    {
        lock (_locker)
        {
            Source.RemoveAt(index);
            for (var i = index; i < Source.Count; i++) Source[i].Index = i;
        }
    }

    public virtual T this[int index]
    {
        get => Source[index];
        set
        {
            lock (_locker)
            {
                value.Index = index;
                Source[index] = value;
            }
        }
    }

    public void UpdateIndexes()
    {
        lock (_locker)
        {
            for (var i = 0; i < Source.Count; i++)
            {
                if (Source[i].Index != i)
                    Source[i].Index = i;
            }
        }
    }

    public static implicit operator List<T>(IndexedCollection<T> x) => x.Source;

    public List<T> ToList() => Source.ToList();
}