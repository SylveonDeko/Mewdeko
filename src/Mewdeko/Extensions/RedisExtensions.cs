using System.Collections.ObjectModel;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Mewdeko.Extensions;

/// <summary>
///     Represents a dictionary stored in Redis.
/// </summary>
public class RedisDictionary<TKey, TValue> : IDictionary<TKey, TValue>
{
    private readonly ConnectionMultiplexer cnn;
    private readonly IDatabase dbCache;
    private readonly string redisKey;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RedisDictionary{TKey, TValue}" /> class.
    /// </summary>
    /// <param name="redisKey">The Redis key under which the dictionary is stored.</param>
    /// <param name="cnn">The Redis connection multiplexer.</param>
    public RedisDictionary(string redisKey, ConnectionMultiplexer cnn)
    {
        this.redisKey = redisKey;
        this.cnn = cnn;
        dbCache = GetRedisDb();
    }

    /// <inheritdoc />
    public void Add(TKey key, TValue value)
    {
        dbCache.HashSet(redisKey, Serialize(key), Serialize(value), flags: CommandFlags.FireAndForget);
    }

    /// <inheritdoc />
    public bool ContainsKey(TKey key)
    {
        return dbCache.HashExists(redisKey, Serialize(key));
    }

    /// <inheritdoc />
    public bool Remove(TKey key)
    {
        return dbCache.HashDelete(redisKey, Serialize(key), CommandFlags.FireAndForget);
    }

    /// <inheritdoc />
    public bool TryGetValue(TKey key, out TValue value)
    {
        var redisValue = dbCache.HashGet(redisKey, Serialize(key));
        if (redisValue.IsNull)
        {
            value = default;
            return false;
        }

        value = Deserialize<TValue>(redisValue);
        return true;
    }

    /// <inheritdoc />
    public ICollection<TValue> Values
    {
        get
        {
            return new Collection<TValue>(dbCache.HashValues(redisKey).Select(h => Deserialize<TValue>(h)).ToList());
        }
    }

    /// <inheritdoc />
    public ICollection<TKey> Keys
    {
        get
        {
            return new Collection<TKey>(dbCache.HashKeys(redisKey).Select(h => Deserialize<TKey>(h)).ToList());
        }
    }

    /// <inheritdoc />
    public TValue this[TKey key]
    {
        get
        {
            var redisValue = dbCache.HashGet(redisKey, Serialize(key));
            return redisValue.IsNull ? default : Deserialize<TValue>(redisValue);
        }
        set
        {
            Add(key, value);
        }
    }

    /// <inheritdoc />
    public void Add(KeyValuePair<TKey, TValue> item)
    {
        Add(item.Key, item.Value);
    }

    /// <inheritdoc />
    public void Clear()
    {
        dbCache.KeyDelete(redisKey);
    }

    /// <inheritdoc />
    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        return dbCache.HashExists(redisKey, Serialize(item.Key));
    }

    /// <inheritdoc />
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        dbCache.HashGetAll(redisKey).CopyTo(array, arrayIndex);
    }

    /// <inheritdoc />
    public int Count
    {
        get
        {
            return (int)dbCache.HashLength(redisKey);
        }
    }

    /// <inheritdoc />
    public bool IsReadOnly
    {
        get
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        return Remove(item.Key);
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return (from hashKey in dbCache.HashKeys(redisKey)
                let redisValue = dbCache.HashGet(redisKey, hashKey)
                select new KeyValuePair<TKey, TValue>(Deserialize<TKey>(hashKey), Deserialize<TValue>(redisValue)))
            .GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private IDatabase GetRedisDb()
    {
        return cnn.GetDatabase();
    }

    private static string Serialize(object obj)
    {
        return JsonConvert.SerializeObject(obj);
    }

    private static T Deserialize<T>(string serialized)
    {
        return JsonConvert.DeserializeObject<T>(serialized);
    }

    /// <summary>
    ///     Adds multiple key-value pairs to the dictionary.
    /// </summary>
    /// <param name="items">The key-value pairs to add.</param>
    public void AddMultiple(IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        dbCache.HashSet(redisKey, items.Select(i => new HashEntry(Serialize(i.Key), Serialize(i.Value))).ToArray());
    }
}