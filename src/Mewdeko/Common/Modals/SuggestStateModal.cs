using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Represents a modal for suggesting a state change.
/// </summary>
public class SuggestStateModal : IModal
{
    /// <summary>
    ///     Gets or sets the reason for the state change.
    /// </summary>
    /// <remarks>
    ///     This property is decorated with the InputLabel attribute with a value of "Reason",
    ///     and the ModalTextInput attribute with a style of TextInputStyle.Paragraph and a placeholder of "Please enter
    ///     reason".
    ///     It is not a required input.
    /// </remarks>
    [InputLabel("Reason")]
    [ModalTextInput("suggestion", TextInputStyle.Paragraph, "Please enter reason")]
    [RequiredInput(false)]
    public string? Reason { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "State Change Reason";
        }
    }
}