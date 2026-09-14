using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal for setting the message shown above the suggest button.
/// </summary>
public class SuggestButtonMessageModal : IModal
{
    /// <summary>
    ///     Gets or sets the message text or embed json. Use "-" to reset to the default.
    /// </summary>
    [InputLabel("Message")]
    [ModalTextInput("message", TextInputStyle.Paragraph, "Text or embed json. Use - to reset to default")]
    public string Message { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title
    {
        get
        {
            return "Suggest Button Message";
        }
    }
}