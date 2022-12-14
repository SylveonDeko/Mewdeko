using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class CommandModal : IModal
{
    public string Title => "Command Input";

    [InputLabel("Args")]
    [ModalTextInput("args", TextInputStyle.Paragraph, "Please enter command arguments"), RequiredInput(false)]
    public string Args { get; set; }
}