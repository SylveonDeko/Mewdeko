namespace Mewdeko.Controllers.Common.ChatTriggers;

/// <summary>
///     Request model for dry running a chat trigger against a sample message.
/// </summary>
public class TriggerTestRequest
{
    /// <summary>
    ///     The message text to match against.
    /// </summary>
    public string? Sample { get; set; }

    /// <summary>
    ///     The member to evaluate the trigger's requirements against, since the result depends on their roles,
    ///     level and balance.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The channel to test in, or zero for the guild's default channel.
    /// </summary>
    public ulong ChannelId { get; set; }
}