using System;
using System.Threading.Tasks;
using Discord;

namespace Mewdeko.Common
{
    public class RatelimitManager
    {
        public RequestOptions DefaultOptions { get; private set; }
        public bool IsRatelimited { get; private set; }
        
        private TaskCompletionSource ResetTask;
        public RatelimitManager(RequestOptions defaultOptions = null)
        {
            DefaultOptions = defaultOptions ?? RequestOptions.Default;
            DefaultOptions.RatelimitCallback = RatelimitCallback;
            ResetTask = new TaskCompletionSource();
        }
        private async Task RatelimitCallback(IRateLimitInfo info)
        {
            if(info.Lag.HasValue && info.Remaining.HasValue && info.ResetAfter.HasValue)
            {
                if(info.Remaining.Value == 0)
                {
                    IsRatelimited = true;
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(info.ResetAfter.Value.Add(info.Lag.Value).Add(TimeSpan.FromSeconds(1))); // add some padding for some odd cases
                        ResetTask.SetResult();
                    });
                }
            }
        }
        public async Task ExecuteWithRatelimits<TArg1>(Func<TArg1, RequestOptions, Task> func, TArg1 arg)
        {
            if (IsRatelimited)
            {
                await ResetTask.Task;
                ResetTask = new TaskCompletionSource();
                IsRatelimited = false;
            }
    
            await func(arg, DefaultOptions);
        }
    }
}