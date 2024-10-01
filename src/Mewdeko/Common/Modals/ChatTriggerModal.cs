using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Represents a modal for adding a chat trigger.
/// </summary>
public class ChatTriggerModal : IModal
{
    /// <summary>
    ///     Gets or sets the key to trigger the response.
    /// </summary>
    /// <remarks>
    ///     This property is decorated with the InputLabel attribute with a value of "Key",
    ///     and the ModalTextInput attribute with a style of TextInputStyle.Short and a placeholder of "The key to trigger the
    ///     response".
    /// </remarks>
    [InputLabel("Key")]
    [ModalTextInput("key", TextInputStyle.Short, "The key to trigger the response")]
    public string? Trigger { get; set; }

    /// <summary>
    ///     Gets or sets the message to respond with when the key is triggered.
    /// </summary>
    /// <remarks>
    ///     This property is decorated with the InputLabel attribute with a value of "Message",
    ///     and the ModalTextInput attribute with a style of TextInputStyle.Paragraph and a placeholder of "The key to message
    ///     to respond with".
    /// </remarks>
    [InputLabel("Message")]
    [ModalTextInput("message", TextInputStyle.Paragraph, "The key to message to respond with")]
    public string? Message { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Add Chat Trigger";
        }
    }
}