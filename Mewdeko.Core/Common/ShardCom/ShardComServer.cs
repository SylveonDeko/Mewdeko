using System;
using System.Threading.Tasks;
using Mewdeko.Core.Services;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Mewdeko.Common.ShardCom
{
    public class ShardComServer
    {
        private readonly IDataCache _cache;

        public ShardComServer(IDataCache cache)
        {
            _cache = cache;
        }

        public void Start()
        {
            var sub = _cache.Redis.GetSubscriber();
            sub.SubscribeAsync("shardcoord_send", (ch, data) =>
            {
                var _ = OnDataReceived(JsonConvert.DeserializeObject<ShardComMessage>(data));
            }, CommandFlags.FireAndForget);
        }

        public event Func<ShardComMessage, Task> OnDataReceived = delegate { return Task.CompletedTask; };
    }
}