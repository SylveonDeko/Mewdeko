namespace Mewdeko.Controllers.Common.GreetMessages;

/// <summary>
///     A guild's goodbye, DM welcome or boost message, as read and as written.
/// </summary>
public class GreetMessageDto
{
    /// <summary>
    ///     Whether the message is sent.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The message text or embed JSON.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    ///     The channel the message is sent to. Always null for DM welcomes.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     Seconds until the message is deleted, 0 for never. Always 0 for DM welcomes.
    /// </summary>
    public int DeleteAfter { get; set; }
}
