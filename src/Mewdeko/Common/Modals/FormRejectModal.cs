using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Represents the modal a reviewer fills in when turning down a form response.
/// </summary>
/// <remarks>
///     The reason is required, because it is what gets sent to the submitter. A rejection with no
///     explanation leaves them with nothing to act on and usually turns into a support message
///     asking what went wrong.
/// </remarks>
public class FormRejectModal : IModal
{
    /// <summary>
    ///     Gets or sets the reason the response was turned down.
    /// </summary>
    [ModalTextInput("reason", TextInputStyle.Paragraph, "This is sent to the person who submitted it",
        maxLength: 1000)]
    [InputLabel("Why is this being rejected?")]
    public string Reason { get; set; } = null!;

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Reject response";
        }
    }
}