using Discord;
using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class ChatTriggerModal : IModal
{
    public string Title => "Add Chat Trigger";

    [InputLabel("Key")]
    [ModalTextInput("key", TextInputStyle.Short, "The key to trigger the responce")]
    public string Key { get; set; }

    [InputLabel("Message")]
    [ModalTextInput("message", TextInputStyle.Paragraph, "The key to message to respond with", maxLength: 2000)]
    public string Message { get; set; }
}
