using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class EvalModal : IModal
{
    public string Title => "Eval Modal";

    [ModalTextInput("code", TextInputStyle.Paragraph, "Put your code here!")]
    [InputLabel("Code")]
    public string Code { get; set; }
}