#nullable enable

using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Providers;

/// <summary>
///     Provider for Bluesky accounts, read from the public author feed API. A followed name ending in +reposts also
///     reports reposts.
/// </summary>
public partial class BlueskyProvider : FeedProvider
{
    private const string RepostSuffix = "+reposts";
    private const string Api = "https://public.api.bsky.app/xrpc/app.bsky.feed.getAuthorFeed";

    private static readonly Regex UrlRegex = UrlPattern();

    /// <summary>
    ///     Initializes a new instance of the <see cref="BlueskyProvider" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public BlueskyProvider(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
    {
    }

    /// <inheritdoc />
    public override FType Platform
    {
        get
        {
            return FType.Bluesky;
        }
    }

    /// <inheritdoc />
    protected override TimeSpan Interval { get; } = TimeSpan.FromMinutes(2);

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
        var actor = ParseActor(input);
        if (actor is null)
            return null;

        var name = actor.ToLowerInvariant() + (includeReposts ? RepostSuffix : "");
        var data = await RefreshAsync(name, true).ConfigureAwait(false);
        return data;
    }

    private static string? ParseActor(string input)
    {
        input = input.Trim();
        var match = UrlRegex.Match(input);
        if (match.Success)
            input = match.Groups["actor"].Value;

        input = input.TrimStart('@').Trim();
        if (input.Length == 0 || input.Any(char.IsWhiteSpace))
            return null;

        if (input.StartsWith("did:", StringComparison.OrdinalIgnoreCase))
            return input;

        return input.Contains('.') ? input : $"{input}.bsky.social";
    }

    /// <inheritdoc />
    protected override async Task<FeedOutcome> FetchAsync(string login, FeedState state)
    {
        var includeReposts = login.EndsWith(RepostSuffix, StringComparison.OrdinalIgnoreCase);
        var actor = includeReposts ? login[..^RepostSuffix.Length] : login;

        using var response = await GetAsync(
            $"{Api}?actor={Uri.EscapeDataString(actor)}&limit=15&filter=posts_no_replies",
            "application/json").ConfigureAwait(false);
        if (response is null)
            return FeedOutcome.Unavailable;

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.BadRequest && body.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return FeedOutcome.NotFound;

        if (!response.IsSuccessStatusCode)
            return FeedOutcome.Unavailable;

        var feed = JsonNode.Parse(body)?["feed"]?.AsArray();
        if (feed is null)
            return FeedOutcome.Unavailable;

        var items = new List<StreamItem>();
        JsonNode? profile = null;
        foreach (var item in feed)
        {
            var reason = item?["reason"]?["$type"]?.ToString();
            if (reason is not null && reason.Contains("Pin", StringComparison.OrdinalIgnoreCase))
                continue;

            var isRepost = reason is not null && reason.Contains("Repost", StringComparison.OrdinalIgnoreCase);
            if (isRepost && !includeReposts)
                continue;

            var post = item?["post"];
            if (post is null)
                continue;

            profile ??= post["author"];
            var uri = post["uri"]?.ToString() ?? "";
            var authorHandle = post["author"]?["handle"]?.ToString() ?? actor;
            var text = post["record"]?["text"]?.ToString() ?? "";
            DateTimeOffset? created = DateTimeOffset.TryParse(post["record"]?["createdAt"]?.ToString(), out var parsed)
                ? parsed
                : null;

            if (isRepost)
            {
                created = DateTimeOffset.TryParse(item!["reason"]?["indexedAt"]?.ToString(), out var reposted)
                    ? reposted
                    : created;
            }

            items.Add(new StreamItem(
                isRepost ? $"{uri}#{item!["reason"]?["indexedAt"]}" : uri,
                $"https://bsky.app/profile/{authorHandle}/post/{uri[(uri.LastIndexOf('/') + 1)..]}",
                isRepost ? $"Repost of @{authorHandle}: {text.Trim()}".TrimTo(250) : text.Trim().TrimTo(250),
                text, PostImage(post["embed"]), null, created));
        }

        items = items.OrderByDescending(x => x.Published ?? DateTimeOffset.MinValue).ToList();
        var ownProfile = feed.Select(x => x?["post"]?["author"])
            .FirstOrDefault(x => string.Equals(x?["handle"]?.ToString(), actor, StringComparison.OrdinalIgnoreCase)
                                 || x?["did"]?.ToString() == actor);
        var displayName = (ownProfile ?? profile)?["displayName"]?.ToString();
        var handle = (ownProfile ?? profile)?["handle"]?.ToString() ?? actor;

        var data = NewData(FType.Bluesky, login, string.IsNullOrWhiteSpace(displayName) ? handle : displayName,
            $"https://bsky.app/profile/{handle}");
        data.AvatarUrl = (ownProfile ?? profile)?["avatar"]?.ToString() ?? "";

        ApplyItems(data, items, state.Data);
        state.Data = data;
        return FeedOutcome.Ok;
    }

    private static string? PostImage(JsonNode? embed)
    {
        if (embed is null)
            return null;

        return embed["images"]?[0]?["fullsize"]?.ToString()
               ?? embed["external"]?["thumb"]?.ToString()
               ?? embed["thumbnail"]?.ToString()
               ?? PostImage(embed["media"]);
    }

    [GeneratedRegex(@"bsky\.app/profile/(?<actor>[^/?#\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();
}
