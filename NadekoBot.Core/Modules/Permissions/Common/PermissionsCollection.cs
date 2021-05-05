using System;
using System.Collections.Generic;
using NadekoBot.Common.Collections;
using NadekoBot.Core.Services.Database.Models;

namespace NadekoBot.Modules.Permissions.Common
{
    public class PermissionsCollection<T> : IndexedCollection<T> where T : class, IIndexed
    {
        private readonly object _localLocker = new object();
        public PermissionsCollection(IEnumerable<T> source) : base(source)
        {
        }

        public static implicit operator List<T>(PermissionsCollection<T> x) => 
            x.Source;

        public override void Clear()
        {
            lock (_localLocker)
            {
                var first = Source[0];
                base.Clear();
                Source[0] = first;
            }
        }

        public override bool Remove(T item)
        {
            bool removed;
            lock (_localLocker)
            {
                if(Source.IndexOf(item) == 0)
                    throw new ArgumentException("You can't remove first permsission (allow all)");
                removed = base.Remove(item);
            }
            return removed;
        }

        public override void Insert(int index, T item)
        {
            lock (_localLocker)
            {
                if(index == 0) // can't insert on first place. Last item is always allow all.
                    throw new IndexOutOfRangeException(nameof(index));
                base.Insert(index, item);
            }
        }

        public override void RemoveAt(int index)
        {
            lock (_localLocker)
            {
                if(index == 0) // you can't remove first permission (allow all)
                    throw new IndexOutOfRangeException(nameof(index));

                base.RemoveAt(index);
            }
        }

        public override T this[int index] {
            get => Source[index];
            set {
                lock (_localLocker)
                {
                    if(index == 0) // can't set first element. It's always allow all
                        throw new IndexOutOfRangeException(nameof(index));
                    base[index] = value;
                }
            }
        }
    }

}
