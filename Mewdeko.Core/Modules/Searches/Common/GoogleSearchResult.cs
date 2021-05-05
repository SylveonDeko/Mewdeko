namespace Mewdeko.Modules.Searches.Common
{
    public readonly struct GoogleSearchResult
    {
        public string Title { get; }
        public string Link { get; }
        public string Text { get; }

        public GoogleSearchResult(string title, string link, string text)
        {
            this.Title = title;
            this.Link = link;
            this.Text = text;
        }
    }
}
