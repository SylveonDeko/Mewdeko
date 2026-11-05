namespace Mewdeko.Controllers.Common.UtilityExtras;

/// <summary>
///     A sticky message, as read and as written.
/// </summary>
public class StickyDto
{
    /// <summary>
    ///     The channel the sticky is in.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The message text or embed json.
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    ///     How many new messages bring the sticky back to the bottom.
    /// </summary>
    public int Messages { get; set; } = 5;

    /// <summary>
    ///     How many seconds after the first new message the sticky comes back to the bottom.
    /// </summary>
    public int Seconds { get; set; } = 15;
}

/// <summary>
///     A sticky template, as read and as written.
/// </summary>
public class StickyTemplateDto
{
    /// <summary>
    ///     The template name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The message text or embed json.
    /// </summary>
    public string Content { get; set; } = "";
}

/// <summary>
///     Where a sticky template is stuck.
/// </summary>
public class UseStickyTemplateRequest
{
    /// <summary>
    ///     The channel to stick the template to.
    /// </summary>
    public ulong ChannelId { get; set; }
}

/// <summary>
///     A rule that sends a command's or module's replies to a set channel.
/// </summary>
public class CommandReplyRedirectDto
{
    /// <summary>
    ///     A command, alias or module name.
    /// </summary>
    public string Target { get; set; } = "";

    /// <summary>
    ///     Whether the target is a module rather than a command.
    /// </summary>
    public bool IsModule { get; set; }

    /// <summary>
    ///     The channel replies go to.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Whether the message that used the command is deleted.
    /// </summary>
    public bool DeleteInvoker { get; set; }
}

/// <summary>
///     A setting that points at a deleted channel or role.
/// </summary>
public class CleanupFindingDto
{
    /// <summary>
    ///     The feature the setting belongs to.
    /// </summary>
    public string Feature { get; set; } = "";

    /// <summary>
    ///     The setting.
    /// </summary>
    public string Setting { get; set; } = "";

    /// <summary>
    ///     Channel or Role.
    /// </summary>
    public string Kind { get; set; } = "";

    /// <summary>
    ///     The ID that no longer exists.
    /// </summary>
    public string MissingId { get; set; } = "";

    /// <summary>
    ///     How many rows point at it.
    /// </summary>
    public int Rows { get; set; }

    /// <summary>
    ///     Clear, DeleteRow, ClearList or Report.
    /// </summary>
    public string Action { get; set; } = "";
}

/// <summary>
///     The result of applying a cleanup.
/// </summary>
public class CleanupResultDto
{
    /// <summary>
    ///     How many settings were fixed.
    /// </summary>
    public int Fixed { get; set; }
}

/// <summary>
///     A message the bot posted, as the dashboard edits it.
/// </summary>
public class BotMessageDto
{
    /// <summary>
    ///     The channel the message is in.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The message ID.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    ///     The link to the message.
    /// </summary>
    public string Link { get; set; } = "";

    /// <summary>
    ///     The plain text when the message has no embeds, otherwise embed json.
    /// </summary>
    public string Content { get; set; } = "";
}

/// <summary>
///     New content for a message the bot posted.
/// </summary>
public class EditBotMessageRequest
{
    /// <summary>
    ///     The link to the bot's message.
    /// </summary>
    public string Link { get; set; } = "";

    /// <summary>
    ///     The new text or embed json.
    /// </summary>
    public string Content { get; set; } = "";
}
