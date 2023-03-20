using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class SayModal : IModal
{
    public string Title => "Send a message/embed";

    [InputLabel("Message")]
    [ModalTextInput("SayMessage", TextInputStyle.Paragraph, "Enter your Message. Variables/placeholders can be found at https://blog.mewdeko.tech/placeholders.")]
    public string? Message { get; set; }
}