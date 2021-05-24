using System;
using System.Threading.Tasks;
using NLog;
using StackExchange.Redis;

namespace Mewdeko.Core.Common
{
    public class RedisPubSub : IPubSub
    {
        private readonly Logger _log;
        private readonly ConnectionMultiplexer _multi;
        private readonly ISeria _serializer;

        public RedisPubSub(ConnectionMultiplexer multi, ISeria serializer)
        {
            _multi = multi;
            _serializer = serializer;
            _log = LogManager.GetCurrentClassLogger();
        }

        public Task Pub<TData>(in TypedKey<TData> key, TData data)
        {
            var serialized = _serializer.Serialize(data);
            return _multi.GetSubscriber().PublishAsync(key.Key, serialized, CommandFlags.FireAndForget);
        }

        public Task Sub<TData>(in TypedKey<TData> key, Func<TData, Task> action)
        {
            var eventName = key.Key;
            return _multi.GetSubscriber().SubscribeAsync(eventName, async (ch, data) =>
            {
                try
                {
                    var dataObj = _serializer.Deserialize<TData>(data);
                    await action(dataObj);
                }
                catch (Exception ex)
                {
                    _log.Error($"Error handling the event {eventName}: {ex.Message}");
                }
            });
        }
    }
}