using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Represents a modal for setting a reminder.
/// </summary>
public class ReminderModal : IModal
{
    /// <summary>
    ///     Gets or sets the reminder text.
    /// </summary>
    /// <remarks>
    ///     This property is decorated with the InputLabel attribute with a value of "Reminder",
    ///     and the ModalTextInput attribute with a style of TextInputStyle.Paragraph and a placeholder of "Enter your
    ///     reminder.".
    /// </remarks>
    [InputLabel("Reminder")]
    [ModalTextInput("reminder", TextInputStyle.Paragraph, "Enter your reminder.")]
    public string? Reminder { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "New Reminder";
        }
    }
}