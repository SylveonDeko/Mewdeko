namespace Mewdeko.Common.ModuleBehaviors;

/// <summary>
///     Interface to be implemented by modules which block execution before anything is executed.
/// </summary>
public interface IEarlyBehavior
{
    /// <summary>
    ///     Gets the priority of the module. The lower the number, the higher the priority.
    /// </summary>
    int Priority { get; }

    /// <summary>
    ///     Gets the type of behavior the module exhibits.
    /// </summary>
    ModuleBehaviorType BehaviorType { get; }

    /// <summary>
    ///     Executes the behavior of the module.
    /// </summary>
    /// <param name="socketClient">The Discord client.</param>
    /// <param name="guild">The guild in which the message was sent.</param>
    /// <param name="msg">The message that triggered the module.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the module blocked the execution.</returns>
    Task<bool> RunBehavior(DiscordShardedClient socketClient, IGuild guild, IUserMessage msg);
}

/// <summary>
///     Enum representing the type of behavior a module can exhibit.
/// </summary>
public enum ModuleBehaviorType
{
    /// <summary>
    ///     The module blocks the execution of subsequent modules.
    /// </summary>
    Blocker,

    /// <summary>
    ///     The module executes an action but does not block the execution of subsequent modules.
    /// </summary>
    Executor
}