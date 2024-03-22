using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
/// Represents a modal for evaluating code.
/// </summary>
public class EvalModal : IModal
{
    /// <summary>
    /// Gets the title of the modal.
    /// </summary>
    public string Title => "Eval Modal";

    /// <summary>
    /// Gets or sets the code to be evaluated.
    /// </summary>
    /// <remarks>
    /// This property is decorated with the InputLabel attribute with a value of "Code",
    /// and the ModalTextInput attribute with a style of TextInputStyle.Paragraph and a placeholder of "Put your code here!".
    /// </remarks>
    [ModalTextInput("code", TextInputStyle.Paragraph, "Put your code here!")]
    [InputLabel("Code")]
    public string? Code { get; set; }
}