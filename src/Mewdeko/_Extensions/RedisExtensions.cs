using Newtonsoft.Json;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Mewdeko._Extensions;

public class RedisDictionary<TKey, TValue> : IDictionary<TKey, TValue>
{
    private static ConnectionMultiplexer _cnn;
    private string _redisKey;
    public RedisDictionary(string redisKey, ConnectionMultiplexer cnn)
    {
        _redisKey = redisKey;
        _cnn = cnn;
    }
    private IDatabase GetRedisDb() => _cnn.GetDatabase();

    private string Serialize(object obj) => JsonConvert.SerializeObject(obj);

    private T Deserialize<T>(string serialized) => JsonConvert.DeserializeObject<T>(serialized);

    public void Add(TKey key, TValue value) => GetRedisDb().HashSet(_redisKey, Serialize(key), Serialize(value));

    public bool ContainsKey(TKey key) => GetRedisDb().HashExists(_redisKey, Serialize(key));

    public bool Remove(TKey key) => GetRedisDb().HashDelete(_redisKey, Serialize(key));

    public bool TryGetValue(TKey key, out TValue value)
    {
        var redisValue = GetRedisDb().HashGet(_redisKey, Serialize(key));
        if (redisValue.IsNull)
        {
            value = default(TValue);
            return false;
        }
        value = Deserialize<TValue>(redisValue.ToString());
        return true;
    }
    public ICollection<TValue> Values => new Collection<TValue>(GetRedisDb().HashValues(_redisKey).Select(h => Deserialize<TValue>(h.ToString())).ToList());

    public ICollection<TKey> Keys => new Collection<TKey>(GetRedisDb().HashKeys(_redisKey).Select(h => Deserialize<TKey>(h.ToString())).ToList());

    public TValue this[TKey key]
    {
        get
        {
            var redisValue = GetRedisDb().HashGet(_redisKey, Serialize(key));
            return redisValue.IsNull ? default(TValue) : Deserialize<TValue>(redisValue.ToString());
        }
        set => Add(key, value);
    }
    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    public void Clear() => GetRedisDb().KeyDelete(_redisKey);

    public bool Contains(KeyValuePair<TKey, TValue> item) => GetRedisDb().HashExists(_redisKey, Serialize(item.Key));

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => GetRedisDb().HashGetAll(_redisKey).CopyTo(array, arrayIndex);

    public int Count => (int)GetRedisDb().HashLength(_redisKey);

    public bool IsReadOnly => false;

    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        var db = GetRedisDb();
        foreach (var hashKey in db.HashKeys(_redisKey))
        {
            var redisValue = db.HashGet(_redisKey, hashKey);
            yield return new KeyValuePair<TKey, TValue>(Deserialize<TKey>(hashKey.ToString()), Deserialize<TValue>(redisValue.ToString()));
        }
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        yield return GetEnumerator();
    }
    public void AddMultiple(IEnumerable<KeyValuePair<TKey, TValue>> items) =>
        GetRedisDb()
            .HashSet(_redisKey, items.Select(i => new HashEntry(Serialize(i.Key), Serialize(i.Value))).ToArray());
}