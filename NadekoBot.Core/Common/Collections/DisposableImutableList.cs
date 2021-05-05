using System;
using System.Collections;
using System.Collections.Generic;

namespace NadekoBot.Common.Collections
{
    public static class DisposableReadOnlyListExtensions
    {
        public static IDisposableReadOnlyList<T> AsDisposable<T>(this IReadOnlyList<T> arr) where T : IDisposable
            => new DisposableReadOnlyList<T>(arr);

        public static IDisposableReadOnlyList<KeyValuePair<TKey, TValue>> AsDisposable<TKey, TValue>(this IReadOnlyList<KeyValuePair<TKey, TValue>> arr) where TValue : IDisposable
            => new DisposableReadOnlyList<TKey, TValue>(arr);
    }

    public interface IDisposableReadOnlyList<T> : IReadOnlyList<T>, IDisposable
    {
    }

    public sealed class DisposableReadOnlyList<T> : IDisposableReadOnlyList<T>
        where T : IDisposable
    {
        private readonly IReadOnlyList<T> _arr;

        public int Count => _arr.Count;

        public T this[int index] => _arr[index];

        public DisposableReadOnlyList(IReadOnlyList<T> arr)
        {
            this._arr = arr;
        }

        public IEnumerator<T> GetEnumerator()
            => _arr.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => _arr.GetEnumerator();

        public void Dispose()
        {
            foreach (var item in _arr)
            {
                item.Dispose();
            }
        }
    }

    public sealed class DisposableReadOnlyList<T, U> : IDisposableReadOnlyList<KeyValuePair<T, U>>
        where U : IDisposable
    {
        private readonly IReadOnlyList<KeyValuePair<T, U>> _arr;

        public int Count => _arr.Count;

        KeyValuePair<T, U> IReadOnlyList<KeyValuePair<T, U>>.this[int index] => _arr[index];

        public DisposableReadOnlyList(IReadOnlyList<KeyValuePair<T, U>> arr)
        {
            this._arr = arr;
        }

        public IEnumerator<KeyValuePair<T, U>> GetEnumerator() =>
            _arr.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            _arr.GetEnumerator();

        public void Dispose()
        {
            foreach (var item in _arr)
            {
                item.Value.Dispose();
            }
        }
    }
}
