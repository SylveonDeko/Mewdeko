#nullable enable

using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Serilog;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Providers;

/// <summary>
///     Provider for new subreddit posts, read from the public subreddit JSON with the RSS feed as a fallback. Requests
///     are spaced out across every followed subreddit and paused when Reddit pushes back.
/// </summary>
public partial class RedditProvider : FeedProvider
{
    private static readonly TimeSpan RequestSpacing = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DefaultBackoff = TimeSpan.FromMinutes(15);
    private static readonly Regex UrlRegex = UrlPattern();
    private static readonly Regex NameRegex = NamePattern();
    private static readonly SemaphoreSlim RequestGate = new(1, 1);

    private static DateTime lastRequest = DateTime.MinValue;
    private static DateTime blockedUntil = DateTime.MinValue;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RedditProvider" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public RedditProvider(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
    {
    }

    /// <inheritdoc />
    public override FType Platform
    {
        get
        {
            return FType.Reddit;
        }
    }

    /// <inheritdoc />
    protected override TimeSpan Interval { get; } = TimeSpan.FromMinutes(3);

    /// <inheritdoc />
    public override Task<bool> IsValidUrl(string url)
    {
        return Task.FromResult(UrlRegex.IsMatch(url));
    }

    /// <inheritdoc />
    public override Task<StreamData?> GetStreamDataByUrlAsync(string url)
    {
        return ResolveAsync(url, false);
    }

    /// <inheritdoc />
    public override async Task<StreamData?> ResolveAsync(string input, bool includeReposts)
    {
        var name = ParseSubreddit(input);
        if (name is null)
            return null;

        var data = await RefreshAsync(name, true).ConfigureAwait(false);
        if (data is null && FailingStreams.ContainsKey(name))
            return null;

        return data ?? NewData(FType.Reddit, name, $"r/{name}", $"https://www.reddit.com/r/{name}");
    }

    private static string? ParseSubreddit(string input)
    {
        input = input.Trim();
        var match = UrlRegex.Match(input);
        if (match.Success)
            input = match.Groups["name"].Value;

        input = input.TrimStart('/');
        if (input.StartsWith("r/", StringComparison.OrdinalIgnoreCase))
            input = input[2..];

        input = input.Trim('/').ToLowerInvariant();
        return NameRegex.IsMatch(input) ? input : null;
    }

    /// <inheritdoc />
    protected override async Task<FeedOutcome> FetchAsync(string login, FeedState state)
    {
        if (DateTime.UtcNow < blockedUntil)
            return FeedOutcome.Unavailable;

        var data = NewData(FType.Reddit, login, $"r/{login}", $"https://www.reddit.com/r/{login}");

        var previous = state.Data;
        var outcome = await ReadJsonAsync(login, data, previous).ConfigureAwait(false);
        if (outcome == FeedOutcome.Unavailable && DateTime.UtcNow >= blockedUntil)
            outcome = await ReadRssAsync(login, data, previous).ConfigureAwait(false);

        if (outcome == FeedOutcome.Ok)
            state.Data = data;

        return outcome;
    }

    private async Task<HttpResponseMessage?> ThrottledGetAsync(string url, string accept)
    {
        await RequestGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var wait = lastRequest + RequestSpacing - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait).ConfigureAwait(false);

            var response = await GetAsync(url, accept).ConfigureAwait(false);
            lastRequest = DateTime.UtcNow;

