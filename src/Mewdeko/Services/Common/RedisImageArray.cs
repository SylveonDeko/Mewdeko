using StackExchange.Redis;

namespace Mewdeko.Services.Common
{
    /// <summary>
    /// Represents a read-only list of byte arrays stored in Redis.
    /// </summary>
    public sealed class RedisImageArray : IReadOnlyList<byte[]>
    {
        private readonly ConnectionMultiplexer con;
        private readonly Lazy<byte[][]> data;
        private readonly string key;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisImageArray"/> class.
        /// </summary>
        /// <param name="key">The Redis key.</param>
        /// <param name="con">The Redis connection multiplexer.</param>
        public RedisImageArray(string key, ConnectionMultiplexer con)
        {
            this.con = con ?? throw new ArgumentNullException(nameof(con));
            this.key = key ?? throw new ArgumentNullException(nameof(key));
            data = new Lazy<byte[][]>(() => this.con.GetDatabase().ListRange(this.key).Select(x => (byte[])x).ToArray(),
                true);
        }

        /// <summary>
        /// Gets the byte array at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the byte array to get.</param>
        /// <returns>The byte array at the specified index.</returns>
        public byte[] this[int index]
        {
            get
            {
                if (index < 0)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return con.GetDatabase().ListGetByIndex(key, index);
            }
        }

        /// <summary>
        /// Gets the number of byte arrays contained in the Redis list.
        /// </summary>
        public int Count => data.IsValueCreated ? data.Value.Length : (int)con.GetDatabase().ListLength(key);

        /// <inheritdoc/>
        public IEnumerator<byte[]> GetEnumerator()
        {
            foreach (var t in data.Value)
                yield return t;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => data.Value.GetEnumerator();
    }
}