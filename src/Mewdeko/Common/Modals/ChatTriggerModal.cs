using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class ChatTriggerModal : IModal
{
    public string Title => "Add Chat Trigger";

    [InputLabel("Key")]
    [ModalTextInput("key", TextInputStyle.Short, "The key to trigger the responce")]
    public string Trigger { get; set; }

    [InputLabel("Message")]
    [ModalTextInput("message", TextInputStyle.Paragraph, "The key to message to respond with")]
    public string? Message { get; set; }
}