            if (response?.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden)
            {
                var retry = response.Headers.RetryAfter?.Delta;
                var pause = retry is { } delta && delta > TimeSpan.Zero ? delta : DefaultBackoff;
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    blockedUntil = DateTime.UtcNow + pause;
                    Log.Debug("Reddit rate limited the bot, pausing Reddit checks for {Minutes} minutes",
                        pause.TotalMinutes);
                }
            }

            return response;
        }
        finally
        {
            RequestGate.Release();
        }
    }

    private async Task<FeedOutcome> ReadJsonAsync(string name, StreamData data, StreamData? previous)
    {
        using var response =
            await ThrottledGetAsync($"https://www.reddit.com/r/{name}/new.json?limit=12&raw_json=1",
                "application/json").ConfigureAwait(false);
        if (response is null)
            return FeedOutcome.Unavailable;

        if (response.StatusCode == HttpStatusCode.NotFound)
            return FeedOutcome.NotFound;

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var isJson = body.TrimStart().StartsWith('{');
        if (response.StatusCode == HttpStatusCode.Forbidden && isJson &&
            body.Contains("\"reason\"", StringComparison.Ordinal))
            return FeedOutcome.NotFound;

        if (!response.IsSuccessStatusCode || !isJson)
            return FeedOutcome.Unavailable;

        var children = JsonNode.Parse(body)?["data"]?["children"]?.AsArray();
        if (children is null)
            return FeedOutcome.Unavailable;

        if (children.Count > 0 && children[0]?["kind"]?.ToString() != "t3")
            return FeedOutcome.NotFound;

        var items = new List<StreamItem>();
        foreach (var post in children.Select(x => x?["data"]))
        {
            if (post is null || post["stickied"]?.GetValue<bool>() == true || post["id"] is null)
                continue;

            var text = post["selftext"]?.ToString();
            var over18 = post["over_18"]?.GetValue<bool>() == true;
            string? thumbnail = null;
            if (!over18)
                thumbnail = post["preview"]?["images"]?[0]?["source"]?["url"]?.ToString()
                            ?? (post["thumbnail"]?.ToString() is { } thumb &&
                                thumb.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                                    ? thumb
                                    : null);

            DateTimeOffset? created = post["created_utc"] is { } stamp
                ? DateTimeOffset.FromUnixTimeSeconds((long)stamp.GetValue<double>())
                : null;

            items.Add(new StreamItem(post["id"]!.ToString(), $"https://www.reddit.com{post["permalink"]}",
                (post["title"]?.ToString() ?? "").TrimTo(250),
                string.IsNullOrWhiteSpace(text) ? post["url"]?.ToString() : text, thumbnail, null, created));
        }

        ApplyItems(data, items.OrderByDescending(x => x.Published ?? DateTimeOffset.MinValue).ToList(), previous);
        return FeedOutcome.Ok;
    }

    private async Task<FeedOutcome> ReadRssAsync(string name, StreamData data, StreamData? previous)
    {
        using var response =
            await ThrottledGetAsync($"https://www.reddit.com/r/{name}/new/.rss?limit=12", "application/atom+xml")
                .ConfigureAwait(false);
        if (response is null)
            return FeedOutcome.Unavailable;

        if (response.StatusCode == HttpStatusCode.NotFound)
            return FeedOutcome.NotFound;

        if (!response.IsSuccessStatusCode)
            return FeedOutcome.Unavailable;

        XDocument doc;
        try
        {
            doc = XDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        }
        catch
        {
            return FeedOutcome.Unavailable;
        }

        var items = new List<StreamItem>();
        foreach (var entry in doc.Descendants().Where(x => x.Name.LocalName == "entry"))
        {
            string? Child(string local)
            {
                return entry.Elements().FirstOrDefault(x => x.Name.LocalName == local)?.Value;
            }

            var id = Child("id");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            DateTimeOffset? updated = DateTimeOffset.TryParse(Child("updated"), out var parsed) ? parsed : null;
            items.Add(new StreamItem(id, entry.Elements().FirstOrDefault(x => x.Name.LocalName == "link")
                    ?.Attribute("href")?.Value, (Child("title") ?? "").TrimTo(250),
                CleanText(Child("content")).TrimTo(1000),
                entry.Elements().FirstOrDefault(x => x.Name.LocalName == "thumbnail")?.Attribute("url")?.Value, null,
                updated));
        }

        ApplyItems(data, items.OrderByDescending(x => x.Published ?? DateTimeOffset.MinValue).ToList(), previous);
        return FeedOutcome.Ok;
    }

    [GeneratedRegex(@"reddit\.com/r/(?<name>[A-Za-z0-9_]+)", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    [GeneratedRegex("^[a-z0-9_]{2,21}$")]
    private static partial Regex NamePattern();
}
