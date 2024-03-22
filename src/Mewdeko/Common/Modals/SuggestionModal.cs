using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
/// Represents a modal for submitting a suggestion.
/// </summary>
public class SuggestionModal : IModal
{
    /// <summary>
    /// Gets the title of the modal.
    /// </summary>
    public string Title => "Suggestion";

    /// <summary>
    /// Gets or sets the suggestion text.
    /// </summary>
    /// <remarks>
    /// This property is decorated with the InputLabel attribute with a value of "Suggestion",
    /// and the ModalTextInput attribute with a style of TextInputStyle.Paragraph and a placeholder of "Please enter suggestion".
    /// </remarks>
    [InputLabel("Suggestion")]
    [ModalTextInput("suggestion", TextInputStyle.Paragraph, "Please enter suggestion")]
    public string Suggestion { get; set; }
}