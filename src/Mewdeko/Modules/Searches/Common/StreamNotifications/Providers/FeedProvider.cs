#nullable enable

using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Serilog;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Providers;

/// <summary>
///     Base class for providers that watch a free public feed or API for new items. Items are reported through the new
///     video event, every request is made at a polite interval and every failure is swallowed.
/// </summary>
public abstract partial class FeedProvider : Provider
{
    /// <summary>
    ///     The User-Agent sent with every feed request.
    /// </summary>
    protected const string UserAgent = "Mewdeko/1.0 (Discord bot; +https://mewdeko.tech)";

    /// <summary>
    ///     The most new items one check reports, so a backlog after downtime cannot flood a channel.
    /// </summary>
    protected const int MaxItemsPerCheck = 5;

    private readonly IHttpClientFactory httpClientFactory;

    /// <summary>
    ///     The last known state of each followed source.
    /// </summary>
    protected readonly ConcurrentDictionary<string, FeedState> States = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The feed URLs of followed sources, keyed by followed name.
    /// </summary>
    protected readonly ConcurrentDictionary<string, string> Sources = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Initializes a new instance of the <see cref="FeedProvider" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    protected FeedProvider(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }

    /// <summary>
    ///     Gets how long to wait before asking about the same source again.
    /// </summary>
    protected abstract TimeSpan Interval { get; }

