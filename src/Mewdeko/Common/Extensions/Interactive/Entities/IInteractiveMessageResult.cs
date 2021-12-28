using Discord;

namespace Mewdeko.Common.Extensions.Interactive.Entities;

/// <summary>
///     Represents a result from an interactive action containing a message associated with the action.
/// </summary>
public interface IInteractiveMessageResult : IInteractiveResult
{
    /// <summary>
    ///     Gets the message this interactive result comes from.
    /// </summary>
    public IUserMessage Message { get; }
}