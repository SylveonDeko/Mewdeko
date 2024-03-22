using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
/// Represents a modal for setting the user's pronouns.
/// </summary>
public class PronounsModal : IModal
{
    /// <summary>
    /// Gets the title of the modal.
    /// </summary>
    public string Title => "Set your pronouns";

    /// <summary>
    /// Gets or sets the user's pronouns.
    /// </summary>
    /// <remarks>
    /// This property is decorated with the InputLabel attribute with a value of "Pronouns",
    /// and the ModalTextInput attribute with no additional parameters.
    /// </remarks>
    [ModalTextInput("pronouns")]
    [InputLabel("Pronouns")]
    public string? Pronouns { get; set; }
}