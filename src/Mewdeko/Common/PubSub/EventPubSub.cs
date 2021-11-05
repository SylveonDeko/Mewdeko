using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mewdeko.Common.PubSub
{
    public class EventPubSub : IPubSub
    {
        private readonly Dictionary<string, Dictionary<Delegate, List<Func<object, ValueTask>>>> _actions
            = new();

        private readonly object _locker = new();

        public Task Sub<TData>(in TypedKey<TData> key, Func<TData, ValueTask> action)
        {
            ValueTask LocalAction(object obj) => action((TData)obj);
            lock (_locker)
            {
                if (!_actions.TryGetValue(key.Key, out var keyActions))
                {
                    keyActions = new Dictionary<Delegate, List<Func<object, ValueTask>>>();
                    _actions[key.Key] = keyActions;
                }

                if (!keyActions.TryGetValue(action, out var sameActions))
                {
                    sameActions = new List<Func<object, ValueTask>>();
                    keyActions[action] = sameActions;
                }

                sameActions.Add(LocalAction);

                return Task.CompletedTask;
            }
        }

        public Task Pub<TData>(in TypedKey<TData> key, TData data)
        {
            lock (_locker)
            {
                if (_actions.TryGetValue(key.Key, out var actions))
                    // if this class ever gets used, this needs to be properly implemented
                    // 1. ignore all valuetasks which are completed
                    // 2. return task.whenall all other tasks
                    return Task.WhenAll(actions
                        .SelectMany(kvp => kvp.Value)
                        .Select(action => action(data).AsTask()));

                return Task.CompletedTask;
            }
        }
    }
}