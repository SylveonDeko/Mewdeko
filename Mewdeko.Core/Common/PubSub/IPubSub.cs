using System;
using System.Threading.Tasks;

namespace Mewdeko.Core.Common
{
    public interface IPubSub
    {
        public Task Pub<TData>(in TypedKey<TData> key, TData data);
        public Task Sub<TData>(in TypedKey<TData> key, Func<TData, Task> action);
    }
}