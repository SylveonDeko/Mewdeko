using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal used to collect the content of a sticky message, which can span several lines.
/// </summary>
public class StickyModal : IModal
{
    /// <summary>
    ///     Gets or sets the message text or embed json.
    /// </summary>
    [InputLabel("Content")]
    [ModalTextInput("content", TextInputStyle.Paragraph, "Text or embed json")]
    public string Content { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title
    {
        get
        {
            return "Sticky Message";
        }
    }
}
