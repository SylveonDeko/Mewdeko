using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
/// Represents the second step of the manual authentication process.
/// </summary>
public class AuthHandshakeStepTwoModal : IModal
{
    /// <summary>
    /// Gets the title of the modal.
    /// </summary>
    public string Title => "Manual Authentication Step 2";

    /// <summary>
    /// Gets or sets the authorization code obtained from the website.
    /// </summary>
    /// <remarks>
    /// This property is decorated with the InputLabel attribute with a value of "Code",
    /// and the ModalTextInput attribute with a style of TextInputStyle.Paragraph and a placeholder of "Enter the authorization code you got from our website.".
    /// </remarks>
    [InputLabel("Code")]
    [ModalTextInput("code", TextInputStyle.Paragraph, "Enter the authorization code you got from our website.")]
    public string? Code { get; set; }
}