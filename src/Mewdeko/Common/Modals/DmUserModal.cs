using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class DmUserModal : IModal
{
    public string Title => "a dev forgot to edit the title of this modal and now looks silly";

    [ModalTextInput("message", TextInputStyle.Paragraph, "send the user a message")]
    [InputLabel("Message")]
    [RequiredInput]
    public string? Message { get; set; }
}