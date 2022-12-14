using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class SuggestStateModal : IModal
{
    public string Title => "State Change Reason";

    [InputLabel("Reason")]
    [ModalTextInput("suggestion", TextInputStyle.Paragraph, "Please enter reason"), RequiredInput(false)]
    public string Reason { get; set; }
}