using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class RecoveryKeyModal : IModal
{
    public string Title => "Recovery Key";

    [InputLabel("Recovery Key")]
    [ModalTextInput("recovery-key", TextInputStyle.Paragraph, "Please enter your Recovery Key"), RequiredInput(false)]
    public string RecoveryKey { get; set; }
}