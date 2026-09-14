using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal used to set the boost, bye, or DM greet message text for a guild.
/// </summary>
public class GreetMessageModal : IModal
{
    /// <summary>
    ///     Gets or sets the message text or embed json.
    /// </summary>
    [InputLabel("Message")]
    [ModalTextInput("message", TextInputStyle.Paragraph, "Text or embed json")]
    public string Message { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title
    {
        get
        {
            return "Greet Message";
        }
    }
}