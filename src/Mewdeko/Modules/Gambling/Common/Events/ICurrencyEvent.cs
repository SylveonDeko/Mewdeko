using System.Threading.Tasks;

namespace Mewdeko.Modules.Gambling.Common.Events;

public interface ICurrencyEvent
{
    event Func<ulong, Task> OnEnded;
    Task StopEvent();
    Task StartEvent();
}