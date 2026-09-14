using Discord.Interactions;

// ReSharper disable NotNullOrRequiredMemberIsNotInitialized

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal for setting the message users are dmed with when they are banned.
/// </summary>
public class BanMessageModal : IModal
{
    /// <summary>
    ///     Gets or sets the ban message text or embed json.
    /// </summary>
    [InputLabel("Message")]
    [ModalTextInput("message", TextInputStyle.Paragraph,
        "Text or embed json. Placeholders can be found at https://blog.mewdeko.tech/placeholders.")]
    public string Message { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Ban Message";
        }
    }
}