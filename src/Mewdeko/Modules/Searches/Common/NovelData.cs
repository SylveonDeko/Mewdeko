namespace Mewdeko.Modules.Searches.Common;

/// <summary>
///     Represents information about a novel.
/// </summary>
public class NovelResult
{
    /// <summary>
    ///     Gets or sets the description of the novel.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    ///     Gets or sets the title of the novel.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    ///     Gets or sets the link to the novel.
    /// </summary>
    public string Link { get; set; }

    /// <summary>
    ///     Gets or sets the URL of the image associated with the novel.
    /// </summary>
    public string ImageUrl { get; set; }

    /// <summary>
    ///     Gets or sets the authors of the novel.
    /// </summary>
    public string[] Authors { get; set; }

    /// <summary>
    ///     Gets or sets the status of the novel (e.g., ongoing, completed).
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    ///     Gets or sets the genres of the novel.
    /// </summary>
    public string[] Genres { get; set; }

    /// <summary>
    ///     Gets or sets the score or rating of the novel.
    /// </summary>
    public string Score { get; set; }
}