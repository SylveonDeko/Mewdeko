using Discord.Interactions;

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal for testing how the bot parses embed json or plain text.
/// </summary>
public class DebugEmbedModal : IModal
{
    /// <summary>
    ///     Gets or sets the embed json or plain text to parse.
    /// </summary>
    [InputLabel("Embed")]
    [ModalTextInput("embed_text", TextInputStyle.Paragraph, "Embed json or plain text to parse")]
    public string EmbedText { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Debug Embed";
        }
    }
}

/// <summary>
///     Modal for setting the custom embed template used for AI responses.
/// </summary>
public class AiCustomEmbedModal : IModal
{
    /// <summary>
    ///     Gets or sets the embed template. Use %airesponse% where the AI response should appear.
    /// </summary>
    [InputLabel("Embed Template")]
    [ModalTextInput("embed_template", TextInputStyle.Paragraph,
        "Embed json. Use %airesponse% where the AI response should appear.")]
    public string EmbedTemplate { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "AI Custom Embed";
        }
    }
}

/// <summary>
///     Modal for entering the message of a repeater.
/// </summary>
public class RepeatMessageModal : IModal
{
    /// <summary>
    ///     Gets or sets the message text or embed json to repeat.
    /// </summary>
    [InputLabel("Message")]
    [ModalTextInput("message", TextInputStyle.Paragraph, "Text or embed json to repeat")]
    public string Message { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Repeater Message";
        }
    }
}

/// <summary>
///     Modal for adding a quote.
/// </summary>
public class QuoteAddModal : IModal
{
    /// <summary>
    ///     Gets or sets the keyword the quote is stored under.
    /// </summary>
    [InputLabel("Keyword")]
    [ModalTextInput("keyword", TextInputStyle.Short, "Keyword", maxLength: 100)]
    public string Keyword { get; set; }

    /// <summary>
    ///     Gets or sets the text or embed json of the quote.
    /// </summary>
    [InputLabel("Text")]
    [ModalTextInput("text", TextInputStyle.Paragraph, "Quote text or embed json")]
    public string Text { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Add Quote";
        }
    }
}

/// <summary>
///     Modal for saving an embed template.
/// </summary>
public class EmbedSaveModal : IModal
{
    /// <summary>
    ///     Gets or sets the name of the embed template.
    /// </summary>
    [InputLabel("Name")]
    [ModalTextInput("name", TextInputStyle.Short, "Template name", maxLength: 100)]
    public string Name { get; set; }

    /// <summary>
    ///     Gets or sets the embed json of the template.
    /// </summary>
    [InputLabel("Embed Json")]
    [ModalTextInput("json", TextInputStyle.Paragraph, "Embed json")]
    public string Json { get; set; }

    /// <summary>
    ///     Gets the title of the modal.
    /// </summary>
    public string Title
    {
        get
        {
            return "Save Embed Template";
        }
    }
}