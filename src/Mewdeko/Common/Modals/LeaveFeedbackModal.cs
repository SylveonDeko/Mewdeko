using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Represents the modal a server owner fills in to explain why the bot was removed.
/// </summary>
public class LeaveFeedbackModal : IModal
{
    /// <summary>
    ///     Gets or sets the free text explanation of why the bot was removed.
    /// </summary>
    [ModalTextInput("comment", TextInputStyle.Paragraph, "What made you remove the bot?", maxLength: 1000)]
    [InputLabel("What could we have done better?")]
    public string? Comment { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Feedback";
        }
    }
}