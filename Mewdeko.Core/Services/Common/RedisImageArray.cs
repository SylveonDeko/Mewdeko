using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StackExchange.Redis;

namespace Mewdeko.Core.Services.Common
{
    public sealed class RedisImageArray : IReadOnlyList<byte[]>
    {
        private readonly ConnectionMultiplexer _con;

        private readonly Lazy<byte[][]> _data;
        private readonly string _key;

        public RedisImageArray(string key, ConnectionMultiplexer con)
        {
            _con = con;
            _key = key;
            _data = new Lazy<byte[][]>(() => _con.GetDatabase().ListRange(_key).Select(x => (byte[])x).ToArray(),
                true);
        }

        public byte[] this[int index]
        {
            get
            {
                if (index < 0)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return _con.GetDatabase().ListGetByIndex(_key, index);
            }
        }

        public int Count => _data.IsValueCreated
            ? _data.Value.Length
            : (int)_con.GetDatabase().ListLength(_key);

        public IEnumerator<byte[]> GetEnumerator()
        {
            var actualData = _data.Value;
            for (var i = 0; i < actualData.Length; i++) yield return actualData[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _data.Value.GetEnumerator();
        }
    }
}