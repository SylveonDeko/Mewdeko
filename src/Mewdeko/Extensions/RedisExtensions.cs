using System.Collections.ObjectModel;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Mewdeko.Extensions;

public class RedisDictionary<TKey, TValue> : IDictionary<TKey, TValue>
{
    private readonly ConnectionMultiplexer cnn;
    private readonly string redisKey;
    private readonly IDatabase dbCache;

    public RedisDictionary(string redisKey, ConnectionMultiplexer cnn)
    {
        this.redisKey = redisKey;
        this.cnn = cnn;
        this.dbCache = GetRedisDb();
    }

    private IDatabase GetRedisDb() => cnn.GetDatabase();

    private static string Serialize(object obj) => JsonConvert.SerializeObject(obj);

    private static T Deserialize<T>(string serialized) => JsonConvert.DeserializeObject<T>(serialized);

    public void Add(TKey key, TValue value) => dbCache.HashSet(redisKey, Serialize(key), Serialize(value), flags: CommandFlags.FireAndForget);

    public bool ContainsKey(TKey key) => dbCache.HashExists(redisKey, Serialize(key));

    public bool Remove(TKey key) => dbCache.HashDelete(redisKey, Serialize(key), flags: CommandFlags.FireAndForget);

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

    public ICollection<TValue> Values => new Collection<TValue>(dbCache.HashValues(redisKey).Select(h => Deserialize<TValue>(h)).ToList());

    public ICollection<TKey> Keys => new Collection<TKey>(dbCache.HashKeys(redisKey).Select(h => Deserialize<TKey>(h)).ToList());

    public TValue this[TKey key]
    {
        get
        {
            var redisValue = dbCache.HashGet(redisKey, Serialize(key));
            return redisValue.IsNull ? default(TValue) : Deserialize<TValue>(redisValue);
        }
        set => Add(key, value);
    }

    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    public void Clear() => dbCache.KeyDelete(redisKey);

    public bool Contains(KeyValuePair<TKey, TValue> item) => dbCache.HashExists(redisKey, Serialize(item.Key));

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => dbCache.HashGetAll(redisKey).CopyTo(array, arrayIndex);

    public int Count => (int)dbCache.HashLength(redisKey);

    public bool IsReadOnly => false;

    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return (from hashKey in dbCache.HashKeys(redisKey)
            let redisValue = dbCache.HashGet(redisKey, hashKey)
            select new KeyValuePair<TKey, TValue>(Deserialize<TKey>(hashKey), Deserialize<TValue>(redisValue))).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void AddMultiple(IEnumerable<KeyValuePair<TKey, TValue>> items) =>
        dbCache.HashSet(redisKey, items.Select(i => new HashEntry(Serialize(i.Key), Serialize(i.Value))).ToArray());
}