using Discord.Interactions;

// ReSharper disable NotNullOrRequiredMemberIsNotInitialized

namespace Mewdeko.Common.Modals;

/// <summary>
///     Modal for adding a new typing game article.
/// </summary>
public class TypingArticleModal : IModal
{
    /// <summary>
    ///     Gets or sets the article text players will have to type.
    /// </summary>
    [InputLabel("Article Text")]
    [ModalTextInput("article_text", TextInputStyle.Paragraph, "The text players will race to type")]
    public string Text { get; set; }

    /// <summary>
    ///     Gets the modal title.
    /// </summary>
    public string Title
    {
        get
        {
            return "Add Typing Article";
        }
    }
}