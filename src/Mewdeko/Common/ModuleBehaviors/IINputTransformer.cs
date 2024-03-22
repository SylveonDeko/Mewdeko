namespace Mewdeko.Common.ModuleBehaviors;

/// <summary>
/// Interface to be implemented by modules that transform user input.
/// </summary>
public interface IInputTransformer
{
    /// <summary>
    /// Transforms the user input.
    /// </summary>
    /// <param name="guild">The guild in which the message was sent.</param>
    /// <param name="channel">The channel in which the message was sent.</param>
    /// <param name="user">The user who sent the message.</param>
    /// <param name="input">The original user input.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the transformed input.</returns>
    Task<string> TransformInput(IGuild guild, IMessageChannel channel, IUser user, string input);
}