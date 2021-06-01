using System;
using System.Threading.Tasks;
using Mewdeko.Core.Services;
using Mewdeko.Extensions;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Core.Common
{
    public sealed class RedisPubSub : IPubSub
    {
        private readonly ConnectionMultiplexer _multi;
        private readonly ISeria _serializer;
        private readonly IBotCredentials _creds;

        public RedisPubSub(ConnectionMultiplexer multi, ISeria serializer, IBotCredentials creds)
        {
            _multi = multi;
            _serializer = serializer;
            _creds = creds;
        }

        public Task Pub<TData>(in TypedKey<TData> key, TData data)
        {
            var serialized = _serializer.Serialize(data);
            return _multi.GetSubscriber().PublishAsync($"{_creds.RedisKey()}:{key.Key}", serialized, CommandFlags.FireAndForget);
        }

        public Task Sub<TData>(in TypedKey<TData> key, Func<TData, ValueTask> action)
        {
            var eventName = key.Key;
            return _multi.GetSubscriber().SubscribeAsync($"{_creds.RedisKey()}:{eventName}", async (ch, data) =>
            {
                try
                {
                    var dataObj = _serializer.Deserialize<TData>(data);
                    await action(dataObj);
                }
                catch (Exception ex)
                {
                    Log.Error($"Error handling the event {eventName}: {ex.Message}");
                }
            });
        }
    }
}