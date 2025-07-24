namespace Mewdeko.Controllers.Common.Chat;

/// <summary>
///     Request model for saving chat logs.
/// </summary>
public class SaveChatLogRequest
{
    /// <summary>
    ///     The Discord channel ID.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The name for the chat log.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     The user ID who created the log.
    /// </summary>
    public ulong CreatedBy { get; set; }

    /// <summary>
    ///     The messages to save in the log.
    /// </summary>
    public List<ChatLogMessageDto> Messages { get; set; } = new();
}