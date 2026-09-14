using Discord.Interactions;

// ReSharper disable NotNullOrRequiredMemberIsNotInitialized

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal for entering the response of a reaction based chat trigger.
/// </summary>
public class ChatTriggerReactionAddModal : IModal
{
    /// <summary>
    ///     Gets or sets the message to respond with when the reaction is added.
    /// </summary>
    [InputLabel("Message")]
    [ModalTextInput("message", TextInputStyle.Paragraph, "The message to respond with")]
    public string Message { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Add Reaction Trigger";
        }
    }
}

/// <summary>
///     Modal for adding an extra response to a chat trigger.
/// </summary>
public class ChatTriggerResponseAddModal : IModal
{
    /// <summary>
    ///     Gets or sets the response to add.
    /// </summary>
    [InputLabel("Response")]
    [ModalTextInput("response", TextInputStyle.Paragraph, "The response to add")]
    public string Response { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Add Trigger Response";
        }
    }
}

/// <summary>
///     Modal for entering a sample message to test a chat trigger against.
/// </summary>
public class ChatTriggerTestModal : IModal
{
    /// <summary>
    ///     Gets or sets the sample message text.
    /// </summary>
    [InputLabel("Sample Message")]
    [ModalTextInput("sample", TextInputStyle.Paragraph, "The message text to test against")]
    public string Sample { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Test Chat Trigger";
        }
    }
}

/// <summary>
///     Modal for setting the message shown when a user does not meet a chat trigger's requirements.
/// </summary>
public class ChatTriggerReqMsgModal : IModal
{
    /// <summary>
    ///     Gets or sets the requirement failure message, or empty to fail silently.
    /// </summary>
    [InputLabel("Message")]
    [ModalTextInput("message", TextInputStyle.Paragraph, "Leave empty to fail silently")]
    [RequiredInput(false)]
    public string? Message { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Requirement Fail Message";
        }
    }
}