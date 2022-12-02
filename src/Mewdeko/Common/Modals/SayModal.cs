using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class SayModal : IModal
{
    public string Title => "Send a message/embed";

    [InputLabel("reminder")]
    [ModalTextInput("reminder", TextInputStyle.Paragraph, "Enter your reminder. Variables/placeholders can be found at https://blog.mewdeko.tech/placeholders.")]
    public string? Message { get; set; }
}