namespace Mewdeko.Common.ModuleBehaviors;

/// <summary>
/// Interface to be implemented by services that need to execute something after the bot is ready.
/// </summary>
public interface IReadyExecutor
{
    /// <summary>
    /// Method to be executed when the bot is ready.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task OnReadyAsync();
}