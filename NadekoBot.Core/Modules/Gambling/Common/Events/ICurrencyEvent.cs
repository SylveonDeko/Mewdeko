using System;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Gambling.Common
{
    public interface ICurrencyEvent
    {
        event Func<ulong, Task> OnEnded;
        Task StopEvent();
        Task StartEvent();
    }
}
