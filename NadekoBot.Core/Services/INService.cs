using System.Threading.Tasks;

namespace NadekoBot.Core.Services
{
    /// <summary>
    /// All services must implement this interface in order to be auto-discovered by the DI system
    /// </summary>
    public interface INService
    {
        
    }

    /// <summary>
    /// All services which require cleanup after they are unloaded must implement this interface
    /// </summary>
    public interface IUnloadableService
    {
        Task Unload();
    }
}
