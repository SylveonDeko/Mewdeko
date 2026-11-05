#nullable enable

using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Serilog;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Providers;

/// <summary>
///     A podcast found through the iTunes Search API.
/// </summary>
/// <param name="Name">The podcast's name.</param>
/// <param name="Author">The podcast's author.</param>
/// <param name="FeedUrl">The RSS feed URL.</param>
/// <param name="Url">The podcast's page.</param>
/// <param name="ArtworkUrl">The podcast's artwork.</param>
/// <param name="Genre">The podcast's main genre.</param>
public record PodcastSearchResult(string Name, string? Author, string FeedUrl, string? Url, string? ArtworkUrl,
    string? Genre);

/// <summary>
///     Provider for new podcast episodes, read from the show's RSS feed. A feed URL, or an Apple Podcasts link that is
///     looked up through the free iTunes Search API, identifies the show.
/// </summary>
public partial class PodcastProvider : FeedProvider
{
    private static readonly Regex AppleIdRegex = AppleIdPattern();

    private readonly IHttpClientFactory httpClientFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PodcastProvider" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public PodcastProvider(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public override FType Platform
    {
        get
        {
            return FType.Podcast;
        }
    }

    /// <inheritdoc />
    protected override TimeSpan Interval { get; } = TimeSpan.FromMinutes(15);

