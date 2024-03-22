using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
/// Represents a modal for sending a direct message to a user.
/// </summary>
public class DmUserModal : IModal
{
    /// <summary>
    /// Gets the title of the modal.
    /// </summary>
    public string Title => "a dev forgot to edit the title of this modal and now looks silly";

    /// <summary>
    /// Gets or sets the message to be sent to the user.
    /// </summary>
    /// <remarks>
    /// This property is decorated with the InputLabel attribute with a value of "Message",
    /// the ModalTextInput attribute with a style of TextInputStyle.Paragraph and a placeholder of "send the user a message",
    /// and the RequiredInput attribute, indicating that it is a required input.
    /// </remarks>
    [ModalTextInput("message", TextInputStyle.Paragraph, "send the user a message")]
    [InputLabel("Message")]
    [RequiredInput]
    public string? Message { get; set; }
}