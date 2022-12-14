using System.Threading.Tasks;

namespace Mewdeko.Common.PubSub;

public class EventPubSub : IPubSub
{
    private readonly Dictionary<string, Dictionary<Delegate, List<Func<object, ValueTask>>>> actions = new();
    private readonly object locker = new();

    public Task Sub<TData>(in TypedKey<TData> key, Func<TData, ValueTask> action)
        where TData : notnull
    {
        ValueTask LocalAction(object obj) => action((TData)obj);

        lock (locker)
        {
            if (!actions.TryGetValue(key.Key, out var keyActions))
            {
                keyActions = new Dictionary<Delegate, List<Func<object, ValueTask>>>();
                actions[key.Key] = keyActions;
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
        where TData : notnull
    {
        lock (locker)
        {
            if (this.actions.TryGetValue(key.Key, out var dictionary))
            {
                // if this class ever gets used, this needs to be properly implemented
                // 1. ignore all valuetasks which are completed
                // 2. run all other tasks in parallel
                return dictionary.SelectMany(kvp => kvp.Value).Select(action => action(data).AsTask()).WhenAll();
            }

            return Task.CompletedTask;
        }
    }

    public Task Unsub<TData>(in TypedKey<TData> key, Func<TData, ValueTask> action)
    {
        lock (locker)
        {
            // get subscriptions for this action
            if (this.actions.TryGetValue(key.Key, out var dictionary))
                // get subscriptions which have the same action hash code
                // note: having this as a list allows for multiple subscriptions of
                //       the same insance's/static method
            {
                if (dictionary.TryGetValue(action, out var sameActions))
                {
                    // remove last subscription
                    sameActions.RemoveAt(sameActions.Count - 1);

                    // if the last subscription was the only subscription
                    // we can safely remove this action's dictionary entry
                    if (sameActions.Count == 0)
                    {
                        dictionary.Remove(action);

                        // if our dictionary has no more elements after
                        // removing the entry
                        // it's safe to remove it from the key's subscriptions
                        if (dictionary.Count == 0)
                            this.actions.Remove(key.Key);
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}