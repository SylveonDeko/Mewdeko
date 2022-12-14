using System;
using System.Collections.Generic;
using System.Linq;
using Mewdeko.Database.Common;
using Mewdeko.Database.Models;
using NUnit.Framework;

namespace Mewdeko.Tests;

public class IndexedCollectionTests
{
    [Test]
    public void AddTest()
    {
        var collection = GetCollectionSample(Enumerable.Empty<ShopEntry>());

        // Add the items
        for (var counter = 0; counter < 10; counter++)
            collection.Add(new ShopEntry());

        // Evaluate the items are ordered
        CheckIndices(collection);
    }

    [Test]
    public void RemoveTest()
    {
        var collection = GetCollectionSample<ShopEntry>();

        collection.Remove(collection[1]);
        collection.Remove(collection[1]);

        // Evaluate the indices are ordered
        CheckIndices(collection);
        Assert.AreEqual(8, collection.Count);
    }

    [Test]
    public void RemoveAtTest()
    {
        var collection = GetCollectionSample<ShopEntry>();

        // Remove items 5 and 7
        collection.RemoveAt(5);
        collection.RemoveAt(6);

        // Evaluate if the items got removed
        foreach (var item in collection)
            Assert.IsFalse(item.Id == 5 || item.Id == 7, $"Item at index {item.Index} was not removed");

        CheckIndices(collection);

        // RemoveAt out of range
        Assert.Throws<ArgumentOutOfRangeException>(() => collection.RemoveAt(999), $"No exception thrown when removing from index 999 in a collection of size {collection.Count}.");
        Assert.Throws<ArgumentOutOfRangeException>(() => collection.RemoveAt(-3), "No exception thrown when removing from negative index -3.");
    }

    [Test]
    public void ClearTest()
    {
        var collection = GetCollectionSample<ShopEntry>();
        collection.Clear();

        Assert.IsTrue(collection.Count == 0, "Collection has not been cleared.");
        // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => collection.Contains(collection[0]), "Collection has not been cleared.");
    }

    [Test]
    public void CopyToTest()
    {
        var collection = GetCollectionSample<ShopEntry>();
        var fullCopy = new ShopEntry[10];

        collection.CopyTo(fullCopy, 0);

        // Evaluate copy
        for (var index = 0; index < fullCopy.Length; index++)
            Assert.AreEqual(index, fullCopy[index].Index);

        Assert.Throws<ArgumentException>(() => collection.CopyTo(new ShopEntry[10], 4));
        Assert.Throws<ArgumentException>(() => collection.CopyTo(new ShopEntry[6], 0));
    }

    [Test]
    public void IndexOfTest()
    {
        var collection = GetCollectionSample<ShopEntry>();

        Assert.AreEqual(4, collection.IndexOf(collection[4]));
        Assert.AreEqual(0, collection.IndexOf(collection[0]));
        Assert.AreEqual(7, collection.IndexOf(collection[7]));
        Assert.AreEqual(9, collection.IndexOf(collection[9]));
    }

    [Test]
    public void InsertTest()
    {
        var collection = GetCollectionSample<ShopEntry>();

        // Insert items at indices 5 and 7
        collection.Insert(5, new ShopEntry
        {
            Id = 555
        });
        collection.Insert(7, new ShopEntry
        {
            Id = 777
        });

        Assert.AreEqual(12, collection.Count);
        Assert.AreEqual(555, collection[5].Id);
        Assert.AreEqual(777, collection[7].Id);

        CheckIndices(collection);

        // Insert out of range
        Assert.Throws<ArgumentOutOfRangeException>(() => collection.Insert(999, new ShopEntry
        {
            Id = 999
        }), $"No exception thrown when inserting at index 999 in a collection of size {collection.Count}.");
        Assert.Throws<ArgumentOutOfRangeException>(() => collection.Insert(-3, new ShopEntry
        {
            Id = -3
        }), "No exception thrown when inserting at negative index -3.");
    }

    [Test]
    public void ContainsTest()
    {
        var subCol = new[]
        {
            new ShopEntry
            {
                Id = 111
            },
            new ShopEntry
            {
                Id = 222
            },
            new ShopEntry
            {
                Id = 333
            }
        };

        var collection = GetCollectionSample(
            Enumerable.Range(0, 10)
                .Select(x => new ShopEntry
                {
                    Id = x
                })
                .Concat(subCol)
        );

        collection.Remove(subCol[1]);
        CheckIndices(collection);

        Assert.IsTrue(collection.Contains(subCol[0]));
        Assert.IsFalse(collection.Contains(subCol[1]));
        Assert.IsTrue(collection.Contains(subCol[2]));
    }

    [Test]
    public void EnumeratorTest()
    {
        var collection = GetCollectionSample<ShopEntry>();
        using var enumerator = collection.GetEnumerator();

        foreach (var item in collection)
        {
            enumerator.MoveNext();
            Assert.AreEqual(item, enumerator.Current);
        }
    }

    [Test]
    public void IndexTest()
    {
        var collection = GetCollectionSample<ShopEntry>();

        collection[4] = new ShopEntry
        {
            Id = 444
        };
        collection[7] = new ShopEntry
        {
            Id = 777
        };
        CheckIndices(collection);

        Assert.AreEqual(444, collection[4].Id);
        Assert.AreEqual(777, collection[7].Id);
    }

    /// <summary>
    /// Checks whether all indices of the items are properly ordered.
    /// </summary>
    /// <typeparam name="T">An indexed, reference type.</typeparam>
    /// <param name="collection">The indexed collection to be checked.</param>
    private static void CheckIndices<T>(IndexedCollection<T> collection) where T : class, IIndexed
    {
        for (var index = 0; index < collection.Count; index++)
            Assert.AreEqual(index, collection[index].Index);
    }

    /// <summary>
    /// Gets an <see cref="IndexedCollection{T}"/> from the specified <paramref name="sample"/> or a collection with 10 shop entries if none is provided.
    /// </summary>
    /// <typeparam name="T">An indexed, database entity type.</typeparam>
    /// <param name="sample">A sample collection to be added as an indexed collection.</param>
    /// <returns>An indexed collection of <typeparamref name="T"/>.</returns>
    private static IndexedCollection<T> GetCollectionSample<T>(IEnumerable<T> sample = default) where T : DbEntity, IIndexed, new()
        => new(sample ?? Enumerable.Range(0, 10).Select(x => new T
        {
            Id = x
        }));
}