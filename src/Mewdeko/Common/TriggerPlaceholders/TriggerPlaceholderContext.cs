namespace Mewdeko.Common.TriggerPlaceholders;

/// <summary>
///     The context a contextual placeholder is resolved against.
/// </summary>
/// <param name="Guild">The guild the placeholder is being resolved in, if any.</param>
/// <param name="Channel">The channel the placeholder is being resolved in.</param>
/// <param name="User">The user that caused the resolution, such as the author of a triggering message.</param>
/// <param name="Target">The user targeted by the invocation, such as the first mentioned user, if any.</param>
public sealed record TriggerPlaceholderContext(
    IGuild? Guild,
    IMessageChannel? Channel,
    IUser User,
    IUser? Target = null);