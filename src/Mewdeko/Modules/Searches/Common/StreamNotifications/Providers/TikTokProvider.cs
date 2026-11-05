#nullable enable

using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Serilog;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Providers;

/// <summary>
///     Provider for TikTok accounts. Live status comes from the public profile page and new videos from an RSS bridge
///     configured in the bot credentials. Every failure is swallowed and the last known data is kept.
/// </summary>
public class TikTokProvider : Provider
{
    private const string UserAgent =
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    private static readonly TimeSpan ProfileInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan VideoInterval = TimeSpan.FromMinutes(5);

    private static readonly Regex UrlRegex =
        new(@"(?:https?://)?(?:www\.|m\.)?tiktok\.com/@(?<name>[\w.\-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DataRegex = new(
        "<script id=\"__UNIVERSAL_DATA_FOR_REHYDRATION__\"[^>]*>(?<json>.*?)</script>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly TimeSpan BlockBackoff = TimeSpan.FromMinutes(15);

    private static readonly Regex CaptchaRegex = new(@"captcha_container|verify-bar-close|Security Check|Access Denied",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static DateTime blockedUntil = DateTime.MinValue;

    private static readonly Regex VideoIdRegex = new(@"/video/(?<id>\d+)", RegexOptions.Compiled);

    private static readonly Regex ImageRegex = new("<img[^>]+src=[\"'](?<src>[^\"']+)[\"']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly string bridgeUrl;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ConcurrentDictionary<string, AccountState> states = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Initializes a new instance of the <see cref="TikTokProvider" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="credsProvider">The credentials provider.</param>
    public TikTokProvider(IHttpClientFactory httpClientFactory, IBotCredentials credsProvider)
    {
        this.httpClientFactory = httpClientFactory;
        bridgeUrl = credsProvider.TikTokRssBridgeUrl ?? "";
    }

    /// <inheritdoc />
    public override FType Platform
    {
        get
        {
            return FType.TikTok;
        }
    }

    /// <inheritdoc />
    public override Task<bool> IsValidUrl(string url)
    {
        return Task.FromResult(UrlRegex.IsMatch(url));
    }

    /// <inheritdoc />
    public override async Task<StreamData?> GetStreamDataByUrlAsync(string url)
    {
        var match = UrlRegex.Match(url);
        if (!match.Success)
            return null;

        return await RefreshAsync(match.Groups["name"].Value, true).ConfigureAwait(false);
    }

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

    private async Task<StreamData?> RefreshAsync(string login, bool force)
    {
        var state = states.GetOrAdd(login, _ => new AccountState());
        var now = DateTime.UtcNow;

        try
        {
            if ((force || now - state.LastProfile >= ProfileInterval) && now >= blockedUntil)
            {
                state.LastProfile = now;
                var outcome = await RefreshProfileAsync(login, state).ConfigureAwait(false);
                if (outcome == ProfileOutcome.NotFound)
                {
                    FailingStreams.TryAdd(login, now);
                    return null;
                }

                FailingStreams.TryRemove(login, out _);
            }

            if (!string.IsNullOrWhiteSpace(bridgeUrl) && (force || now - state.LastVideos >= VideoInterval))
            {
                state.LastVideos = now;
                await RefreshVideosAsync(login, state).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.Debug("TikTok refresh failed for {Login}: {Error}", login, ex.Message);
        }

        return state.Data with
        {
            Name = state.Data.Name ?? login, UniqueName = login, StreamUrl = state.Data.IsLive
                ? $"https://www.tiktok.com/@{login}/live"
                : $"https://www.tiktok.com/@{login}"
        };
    }

    private async Task<ProfileOutcome> RefreshProfileAsync(string login, AccountState state)
    {
        using var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(15);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.tiktok.com/@{login}");
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml");

        using var response = await http.SendAsync(request).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return ProfileOutcome.NotFound;

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
        {
            Block();
            return ProfileOutcome.Unavailable;
        }

        if (!response.IsSuccessStatusCode)
            return ProfileOutcome.Unavailable;

        var html = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var match = DataRegex.Match(html);
        var detail = match.Success
            ? JsonNode.Parse(match.Groups["json"].Value)?["__DEFAULT_SCOPE__"]?["webapp.user-detail"]
            : null;
        if (detail is null)
        {
            if (CaptchaRegex.IsMatch(html))
                Block();

            return ProfileOutcome.Unavailable;
        }

        var statusCode = detail["statusCode"]?.GetValue<int>() ?? 0;
        var user = detail["userInfo"]?["user"];
        if (user is null)
            return statusCode is 10202 or 10221 ? ProfileOutcome.NotFound : ProfileOutcome.Unavailable;

        var roomId = user["roomId"]?.ToString();
        var live = !string.IsNullOrWhiteSpace(roomId) && roomId != "0";

        var data = state.Data with
        {
            Name = user["nickname"]?.ToString() ?? login,
            AvatarUrl = user["avatarLarger"]?.ToString() ?? "",
            ChannelId = user["secUid"]?.ToString() ?? "",
            IsLive = live
        };

        if (live)
        {
            var details = await GetLiveDetailsAsync(http, roomId!).ConfigureAwait(false);
            data = data with
            {
                IsLive = details.Alive ?? true,
                Title = details.Title ?? data.Title,
                Viewers = details.Viewers ?? data.Viewers,
                Preview = details.Cover ?? data.Preview
            };
        }
        else
        {
            data = data with
            {
                Title = "", Viewers = 0, Preview = ""
            };
        }

        state.Data = data;
        return ProfileOutcome.Ok;
    }

    private static async Task<(bool? Alive, string? Title, int? Viewers, string? Cover)> GetLiveDetailsAsync(
        HttpClient http, string roomId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://webcast.tiktok.com/webcast/room/info/?aid=1988&room_id={roomId}");
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

            using var response = await http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return (null, null, null, null);

            var root = JsonNode.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            var room = root?["data"];
            if (room is null || root?["status_code"]?.GetValue<int>() != 0 || room["status"] is null)
                return (null, null, null, null);

            var title = room["title"]?.ToString();
            return (room["status"]!.GetValue<int>() == 2, string.IsNullOrWhiteSpace(title) ? null : title,
                room["user_count"]?.GetValue<int>(), room["cover"]?["url_list"]?[0]?.ToString());
        }
        catch
        {
            return (null, null, null, null);
        }
    }

    private static void Block()
    {
        blockedUntil = DateTime.UtcNow + BlockBackoff;
        Log.Warning("TikTok blocked the profile request, pausing TikTok checks for {Minutes} minutes",
            BlockBackoff.TotalMinutes);
    }

    private async Task RefreshVideosAsync(string login, AccountState state)
    {
        using var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(15);

        var url = bridgeUrl.Replace("{username}", Uri.EscapeDataString(login), StringComparison.OrdinalIgnoreCase);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        using var response = await http.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return;

        var doc = XDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        var item = doc.Descendants().FirstOrDefault(x => x.Name.LocalName is "item" or "entry");
        if (item is null)
            return;

        string? Child(string name)
        {
            return item.Elements().FirstOrDefault(x => x.Name.LocalName == name)?.Value;
        }

        var link = Child("link");
        if (string.IsNullOrWhiteSpace(link))
            link = item.Elements().FirstOrDefault(x => x.Name.LocalName == "link")?.Attribute("href")?.Value;

        var guid = Child("guid") ?? Child("id");
        var videoMatch = VideoIdRegex.Match(link ?? "");
        var id = videoMatch.Success ? videoMatch.Groups["id"].Value : guid ?? link;
        if (string.IsNullOrWhiteSpace(id))
            return;

        var description = Child("description") ?? Child("summary") ?? Child("content") ?? "";
        var thumbnail = item.Descendants().FirstOrDefault(x => x.Name.LocalName == "thumbnail")?.Attribute("url")?.Value
                        ?? ImageRegex.Match(description).Groups["src"].Value;

        var title = Child("title");
        if (string.IsNullOrWhiteSpace(title))
            title = Regex.Replace(description, "<[^>]+>", "").Trim();

        state.Data = state.Data with
        {
            LatestVideoId = id,
            LatestVideoUrl = link,
            LatestVideoTitle = title,
            LatestVideoThumbnail = string.IsNullOrWhiteSpace(thumbnail) ? null : thumbnail
        };
    }

    private enum ProfileOutcome
    {
        Ok,
        Unavailable,
        NotFound
    }

    private sealed class AccountState
    {
        public StreamData Data { get; set; } = new()
        {
            StreamType = FType.TikTok, UniqueName = "", Title = "", Game = "", Preview = "", StreamUrl = "",
            AvatarUrl = "", ChannelId = ""
        };

        public DateTime LastProfile { get; set; } = DateTime.MinValue;
        public DateTime LastVideos { get; set; } = DateTime.MinValue;
    }
}
