using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Serilog;
using TwitchLib.Api;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Providers;

public class TwitchHelixProvider : Provider
{
    private readonly IHttpClientFactory httpClientFactory;

    private static Regex Regex { get; } = new(@"twitch.tv/(?<name>[\w\d\-_]+)/?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override FollowedStream.FType Platform
        => FollowedStream.FType.Twitch;

    private readonly Lazy<TwitchAPI> api;
    private readonly string clientId;

    public TwitchHelixProvider(IHttpClientFactory httpClientFactory, IBotCredentials credsProvider)
    {
        this.httpClientFactory = httpClientFactory;

        var creds = credsProvider;
        clientId = creds.TwitchClientId;
        var clientSecret = creds.TwitchClientSecret;
        api = new Lazy<TwitchAPI>(() => new TwitchAPI
        {
            Helix =
            {
                Settings =
                {
                    ClientId = clientId, Secret = clientSecret
                }
            }
        });
    }

    private async Task<string?> EnsureTokenValidAsync()
        => await api.Value.Auth.GetAccessTokenAsync().ConfigureAwait(false);

    public override Task<bool> IsValidUrl(string url)
    {
        var match = Regex.Match(url);
        if (!match.Success)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public override Task<StreamData?> GetStreamDataByUrlAsync(string url)
    {
        var match = Regex.Match(url);
        if (!match.Success) return Task.FromResult<StreamData>(null);
        var name = match.Groups["name"].Value;
        return GetStreamDataAsync(name);
    }

    public override async Task<StreamData?> GetStreamDataAsync(string login)
    {
        var data = await GetStreamDataAsync(new List<string>
        {
            login
        }).ConfigureAwait(false);

        return data.FirstOrDefault();
    }

    public override async Task<IReadOnlyCollection<StreamData>> GetStreamDataAsync(List<string> logins)
    {
        if (logins.Count == 0)
        {
            return Array.Empty<StreamData>();
        }

        var token = await EnsureTokenValidAsync().ConfigureAwait(false);

        if (token is null)
        {
            Log.Warning("Twitch client ID and Secret are incorrect! Please go to https://dev.twitch.tv and create an application!");
            return Array.Empty<StreamData>();
        }

        using var http = httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.Add("Client-Id", clientId);
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var loginsSet = logins.Select(x => x.ToLowerInvariant())
            .Distinct()
            .ToHashSet();

        var dataDict = new Dictionary<string, StreamData>();

        foreach (var chunk in logins.Chunk(100))
        {
            try
            {
                var str = await http.GetStringAsync(
                    $"https://api.twitch.tv/helix/users?{chunk.Select(x => $"login={x}").Join('&')}&first=100").ConfigureAwait(false);

                var resObj = JsonSerializer.Deserialize<HelixUsersResponse>(str);

                if (resObj?.Data is null || resObj.Data.Count == 0)
                    continue;

                foreach (var user in resObj.Data)
                {
                    var lowerLogin = user.Login.ToLowerInvariant();
                    if (loginsSet.Remove(lowerLogin))
                    {
                        dataDict[lowerLogin] = UserToStreamData(user);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Something went wrong retreiving {StreamPlatform} streams", Platform);
                return new List<StreamData>();
            }
        }

        // any item left over loginsSet is an invalid username
        foreach (var login in loginsSet)
        {
            FailingStreams.TryAdd(login, DateTime.UtcNow);
        }

        // only get streams for users which exist
        foreach (var chunk in dataDict.Keys.Chunk(100))
        {
            try
            {
                var str = await http.GetStringAsync(
                    $"https://api.twitch.tv/helix/streams?{chunk.Select(x => $"user_login={x}").Join('&')}&first=100").ConfigureAwait(false);

                var res = JsonSerializer.Deserialize<HelixStreamsResponse>(str);

                if (res?.Data is null || res.Data.Count == 0)
                {
                    continue;
                }

                foreach (var helixStreamData in res.Data)
                {
                    var login = helixStreamData.UserLogin.ToLowerInvariant();
                    if (dataDict.TryGetValue(login, out var old))
                    {
                        dataDict[login] = FillStreamData(old, helixStreamData);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Something went wrong retreiving {StreamPlatform} streams", Platform);
                return new List<StreamData>();
            }
        }

        return dataDict.Values;
    }

    private static StreamData UserToStreamData(HelixUsersResponse.User user)
        => new()
        {
            UniqueName = user.Login,
            Name = user.DisplayName,
            AvatarUrl = user.ProfileImageUrl,
            IsLive = false,
            StreamUrl = $"https://twitch.tv/{user.Login}",
            StreamType = FollowedStream.FType.Twitch,
            Preview = user.OfflineImageUrl
        };

    private static StreamData FillStreamData(StreamData partial, HelixStreamsResponse.StreamData apiData)
        => partial with
        {
            StreamType = FollowedStream.FType.Twitch,
            Viewers = apiData.ViewerCount,
            Title = apiData.Title,
            IsLive = apiData.Type == "live",
            Preview = apiData.ThumbnailUrl
                .Replace("{width}", "640")
                .Replace("{height}", "480"),
            Game = apiData.GameName
        };
}