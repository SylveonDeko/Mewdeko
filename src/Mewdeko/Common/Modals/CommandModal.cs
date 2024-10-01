using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Represents a modal for command input.
/// </summary>
public class CommandModal : IModal
{
    /// <summary>
    ///     Gets or sets the command arguments.
    /// </summary>
    /// <remarks>
    ///     This property is decorated with the InputLabel attribute with a value of "Args",
    ///     and the ModalTextInput attribute with a style of TextInputStyle.Paragraph and a placeholder of "Please enter
    ///     command arguments".
    ///     It is not a required input.
    /// </remarks>
    [InputLabel("Args")]
    [ModalTextInput("args", TextInputStyle.Paragraph, "Please enter command arguments")]
    [RequiredInput(false)]
    public string? Args { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Command Input";
        }
    }
}