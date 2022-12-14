using StackExchange.Redis;

namespace Mewdeko.Services.Common;

public sealed class RedisImageArray : IReadOnlyList<byte[]>
{
    private readonly ConnectionMultiplexer con;

    private readonly Lazy<byte[][]> data;
    private readonly string key;

    public RedisImageArray(string key, ConnectionMultiplexer con)
    {
        this.con = con;
        this.key = key;
        data = new Lazy<byte[][]>(() => this.con.GetDatabase().ListRange(this.key).Select(x => (byte[])x).ToArray(),
            true);
    }

    public byte[] this[int index]
    {
        get
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            return con.GetDatabase().ListGetByIndex(key, index);
        }
    }

    public int Count => data.IsValueCreated
        ? data.Value.Length
        : (int)con.GetDatabase().ListLength(key);

    public IEnumerator<byte[]> GetEnumerator()
    {
        foreach (var t in data.Value)
            yield return t;
    }

    IEnumerator IEnumerable.GetEnumerator() => data.Value.GetEnumerator();
}