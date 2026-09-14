using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal for setting the dm message sent to giveaway winners.
/// </summary>
public class GiveawayDmMessageModal : IModal
{
    /// <summary>
    ///     Gets or sets the message text or embed json. Use "-" to remove the message.
    /// </summary>
    [InputLabel("Message")]
    [ModalTextInput("message", TextInputStyle.Paragraph, "Text or embed json. Use - to remove")]
    public string Message { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title
    {
        get
        {
            return "Giveaway Winner DM";
        }
    }
}