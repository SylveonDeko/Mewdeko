#nullable enable

using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Providers;

/// <summary>
///     Provider for YouTube channel uploads, read from the free per-channel RSS feed. A channel URL, handle or id is
///     resolved to the channel id by reading the channel page.
/// </summary>
public partial class YoutubeUploadsProvider : FeedProvider
{
    private const string FeedUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=";

    private static readonly Regex ChannelIdRegex = ChannelIdPattern();
    private static readonly Regex PageIdRegex = PageIdPattern();
    private static readonly Regex CanonicalRegex = CanonicalPattern();

    /// <summary>
    ///     Initializes a new instance of the <see cref="YoutubeUploadsProvider" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public YoutubeUploadsProvider(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
    {
    }

    /// <inheritdoc />
    public override FType Platform
    {
        get
        {
            return FType.YoutubeUploads;
        }
    }

    /// <inheritdoc />
    protected override TimeSpan Interval { get; } = TimeSpan.FromMinutes(10);

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
        var channelId = await ResolveChannelIdAsync(input.Trim()).ConfigureAwait(false);
        if (channelId is null)
            return null;

        var feedUrl = FeedUrl + channelId;
        var state = new FeedState();
        var outcome = await ReadFeedAsync(feedUrl, channelId, null, state).ConfigureAwait(false);
        if (outcome != FeedOutcome.Ok || state.Data is null)
            return null;

        var name = state.Data.UniqueName;
        RegisterSource(name, feedUrl);
        States[name] = state;
        state.LastCheck = DateTime.UtcNow;
        return state.Data;
    }

    /// <inheritdoc />
    protected override async Task<FeedOutcome> FetchAsync(string login, FeedState state)
    {
        if (!Sources.TryGetValue(login, out var feedUrl))
            return FeedOutcome.Unavailable;

        return await ReadFeedAsync(feedUrl, feedUrl[FeedUrl.Length..], login, state).ConfigureAwait(false);
    }

    private async Task<string?> ResolveChannelIdAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        if (ChannelIdRegex.IsMatch(input))
            return input;

        var channelUrl = Regex.Match(input, @"youtube\.com/channel/(?<id>UC[\w-]{22})", RegexOptions.IgnoreCase);
        if (channelUrl.Success)
            return channelUrl.Groups["id"].Value;

        string page;
        if (input.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
            input.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            page = input.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? input : $"https://{input}";
        else
            page = $"https://www.youtube.com/@{input.TrimStart('@')}";

        if (!Uri.TryCreate(page, UriKind.Absolute, out var uri) || !uri.Host.EndsWith("youtube.com") &&
            !uri.Host.EndsWith("youtu.be"))
            return null;

        using var response = await GetAsync(uri.ToString(), "text/html").ConfigureAwait(false);
        if (response is null || !response.IsSuccessStatusCode)
            return null;

        var html = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var match = PageIdRegex.Match(html);
        if (match.Success)
            return match.Groups["id"].Value;

        match = CanonicalRegex.Match(html);
        return match.Success ? match.Groups["id"].Value : null;
    }

    private async Task<FeedOutcome> ReadFeedAsync(string feedUrl, string channelId, string? name, FeedState state)
    {
        using var response = await GetAsync(feedUrl, "application/atom+xml").ConfigureAwait(false);
        if (response is null)
            return FeedOutcome.Unavailable;

        if (response.StatusCode == HttpStatusCode.NotFound)
            return FeedOutcome.NotFound;

        if (!response.IsSuccessStatusCode)
            return FeedOutcome.Unavailable;

        var doc = XDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        var root = doc.Root;
        if (root is null)
            return FeedOutcome.Unavailable;

        string? Child(XElement parent, string local)
        {
            return parent.Elements().FirstOrDefault(x => x.Name.LocalName == local)?.Value;
        }

        var title = Child(root, "title") ?? channelId;
        var channelPage = root.Elements().Where(x => x.Name.LocalName == "link")
            .FirstOrDefault(x => x.Attribute("rel")?.Value == "alternate")?.Attribute("href")?.Value
            ?? $"https://www.youtube.com/channel/{channelId}";

        name ??= MakeKey(title, channelId);
        var data = NewData(FType.YoutubeUploads, name, title, channelPage);
        data.ChannelId = channelId;
        data.SourceUrl = FeedUrl + channelId;

        var items = new List<StreamItem>();
        foreach (var entry in root.Elements().Where(x => x.Name.LocalName == "entry"))
        {
            var group = entry.Elements().FirstOrDefault(x => x.Name.LocalName == "group");
            var link = entry.Elements().Where(x => x.Name.LocalName == "link")
                .FirstOrDefault(x => x.Attribute("rel")?.Value == "alternate")?.Attribute("href")?.Value;
            var videoId = Child(entry, "videoId");
            var id = videoId ?? Child(entry, "id");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            DateTimeOffset? published = DateTimeOffset.TryParse(Child(entry, "published"), out var parsed)
                ? parsed
                : null;
            items.Add(new StreamItem(id, link ?? (videoId is null ? null : $"https://www.youtube.com/watch?v={videoId}"),
                Child(entry, "title"), group is null ? null : CleanText(Child(group, "description")),
                group?.Elements().FirstOrDefault(x => x.Name.LocalName == "thumbnail")?.Attribute("url")?.Value, null,
                published));
        }

        items = items.OrderByDescending(x => x.Published ?? DateTimeOffset.MinValue).ToList();
        ApplyItems(data, items, state.Data);
        state.Data = data;
        return FeedOutcome.Ok;
    }

    [GeneratedRegex(@"^UC[\w-]{22}$")]
    private static partial Regex ChannelIdPattern();

    [GeneratedRegex("\"(?:externalId|channelId)\":\"(?<id>UC[\\w-]{22})\"")]
    private static partial Regex PageIdPattern();

    [GeneratedRegex("<link rel=\"canonical\" href=\"https?://www\\.youtube\\.com/channel/(?<id>UC[\\w-]{22})\"")]
    private static partial Regex CanonicalPattern();
}
