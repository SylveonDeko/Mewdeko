using System.Threading.Tasks;

namespace Mewdeko.Common.ModuleBehaviors;

/// <summary>
///     All services which need to execute something after
///     the bot is ready should implement this interface
/// </summary>
public interface IReadyExecutor
{
    /// <summary>
    ///     Executed when bot is ready
    /// </summary>
    public Task OnReadyAsync();
}