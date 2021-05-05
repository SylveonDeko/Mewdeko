using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NadekoBot.Core.Services.Database.Models;

namespace NadekoBot.Common.Collections
{
    public class IndexedCollection<T> : IList<T> where T : class, IIndexed
    {
        public List<T> Source { get; }
        private readonly object _locker = new object();

        public IndexedCollection()
        {
            Source = new List<T>();
        }
        public IndexedCollection(IEnumerable<T> source)
        {
            lock (_locker)
            {
                Source = source.OrderBy(x => x.Index).ToList();
                for (var i = 0; i < Source.Count; i++)
                {
                    if (Source[i].Index != i)
                        Source[i].Index = i;
                }
            }
        }

        public static implicit operator List<T>(IndexedCollection<T> x) =>
            x.Source;

        public List<T> ToList() => Source.ToList();

        public IEnumerator<T> GetEnumerator() =>
            Source.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            Source.GetEnumerator();

        public void Add(T item)
        {
            lock (_locker)
            {
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
                if (removed = Source.Remove(item))
                {
                    for (int i = 0; i < Source.Count; i++)
                    {
                        // hm, no idea how ef works, so I don't want to set if it's not changed, 
                        // maybe it will try to update db? 
                        // But most likely it just compares old to new values, meh.
                        if (Source[i].Index != i)
                            Source[i].Index = i;
                    }
                }
            }
            return removed;
        }

        public int Count => Source.Count;
        public bool IsReadOnly => false;
        public int IndexOf(T item) => item.Index;

        public virtual void Insert(int index, T item)
        {
            lock (_locker)
            {
                Source.Insert(index, item);
                for (int i = index; i < Source.Count; i++)
                {
                    Source[i].Index = i;
                }
            }
        }

        public virtual void RemoveAt(int index)
        {
            lock (_locker)
            {
                Source.RemoveAt(index);
                for (int i = index; i < Source.Count; i++)
                {
                    Source[i].Index = i;
                }
            }
        }

        public virtual T this[int index]
        {
            get { return Source[index]; }
            set
            {
                lock (_locker)
                {
                    value.Index = index;
                    Source[index] = value;
                }
            }
        }
    }

}
