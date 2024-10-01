namespace Mewdeko.Modules.Games.Common;

/// <summary>
///     Represents a typing article for the typing game.
/// </summary>
public class TypingArticle
{
    /// <summary>
    ///     Gets or sets the source of the typing article.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    ///     Gets or sets any extra information related to the typing article.
    /// </summary>
    public string Extra { get; set; }

    /// <summary>
    ///     Gets or sets the text content of the typing article.
    /// </summary>
    public string? Text { get; set; }
}