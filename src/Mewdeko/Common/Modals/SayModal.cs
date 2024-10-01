using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Represents a modal for sending a message or an embed.
/// </summary>
public class SayModal : IModal
{
    /// <summary>
    ///     Gets or sets the message to be sent.
    /// </summary>
    /// <remarks>
    ///     This property is decorated with the InputLabel attribute with a value of "Message",
    ///     and the ModalTextInput attribute with a style of TextInputStyle.Paragraph and a placeholder of "Enter your Message.
    ///     Variables/placeholders can be found at https://blog.mewdeko.tech/placeholders.".
    /// </remarks>
    [InputLabel("Message")]
    [ModalTextInput("SayMessage", TextInputStyle.Paragraph,
        "Enter your Message. Variables/placeholders can be found at https://blog.mewdeko.tech/placeholders.")]
    public string? Message { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Send a message/embed";
        }
    }
}