namespace Mewdeko.Services;

/// <summary>
///     Interface that all services must implement in order to be auto-discovered by the dependency injection system.
/// </summary>
public interface INService
{
}

/// <summary>
///     Interface implemented by services that require cleanup after they are unloaded.
/// </summary>
public interface IUnloadableService
{
    /// <summary>
    ///     Asynchronously unloads the service and performs any necessary cleanup.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task Unload();
}