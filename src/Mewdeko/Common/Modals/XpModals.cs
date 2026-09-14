using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal for adding a custom level-up message template.
/// </summary>
public class XpLevelUpMessageModal : IModal
{
    /// <summary>
    ///     Gets or sets the level-up message text or embed json.
    /// </summary>
    [InputLabel("Message")]
    [ModalTextInput("message", TextInputStyle.Paragraph,
        "Text or embed json, placeholders like %xp.user.mention% are supported")]
    public string Message { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title
    {
        get
        {
            return "Add Level-Up Message";
        }
    }
}

/// <summary>
///     Modal for testing a level-up message template.
/// </summary>
public class XpLevelUpTestModal : IModal
{
    /// <summary>
    ///     Gets or sets the level-up message text or embed json to test.
    /// </summary>
    [InputLabel("Message")]
    [ModalTextInput("message", TextInputStyle.Paragraph, "Text or embed json to test, leave empty for a stored one")]
    [RequiredInput(false)]
    public string? Message { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title
    {
        get
        {
            return "Test Level-Up Message";
        }
    }
}