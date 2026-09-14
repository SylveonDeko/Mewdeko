using Discord.Interactions;

// ReSharper disable NotNullOrRequiredMemberIsNotInitialized

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal for adding an option to an existing ticket select menu.
/// </summary>
public class TicketOptionAddModal : IModal
{
    /// <summary>
    ///     Gets or sets the label shown to users for the option.
    /// </summary>
    [InputLabel("Label")]
    [ModalTextInput("label", TextInputStyle.Short, "The label shown to users", 1, 100)]
    public string Label { get; set; }

    /// <summary>
    ///     Gets or sets the optional description shown under the label.
    /// </summary>
    [InputLabel("Description")]
    [ModalTextInput("description", TextInputStyle.Short, "Optional description shown under the label", 0, 100)]
    [RequiredInput(false)]
    public string? Description { get; set; }

    /// <summary>
    ///     Gets or sets the optional emoji shown next to the label.
    /// </summary>
    [InputLabel("Emoji")]
    [ModalTextInput("emoji", TextInputStyle.Short, "Optional emoji shown next to the label", 0, 100)]
    [RequiredInput(false)]
    public string? Emoji { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Add Select Menu Option";
        }
    }
}

/// <summary>
///     Modal for adding a field to a ticket creation modal.
/// </summary>
public class TicketModalFieldModal : IModal
{
    /// <summary>
    ///     Gets or sets the label for the field.
    /// </summary>
    [InputLabel("Label")]
    [ModalTextInput("label", TextInputStyle.Short, "The label for the field", 1, 45)]
    public string Label { get; set; }

    /// <summary>
    ///     Gets or sets the comma separated field configuration: paragraph, optional, min:X, max:X.
    /// </summary>
    [InputLabel("Configuration")]
    [ModalTextInput("config", TextInputStyle.Short, "Comma separated: paragraph, optional, min:X, max:X", 0, 200)]
    [RequiredInput(false)]
    public string? Config { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Add Modal Field";
        }
    }
}