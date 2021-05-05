using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NadekoBot.Core.Services;

namespace NadekoBot.Common.ShardCom
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
            }, StackExchange.Redis.CommandFlags.FireAndForget);
        }

        public event Func<ShardComMessage, Task> OnDataReceived = delegate { return Task.CompletedTask; };
    }
}
