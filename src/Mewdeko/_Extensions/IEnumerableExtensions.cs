﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Mewdeko.Common.Collections;
using Mewdeko.Services.Database.Models;

namespace Mewdeko._Extensions
{
    public static class EnumerableExtensions
    {
        public static string JoinWith<T>(this IEnumerable<T> data, char separator, Func<T, string> func = null)
        {
            func ??= x => x?.ToString() ?? string.Empty;

            return string.Join(separator, data.Select(func));
        }
        public static bool ListCheck<T>(IEnumerable<T> l1, IEnumerable<T> l2)
        {
            if (l1.Intersect(l2).Any())
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public static string JoinWith<T>(this IEnumerable<T> data, string separator, Func<T, string> func = null)
        {
            func ??= x => x?.ToString() ?? string.Empty;

            return string.Join(separator, data.Select(func));
        }

        public static IEnumerable<T> Distinct<T, TU>(this IEnumerable<T> data, Func<T, TU> getKey)
        {
            return data.GroupBy(getKey)
                .Select(x => x.First());
        }

        /// <summary>
        ///     Randomize element order by performing the Fisher-Yates shuffle
        /// </summary>
        /// <typeparam name="T">Item type</typeparam>
        /// <param name="items">Items to shuffle</param>
        public static IReadOnlyList<T> Shuffle<T>(this IEnumerable<T> items)
        {
            using var provider = RandomNumberGenerator.Create();
            var list = items.ToList();
            var n = list.Count;
            while (n > 1)
            {
                var box = new byte[n / byte.MaxValue + 1];
                int boxSum;
                do
                {
                    provider.GetBytes(box);
                    boxSum = box.Sum(b => b);
                } while (!(boxSum < n * (byte.MaxValue * box.Length / n)));

                var k = boxSum % n;
                n--;
                (list[k], list[n]) = (list[n], list[k]);
            }

            return list;
        }

        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> elems, Action<T> exec)
        {
            var realElems = elems.ToList();
            foreach (var elem in realElems) exec(elem);

            return realElems;
        }

        public static ConcurrentDictionary<TKey, TValue> ToConcurrent<TKey, TValue>(
            this IEnumerable<KeyValuePair<TKey, TValue>> dict)
        {
            return new ConcurrentDictionary<TKey, TValue>(dict);
        }

        public static IndexedCollection<T> ToIndexed<T>(this IEnumerable<T> enumerable)
            where T : class, IIndexed
        {
            return new IndexedCollection<T>(enumerable);
        }

        // Licensed to the .NET Foundation under one or more agreements.
        // The .NET Foundation licenses this file to you under the MIT license.

        /// <summary>
        ///     Split the elements of a sequence into chunks of size at most <paramref name="size" />.
        /// </summary>
        /// <remarks>
        ///     Every chunk except the last will be of size <paramref name="size" />.
        ///     The last chunk will contain the remaining elements and may be of a smaller size.
        /// </remarks>
        /// <param name="source">
        ///     An <see cref="IEnumerable{T}" /> whose elements to chunk.
        /// </param>
        /// <param name="size">
        ///     Maximum size of each chunk.
        /// </param>
        /// <param name="constraints"></param>
        /// <typeparam name="TSource">
        ///     The type of the elements of source.
        /// </typeparam>
        /// <returns>
        ///     An <see cref="IEnumerable{T}" /> that contains the elements the input sequence split into chunks of size
        ///     <paramref name="size" />.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="source" /> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     <paramref name="size" /> is below 1.
        /// </exception>
        public static IEnumerable<IEnumerable<T>> ChunkBy<T>(IEnumerable<T> source, Predicate<IEnumerable<T>> constraints = null)
        {
            constraints ??= t => true;

            using var enumerator = source.GetEnumerator();
            bool hasElement = true;
            while (hasElement)
            {
                var group = new List<T>();
                while (constraints(group) && (hasElement = enumerator.MoveNext()))
                {
                    group.Add(enumerator.Current);
                    if (constraints(group)) continue;
                    group.Remove(enumerator.Current);
                    yield return group;
                    group = new List<T> { enumerator.Current };
                }
                yield return group;
            }
        }
    }
}