namespace Mewdeko.Modules.Searches.Common;

/// <summary>
/// Represents a search result from Google.
/// </summary>
public sealed class GoogleSearchResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleSearchResult"/> class.
    /// </summary>
    /// <param name="title">The title of the search result.</param>
    /// <param name="link">The link to the search result.</param>
    /// <param name="text">The text of the search result.</param>
    public GoogleSearchResult(string title, string link, string text)
    {
        Title = title;
        Link = link;
        Text = text;
    }

    /// <summary>
    /// Gets the title of the search result.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the link to the search result.
    /// </summary>
    public string Link { get; }

    /// <summary>
    /// Gets the text of the search result.
    /// </summary>
    public string Text { get; }
}