    /// <inheritdoc />
    public override Task<StreamData?> GetStreamDataAsync(string login)
    {
        return RefreshAsync(login, false);
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyCollection<StreamData>> GetStreamDataAsync(List<string> usernames)
    {
        var results = new List<StreamData>();
        foreach (var login in usernames)
        {
            var data = await RefreshAsync(login, false).ConfigureAwait(false);
            if (data is not null)
                results.Add(data);
        }

        return results;
    }

    /// <inheritdoc />
    public override void RegisterSource(string name, string sourceUrl)
    {
        Sources[name] = sourceUrl;
    }

    /// <inheritdoc />
    public override void ClearErrorsFor(string? login)
    {
        if (login is null)
            FailingStreams.Clear();
        else
            FailingStreams.TryRemove(login, out _);
    }

    /// <summary>
    ///     Fetches the newest data of a source and stores it in its state.
    /// </summary>
    /// <param name="login">The followed name.</param>
    /// <param name="state">The state to fill.</param>
    /// <returns>How the fetch went.</returns>
    protected abstract Task<FeedOutcome> FetchAsync(string login, FeedState state);

    /// <summary>
    ///     Refreshes a source if it is due, or when forced, and returns what is known about it.
    /// </summary>
    /// <param name="login">The followed name.</param>
    /// <param name="force">Whether to ignore the polling interval.</param>
    /// <returns>The source's data, or null while nothing is known.</returns>
    protected async Task<StreamData?> RefreshAsync(string login, bool force)
    {
        var state = States.GetOrAdd(login, _ => new FeedState());
        var now = DateTime.UtcNow;
        if (!force && state.Data is not null && now - state.LastCheck < Interval)
            return state.Data;

        if (!force && state.Data is null && now - state.LastCheck < TimeSpan.FromMinutes(1))
            return null;

        state.LastCheck = now;
        try
        {
            switch (await FetchAsync(login, state).ConfigureAwait(false))
            {
                case FeedOutcome.NotFound:
                    FailingStreams.TryAdd(login, now);
                    return null;
                case FeedOutcome.Ok:
                    FailingStreams.TryRemove(login, out _);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Debug("{Platform} refresh failed for {Login}: {Error}", Platform, login, ex.Message);
        }

        return state.Data;
    }

    /// <summary>
    ///     Sends a GET request with the bot's User-Agent.
    /// </summary>
    /// <param name="url">The URL to fetch.</param>
    /// <param name="accept">The Accept header, if any.</param>
    /// <param name="userAgent">An override for the User-Agent.</param>
    /// <returns>The response, headers read, or null when the request failed.</returns>
    protected async Task<HttpResponseMessage?> GetAsync(string url, string? accept = null, string? userAgent = null)
    {
        try
        {
            using var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(20);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent ?? UserAgent);
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            if (accept is not null)
                request.Headers.TryAddWithoutValidation("Accept", accept);

            return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Debug("{Platform} request to {Url} failed: {Error}", Platform, url, ex.Message);
            return null;
        }
    }

    /// <summary>
    ///     Builds a followed name from a title and the source it was found at, safe to lowercase.
    /// </summary>
    /// <param name="title">The title to base the name on.</param>
    /// <param name="source">The source URL or id the name must stay unique for.</param>
    /// <returns>A lowercase name such as the-daily-3fa91c2d.</returns>
    public static string MakeKey(string title, string source)
    {
        var slug = SlugRegex().Replace(title.ToLowerInvariant(), "-").Trim('-');
        if (slug.Length > 30)
            slug = slug[..30].Trim('-');

        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(source)))[..8].ToLowerInvariant();
        return slug.Length == 0 ? hash : $"{slug}-{hash}";
    }

    /// <summary>
    ///     Strips markup from feed text and collapses its whitespace.
    /// </summary>
    /// <param name="html">The text to clean.</param>
    /// <returns>Plain text.</returns>
    public static string CleanText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";

        var text = TagRegex().Replace(html, " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return SpaceRegex().Replace(text, " ").Trim();
    }

    /// <summary>
    ///     Fills stream data from a feed's items and works out which of them are new since the previous check.
    /// </summary>
    /// <param name="data">The data to fill.</param>
    /// <param name="items">The feed's items, newest first.</param>
    /// <param name="previous">The data from the previous check, or null on the first check, which only records.</param>
    protected static void ApplyItems(StreamData data, IReadOnlyList<StreamItem> items, StreamData? previous)
    {
        if (items.Count == 0)
            return;

        var newest = items[0];
        data.LatestVideoId = newest.Id;
        data.LatestVideoUrl = newest.Url;
        data.LatestVideoTitle = newest.Title;
        data.LatestVideoDescription = newest.Description;
        data.LatestVideoThumbnail = newest.Thumbnail;
        data.LatestVideoMedia = newest.Media;
        data.LatestVideoPublished = newest.Published;

        if (string.IsNullOrEmpty(previous?.LatestVideoId))
            return;

        var fresh = new List<StreamItem>();
        var found = false;
        foreach (var item in items)
        {
            if (item.Id == previous.LatestVideoId)
            {
                found = true;
                break;
            }

            fresh.Add(item);
        }

        if (!found)
        {
            fresh = previous.LatestVideoPublished is { } last
                ? items.Where(x => x.Published > last).ToList()
                : [newest];
        }

        if (fresh.Count == 0)
            return;

        data.SkippedItems = Math.Max(0, fresh.Count - MaxItemsPerCheck);
        data.NewItems = fresh.Take(MaxItemsPerCheck).Reverse().ToList();
    }

    /// <summary>
    ///     Creates empty stream data for a feed source.
    /// </summary>
    /// <param name="type">The platform.</param>
    /// <param name="name">The followed name.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="url">The URL of the source's page.</param>
    /// <returns>Data that is never live.</returns>
    protected static StreamData NewData(FType type, string name, string displayName, string url)
    {
        return new StreamData
        {
            StreamType = type, UniqueName = name, Name = displayName, Title = "", Game = "", Preview = "",
            StreamUrl = url, AvatarUrl = "", ChannelId = "", IsLive = false
        };
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();

    /// <summary>
    ///     How a fetch went.
    /// </summary>
    protected enum FeedOutcome
    {
        /// <summary>
        ///     The data was read.
        /// </summary>
        Ok,

        /// <summary>
        ///     The source could not be reached right now.
        /// </summary>
        Unavailable,

        /// <summary>
        ///     The source does not exist.
        /// </summary>
        NotFound
    }

    /// <summary>
    ///     What is known about a followed source.
    /// </summary>
    protected sealed class FeedState
    {
        /// <summary>
        ///     Gets or sets the last data read, or null before the first successful read.
        /// </summary>
        public StreamData? Data { get; set; }

        /// <summary>
        ///     Gets or sets when the source was last asked about.
        /// </summary>
        public DateTime LastCheck { get; set; } = DateTime.MinValue;
    }
}
