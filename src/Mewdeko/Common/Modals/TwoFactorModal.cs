using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class TwoFactorModal : IModal
{
    public string Title => "2FA Code";

    [InputLabel("Code")]
    [ModalTextInput("code", TextInputStyle.Paragraph, "Please enter your 2fa code"), RequiredInput(false)]
    public string Code { get; set; }
}