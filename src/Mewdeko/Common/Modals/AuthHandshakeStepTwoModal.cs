using Discord.Interactions;

namespace Mewdeko.Common.Modals;

public class AuthHandshakeStepTwoModal : IModal
{
    public string Title => "Manual Authentication Step 2";

    [InputLabel("Code")]
    [ModalTextInput("code", TextInputStyle.Paragraph, "Enter the authorization code you got from our website.")]
    public string? Code { get; set; }
}