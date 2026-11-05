using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal for typing the code from a verification captcha.
/// </summary>
public class VerificationCodeModal : IModal
{
    /// <summary>
    ///     Gets or sets the code the member typed.
    /// </summary>
    [InputLabel("Code")]
    [ModalTextInput("code", TextInputStyle.Short, "Code from the image", 1, 12)]
    public string Code { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title
    {
        get
        {
            return "Verification";
        }
    }
}
