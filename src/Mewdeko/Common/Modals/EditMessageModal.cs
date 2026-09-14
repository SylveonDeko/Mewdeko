using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal used to collect the new content for a message the bot should edit.
/// </summary>
public class EditMessageModal : IModal
{
    /// <summary>
    ///     Gets or sets the new message text or embed json.
    /// </summary>
    [InputLabel("New Content")]
    [ModalTextInput("content", TextInputStyle.Paragraph, "Text or embed json")]
    public string Content { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title
    {
        get
        {
            return "Edit Message";
        }
    }
}