    /// <summary>
    ///     Searches the free iTunes Search API for podcasts.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="term">The search term.</param>
    /// <param name="limit">How many results to ask for.</param>
    /// <returns>The podcasts found, empty when nothing matched or the API could not be reached.</returns>
    public static async Task<IReadOnlyList<PodcastSearchResult>> SearchAsync(IHttpClientFactory httpClientFactory,
        string term, int limit = 5)
    {
        return await QueryItunesAsync(httpClientFactory,
            $"https://itunes.apple.com/search?media=podcast&entity=podcast&limit={limit}&term={Uri.EscapeDataString(term)}")
            .ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<PodcastSearchResult>> QueryItunesAsync(
        IHttpClientFactory httpClientFactory, string url)
    {
        try
        {
            using var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(15);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            using var response = await http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            var results = JsonNode.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false))?["results"]
                ?.AsArray();
            if (results is null)
                return [];

            var found = new List<PodcastSearchResult>();
            foreach (var item in results)
            {
                var feed = item?["feedUrl"]?.ToString();
                var name = item?["collectionName"]?.ToString() ?? item?["trackName"]?.ToString();
                if (string.IsNullOrWhiteSpace(feed) || string.IsNullOrWhiteSpace(name))
                    continue;

                found.Add(new PodcastSearchResult(name, item?["artistName"]?.ToString(), feed,
                    item?["collectionViewUrl"]?.ToString(), item?["artworkUrl600"]?.ToString()
                                                           ?? item?["artworkUrl100"]?.ToString(),
                    item?["primaryGenreName"]?.ToString()));
            }

            return found;
        }
        catch (Exception ex)
        {
            Log.Debug("iTunes podcast lookup failed: {Error}", ex.Message);
            return [];
        }
    }

    /// <inheritdoc />
    public override Task<bool> IsValidUrl(string url)
    {
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public override Task<StreamData?> GetStreamDataByUrlAsync(string url)
    {
        return ResolveAsync(url, false);
    }

    /// <inheritdoc />
    public override async Task<StreamData?> ResolveAsync(string input, bool includeReposts)
    {
        input = input.Trim();
        var feedUrl = input;

        var apple = AppleIdRegex.Match(input);
        if (apple.Success)
        {
            var found = await QueryItunesAsync(httpClientFactory,
                $"https://itunes.apple.com/lookup?id={apple.Groups["id"].Value}&entity=podcast").ConfigureAwait(false);
            if (found.Count == 0)
                return null;

            feedUrl = found[0].FeedUrl;
        }

        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return null;

        var state = new FeedState();
        var outcome = await ReadFeedAsync(uri.ToString(), null, state).ConfigureAwait(false);
        if (outcome != FeedOutcome.Ok || state.Data is null)
            return null;

        RegisterSource(state.Data.UniqueName, uri.ToString());
        state.LastCheck = DateTime.UtcNow;
        States[state.Data.UniqueName] = state;
        return state.Data;
    }

    /// <inheritdoc />
    protected override async Task<FeedOutcome> FetchAsync(string login, FeedState state)
    {
        if (!Sources.TryGetValue(login, out var feedUrl))
            return FeedOutcome.Unavailable;

        return await ReadFeedAsync(feedUrl, login, state).ConfigureAwait(false);
    }

    private async Task<FeedOutcome> ReadFeedAsync(string feedUrl, string? name, FeedState state)
    {
        using var response = await GetAsync(feedUrl, "application/rss+xml, application/xml, text/xml")
            .ConfigureAwait(false);
        if (response is null)
            return FeedOutcome.Unavailable;

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            return FeedOutcome.NotFound;

        if (!response.IsSuccessStatusCode)
            return FeedOutcome.Unavailable;

        string? title = null;
        string? link = null;
        string? image = null;
        var itemElements = new List<XElement>();

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersFromEntities = 1024
        });

        try
        {
            if (!reader.ReadToFollowing("channel"))
                return FeedOutcome.Unavailable;

            var depth = reader.Depth;
            reader.Read();
            while (!reader.EOF && reader.Depth > depth)
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                switch (reader.LocalName)
                {
                    case "title" when title is null && reader.Depth == depth + 1:
                        title = reader.ReadElementContentAsString();
                        continue;
                    case "link" when link is null && reader.Depth == depth + 1 && reader.NamespaceURI == "":
                        link = reader.ReadElementContentAsString();
                        continue;
                    case "image" when reader.Prefix == "itunes":
                        image ??= reader.GetAttribute("href");
                        reader.Read();
                        continue;
                    case "item" when reader.Depth == depth + 1:
                        itemElements.Add((XElement)XNode.ReadFrom(reader));
                        if (itemElements.Count >= 12)
                            goto doneReading;
                        continue;
                    default:
                        reader.Read();
                        continue;
                }
            }

            doneReading: ;
        }
        catch (XmlException)
        {
            if (title is null)
                return FeedOutcome.Unavailable;
        }

        if (string.IsNullOrWhiteSpace(title))
            return FeedOutcome.Unavailable;

        name ??= MakeKey(title, feedUrl);
        var data = NewData(FType.Podcast, name, title.Trim(), string.IsNullOrWhiteSpace(link) ? feedUrl : link);
        data.AvatarUrl = image ?? "";
        data.SourceUrl = feedUrl;

        var items = new List<StreamItem>();
        foreach (var item in itemElements)
        {
            string? Child(string local)
            {
                return item.Elements().FirstOrDefault(x => x.Name.LocalName == local)?.Value;
            }

            var enclosure = item.Elements().FirstOrDefault(x => x.Name.LocalName == "enclosure")?.Attribute("url")
                ?.Value;
            var guid = Child("guid");
            var itemLink = Child("link");
            var id = !string.IsNullOrWhiteSpace(guid) ? guid : enclosure ?? itemLink;
            if (string.IsNullOrWhiteSpace(id))
                continue;

            DateTimeOffset? published = DateTimeOffset.TryParse(Child("pubDate"), out var parsed) ? parsed : null;
            items.Add(new StreamItem(id.Trim(), string.IsNullOrWhiteSpace(itemLink) ? data.StreamUrl : itemLink,
                CleanText(Child("title")).TrimTo(250),
                CleanText(Child("encoded") ?? Child("summary") ?? Child("description")).TrimTo(1000),
                item.Elements().FirstOrDefault(x => x.Name.LocalName == "image")?.Attribute("href")?.Value ?? image,
                enclosure, published));
        }

        ApplyItems(data, items.OrderByDescending(x => x.Published ?? DateTimeOffset.MinValue).ToList(), state.Data);
        state.Data = data;
        return FeedOutcome.Ok;
    }

    [GeneratedRegex(@"podcasts\.apple\.com/.*?/id(?<id>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex AppleIdPattern();
}
