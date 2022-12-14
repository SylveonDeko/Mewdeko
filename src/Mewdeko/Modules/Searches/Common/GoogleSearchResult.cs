namespace Mewdeko.Modules.Searches.Common;

public sealed class GoogleSearchResult
{
    public GoogleSearchResult(string title, string link, string text)
    {
        Title = title;
        Link = link;
        Text = text;
    }

    public string Title { get; }
    public string Link { get; }
    public string Text { get; }
}