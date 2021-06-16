using System;
using System.Collections;
using System.Collections.Generic;

namespace Mewdeko.Common.Collections
{
    public static class DisposableReadOnlyListExtensions
    {
        public static IDisposableReadOnlyList<T> AsDisposable<T>(this IReadOnlyList<T> arr) where T : IDisposable
        {
            return new DisposableReadOnlyList<T>(arr);
        }

        public static IDisposableReadOnlyList<KeyValuePair<TKey, TValue>> AsDisposable<TKey, TValue>(
            this IReadOnlyList<KeyValuePair<TKey, TValue>> arr) where TValue : IDisposable
        {
            return new DisposableReadOnlyList<TKey, TValue>(arr);
        }
    }

    public interface IDisposableReadOnlyList<T> : IReadOnlyList<T>, IDisposable
    {
    }

    public sealed class DisposableReadOnlyList<T> : IDisposableReadOnlyList<T>
        where T : IDisposable
    {
        private readonly IReadOnlyList<T> _arr;

        public DisposableReadOnlyList(IReadOnlyList<T> arr)
        {
            _arr = arr;
        }

        public int Count => _arr.Count;

        public T this[int index] => _arr[index];

        public IEnumerator<T> GetEnumerator()
        {
            return _arr.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _arr.GetEnumerator();
        }

        public void Dispose()
        {
            foreach (var item in _arr) item.Dispose();
        }
    }

    public sealed class DisposableReadOnlyList<T, U> : IDisposableReadOnlyList<KeyValuePair<T, U>>
        where U : IDisposable
    {
        private readonly IReadOnlyList<KeyValuePair<T, U>> _arr;

        public DisposableReadOnlyList(IReadOnlyList<KeyValuePair<T, U>> arr)
        {
            _arr = arr;
        }

        public int Count => _arr.Count;

        KeyValuePair<T, U> IReadOnlyList<KeyValuePair<T, U>>.this[int index] => _arr[index];

        public IEnumerator<KeyValuePair<T, U>> GetEnumerator()
        {
            return _arr.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _arr.GetEnumerator();
        }

        public void Dispose()
        {
            foreach (var item in _arr) item.Value.Dispose();
        }
    }
}