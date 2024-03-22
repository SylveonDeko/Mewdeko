using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
/// Represents a modal for setting the pronouns for a user in the FCB (Full Combo Breaker) system.
/// </summary>
public class PronounsFcbModal : IModal
{
    /// <summary>
    /// Gets the title of the modal.
    /// </summary>
    public string Title => "PNOFC/B";

    /// <summary>
    /// Gets or sets the reason for the FCB action.
    /// </summary>
    /// <remarks>
    /// This property is decorated with the InputLabel attribute with a value of "Reason",
    /// and the ModalTextInput attribute with an initial value of "An anti-abuse report was actioned by a moderator.".
    /// </remarks>
    [ModalTextInput("fcb_reason", initValue: "An anti-abuse report was actioned by a moderator.")]
    [InputLabel("Reason")]
    public string? FcbReason { get; set; }
}