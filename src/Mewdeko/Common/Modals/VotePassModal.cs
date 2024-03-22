using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
/// Represents a modal for entering a vote password.
/// </summary>
public class VotePasswordModal : IModal
{
    /// <summary>
    /// Gets the title of the modal.
    /// </summary>
    public string Title => "Vote Password";

    /// <summary>
    /// Gets or sets the vote password.
    /// </summary>
    /// <remarks>
    /// This property is decorated with the InputLabel attribute with a value of "VotePassword",
    /// and the ModalTextInput attribute with a style of TextInputStyle.Paragraph and a placeholder of "Please enter the password. DO NOT SHARE THIS TO ANYONE ELSE".
    /// It is not a required input.
    /// </remarks>
    [InputLabel("VotePassword")]
    [ModalTextInput("votepassword", TextInputStyle.Paragraph,
         "Please enter the password. DO NOT SHARE THIS TO ANYONE ELSE"), RequiredInput(false)]
    public string? Password { get; set; }
}