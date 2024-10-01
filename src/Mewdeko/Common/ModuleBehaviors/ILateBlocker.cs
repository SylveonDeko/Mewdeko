using Discord.Commands;
using Discord.Interactions;

namespace Mewdeko.Common.ModuleBehaviors;

/// <summary>
///     Interface to be implemented by modules that block execution after a command has been executed.
/// </summary>
public interface ILateBlocker
{
    /// <summary>
    ///     Gets the priority of the module. The lower the number, the higher the priority.
    /// </summary>
    int Priority { get; }

    /// <summary>
    ///     Tries to block the execution of subsequent modules after a command has been executed.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="context">The command context.</param>
    /// <param name="moduleName">The name of the module.</param>
    /// <param name="command">The command that was executed.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a boolean indicating whether the
    ///     module blocked the execution.
    /// </returns>
    Task<bool> TryBlockLate(DiscordShardedClient client, ICommandContext context,
        string moduleName, CommandInfo command);

    /// <summary>
    ///     Tries to block the execution of subsequent modules after a command has been executed.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="context">The interaction context.</param>
    /// <param name="command">The command that was executed.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a boolean indicating whether the
    ///     module blocked the execution.
    /// </returns>
    Task<bool> TryBlockLate(DiscordShardedClient client, IInteractionContext context,
        ICommandInfo command);
}