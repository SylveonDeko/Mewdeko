using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class ReminderModal : IModal
{
    public string Title => "New Reminder";

    [InputLabel("reminder")]
    [ModalTextInput("reminder", TextInputStyle.Paragraph, "Enter your reminder.")]
    public string? Reminder { get; set; }
}