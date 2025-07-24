namespace Mewdeko.Controllers.Common.Chat;

/// <summary>
///     Data transfer object for chat log messages.
/// </summary>
public class ChatLogMessageDto
{
    /// <summary>
    ///     The Discord message ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     The message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    ///     The message author information.
    /// </summary>
    public ChatLogAuthorDto Author { get; set; } = new();

    /// <summary>
    ///     The message timestamp in ISO format.
    /// </summary>
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    ///     The message attachments.
    /// </summary>
    public List<ChatLogAttachmentDto> Attachments { get; set; } = new();

    /// <summary>
    ///     The message embeds.
    /// </summary>
    public List<ChatLogEmbedDto> Embeds { get; set; } = new();
}

/// <summary>
///     Data transfer object for chat log message authors.
/// </summary>
public class ChatLogAuthorDto
{
    /// <summary>
    ///     The Discord user ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     The username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    ///     The avatar URL.
    /// </summary>
    public string AvatarUrl { get; set; } = string.Empty;
}

/// <summary>
///     Data transfer object for chat log message attachments.
/// </summary>
public class ChatLogAttachmentDto
{
    /// <summary>
    ///     The attachment URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    ///     The attachment proxy URL.
    /// </summary>
    public string ProxyUrl { get; set; } = string.Empty;

    /// <summary>
    ///     The attachment filename.
    /// </summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    ///     The attachment file size in bytes.
    /// </summary>
    public int FileSize { get; set; }
}

/// <summary>
///     Data transfer object for chat log message embeds.
/// </summary>
public class ChatLogEmbedDto
{
    /// <summary>
    ///     The embed type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    ///     The embed title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    ///     The embed description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     The embed URL.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    ///     The embed thumbnail URL.
    /// </summary>
    public string? Thumbnail { get; set; }

    /// <summary>
    ///     The embed author information.
    /// </summary>
    public ChatLogEmbedAuthorDto? Author { get; set; }
}

/// <summary>
///     Data transfer object for chat log embed authors.
/// </summary>
public class ChatLogEmbedAuthorDto
{
    /// <summary>
    ///     The embed author name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     The embed author icon URL.
    /// </summary>
    public string? IconUrl { get; set; }
}