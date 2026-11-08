using Discord.Interactions;

namespace Mewdeko.Modules.Afk.Common;

/// <summary>
///     The form for leaving a message for an AFK member.
/// </summary>
public class AfkMessageModal : IModal
{
    /// <summary>
    ///     The message.
    /// </summary>
    [ModalTextInput("content")]
    public string Content { get; set; } = "";

    /// <inheritdoc />
    public string Title => "Leave a message";
}
