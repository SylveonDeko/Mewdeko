using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Represents a modal for entering a two-factor authentication (2FA) code.
/// </summary>
public class TwoFactorModal : IModal
{
    /// <summary>
    ///     Gets or sets the 2FA code.
    /// </summary>
    /// <remarks>
    ///     This property is decorated with the InputLabel attribute with a value of "Code",
    ///     and the ModalTextInput attribute with a style of TextInputStyle.Paragraph and a placeholder of "Please enter your
    ///     2fa code".
    ///     It is not a required input.
    /// </remarks>
    [InputLabel("Code")]
    [ModalTextInput("code", TextInputStyle.Paragraph, "Please enter your 2fa code")]
    [RequiredInput(false)]
    public string? Code { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "2FA Code";
        }
    }
}