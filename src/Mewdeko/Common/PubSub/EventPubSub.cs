using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mewdeko.Core.Common
{
    public class EventPubSub : IPubSub
    {
        private readonly Dictionary<string, Dictionary<Delegate, List<Func<object, ValueTask>>>> _actions
            = new();

        private readonly object locker = new();

        public Task Sub<TData>(in TypedKey<TData> key, Func<TData, ValueTask> action)
        {
            Func<object, ValueTask> localAction = obj => action((TData)obj);
            lock (locker)
            {
                Dictionary<Delegate, List<Func<object, ValueTask>>> keyActions;
                if (!_actions.TryGetValue(key.Key, out keyActions))
                {
                    keyActions = new Dictionary<Delegate, List<Func<object, ValueTask>>>();
                    _actions[key.Key] = keyActions;
                }

                List<Func<object, ValueTask>> sameActions;
                if (!keyActions.TryGetValue(action, out sameActions))
                {
                    sameActions = new List<Func<object, ValueTask>>();
                    keyActions[action] = sameActions;
                }

                sameActions.Add(localAction);

                return Task.CompletedTask;
            }
        }

        public Task Pub<TData>(in TypedKey<TData> key, TData data)
        {
            lock (locker)
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

        public Task Unsub<TData>(in TypedKey<TData> key, Func<TData, ValueTask> action)
        {
            lock (locker)
            {
                // get subscriptions for this action
                if (_actions.TryGetValue(key.Key, out var actions))
                {
                    var hashCode = action.GetHashCode();
                    // get subscriptions which have the same action hash code
                    // note: having this as a list allows for multiple subscriptions of
                    //       the same insance's/static method
                    if (actions.TryGetValue(action, out var sameActions))
                    {
                        // remove last subscription
                        sameActions.RemoveAt(sameActions.Count - 1);

                        // if the last subscription was the only subscription
                        // we can safely remove this action's dictionary entry
                        if (sameActions.Count == 0)
                        {
                            actions.Remove(action);

                            // if our dictionary has no more elements after 
                            // removing the entry
                            // it's safe to remove it from the key's subscriptions
                            if (actions.Count == 0) _actions.Remove(key.Key);
                        }
                    }
                }

                return Task.CompletedTask;
            }
        }
    }
}