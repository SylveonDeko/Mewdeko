using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
/// Represents a modal for entering a recovery key.
/// </summary>
public class RecoveryKeyModal : IModal
{
    /// <summary>
    /// Gets the title of the modal.
    /// </summary>
    public string Title => "Recovery Key";

    /// <summary>
    /// Gets or sets the recovery key.
    /// </summary>
    /// <remarks>
    /// This property is decorated with the InputLabel attribute with a value of "Recovery Key",
    /// and the ModalTextInput attribute with a style of TextInputStyle.Paragraph and a placeholder of "Please enter your Recovery Key".
    /// It is not a required input.
    /// </remarks>
    [InputLabel("Recovery Key")]
    [ModalTextInput("recovery-key", TextInputStyle.Paragraph, "Please enter your Recovery Key"), RequiredInput(false)]
    public string? RecoveryKey { get; set; }
}