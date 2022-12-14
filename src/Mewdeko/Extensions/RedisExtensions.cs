using System.Collections.ObjectModel;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Mewdeko.Extensions;

public class RedisDictionary<TKey, TValue> : IDictionary<TKey, TValue>
{
    private readonly ConnectionMultiplexer cnn;
    private readonly string redisKey;

    public RedisDictionary(string redisKey, ConnectionMultiplexer cnn)
    {
        this.redisKey = redisKey;
        this.cnn = cnn;
    }

    private IDatabase GetRedisDb() => cnn.GetDatabase();

    private static string Serialize(object obj) => JsonConvert.SerializeObject(obj);

    private static T Deserialize<T>(string serialized) => JsonConvert.DeserializeObject<T>(serialized);

    public void Add(TKey key, TValue value) => GetRedisDb().HashSet(redisKey, Serialize(key), Serialize(value), flags: CommandFlags.FireAndForget);

    public bool ContainsKey(TKey key) => GetRedisDb().HashExists(redisKey, Serialize(key));

    public bool Remove(TKey key) => GetRedisDb().HashDelete(redisKey, Serialize(key), flags: CommandFlags.FireAndForget);

    public bool TryGetValue(TKey key, out TValue value)
    {
        var redisValue = GetRedisDb().HashGet(redisKey, Serialize(key));
        if (redisValue.IsNull)
        {
            value = default(TValue);
            return false;
        }

        value = Deserialize<TValue>(redisValue.ToString());
        return true;
    }

    public ICollection<TValue> Values => new Collection<TValue>(GetRedisDb().HashValues(redisKey).Select(h => Deserialize<TValue>(h.ToString())).ToList());

    public ICollection<TKey> Keys => new Collection<TKey>(GetRedisDb().HashKeys(redisKey).Select(h => Deserialize<TKey>(h.ToString())).ToList());

    public TValue this[TKey key]
    {
        get
        {
            var redisValue = GetRedisDb().HashGet(redisKey, Serialize(key));
            return redisValue.IsNull ? default(TValue) : Deserialize<TValue>(redisValue.ToString());
        }
        set => Add(key, value);
    }

    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    public void Clear() => GetRedisDb().KeyDelete(redisKey);

    public bool Contains(KeyValuePair<TKey, TValue> item) => GetRedisDb().HashExists(redisKey, Serialize(item.Key));

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => GetRedisDb().HashGetAll(redisKey).CopyTo(array, arrayIndex);

    public int Count => (int)GetRedisDb().HashLength(redisKey);

    public bool IsReadOnly => false;

    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        var db = GetRedisDb();
        foreach (var hashKey in db.HashKeys(redisKey))
        {
            var redisValue = db.HashGet(redisKey, hashKey);
            yield return new KeyValuePair<TKey, TValue>(Deserialize<TKey>(hashKey.ToString()), Deserialize<TValue>(redisValue.ToString()));
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        yield return GetEnumerator();
    }

    public void AddMultiple(IEnumerable<KeyValuePair<TKey, TValue>> items) =>
        GetRedisDb()
            .HashSet(redisKey, items.Select(i => new HashEntry(Serialize(i.Key), Serialize(i.Value))).ToArray());
}