using System;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Gambling.Common
{
    public interface ICurrencyEvent
    {
        event Func<ulong, Task> OnEnded;
        Task StopEvent();
        Task StartEvent();
    }
}