#nullable enable

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Serilog;

namespace Mewdeko.Modules.Searches.Common.StreamNotifications.Providers;

/// <summary>
///     Provider for Kick streaming platform.
/// </summary>
public class KickProvider : Provider
{
    private readonly string clientId;
    private readonly string clientSecret;
    private readonly IHttpClientFactory httpClientFactory;
    private string? cachedAccessToken;
    private DateTime tokenExpiresAt = DateTime.MinValue;

    /// <summary>
    ///     Initializes a new instance of the <see cref="KickProvider" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="credsProvider">The credentials provider.</param>
    public KickProvider(IHttpClientFactory httpClientFactory, IBotCredentials credsProvider)
    {
        this.httpClientFactory = httpClientFactory;
        clientId = credsProvider.KickClientId;
        clientSecret = credsProvider.KickClientSecret;
    }

    private static Regex Regex { get; } = new(@"(?:https?://)?(?:www\.)?kick\.com/(?<name>[\w\d\-_]+)/?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <inheritdoc />
    public override FType Platform
    {
        get
        {
            return FType.Kick;
        }
    }

    /// <summary>
    ///     Ensures a valid access token is available, fetching a new one if needed.
    /// </summary>
    /// <returns>The access token, or null if unable to obtain one.</returns>
    private async Task<string?> EnsureTokenValidAsync()
    {
        // Return cached token if still valid
        if (!string.IsNullOrEmpty(cachedAccessToken) && DateTime.UtcNow < tokenExpiresAt)
            return cachedAccessToken;

        try
        {
            using var http = httpClientFactory.CreateClient();

            // Build form data for client credentials flow
            var formData = new Dictionary<string, string>
            {
                {
                    "grant_type", "client_credentials"
                },
                {
                    "client_id", clientId
                },
                {
                    "client_secret", clientSecret
                }
            };

            var content = new FormUrlEncodedContent(formData);

            // Ensure Content-Type header is set correctly
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            Log.Information("Requesting Kick OAuth token with client_id: {ClientId}", clientId);

            var response = await http.PostAsync("https://id.kick.com/oauth/token", content).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Log.Warning("Kick OAuth token request failed with status {StatusCode}: {ErrorBody}",
                    response.StatusCode, errorBody);
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Log.Information("Kick OAuth token response: {Response}", responseBody);

            var tokenResponse = JsonSerializer.Deserialize<KickTokenResponse>(responseBody);

            if (tokenResponse?.AccessToken is null)
            {
                Log.Warning("Kick OAuth response did not contain an access token");
                return null;
            }

            // Cache the token (subtract 60 seconds for safety margin)
            cachedAccessToken = tokenResponse.AccessToken;
            tokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);

            Log.Information("Successfully obtained Kick OAuth token, expires in {ExpiresIn} seconds",
                tokenResponse.ExpiresIn);

            return cachedAccessToken;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching Kick OAuth token: {ErrorMessage}", ex.Message);
            return null;
        }
    }

    /// <inheritdoc />
    public override Task<bool> IsValidUrl(string url)
    {
        var match = Regex.Match(url);
        return Task.FromResult(match.Success);
    }

    /// <inheritdoc />
    public override Task<StreamData?> GetStreamDataByUrlAsync(string url)
    {
        var match = Regex.Match(url);
        if (!match.Success) return Task.FromResult<StreamData?>(null);
        var name = match.Groups["name"].Value;
        return GetStreamDataAsync(name);
    }

    /// <inheritdoc />
#pragma warning disable CS8609
    public override async Task<StreamData?> GetStreamDataAsync(string login)
#pragma warning restore CS8609
    {
        var data = await GetStreamDataAsync([login]).ConfigureAwait(false);
        return data.FirstOrDefault();
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyCollection<StreamData>> GetStreamDataAsync(List<string> logins)
    {
        if (logins.Count == 0)
            return new List<StreamData>();

        // Get OAuth token
        var token = await EnsureTokenValidAsync().ConfigureAwait(false);
        if (token is null)
        {
            Log.Warning("Unable to fetch Kick streams - no valid OAuth token");
            return new List<StreamData>();
        }

        using var http = httpClientFactory.CreateClient();
        var toReturn = new List<StreamData>();

        // Kick API supports up to 50 slugs per request
        foreach (var chunk in logins.Chunk(50))
        {
            try
            {
                // Build query string with multiple slug parameters
                var slugParams = string.Join("&",
                    chunk.Select(slug => $"slug={Uri.EscapeDataString(slug.ToLowerInvariant())}"));
                var url = $"https://api.kick.com/public/v1/channels?{slugParams}";

                http.DefaultRequestHeaders.Clear();
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await http.GetAsync(url).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning("Kick API returned {StatusCode} for channels request", response.StatusCode);
                    continue;
                }

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var channelResponse = JsonSerializer.Deserialize<KickChannelResponse>(content);

                if (channelResponse?.Data is null || channelResponse.Data.Count == 0)
                    continue;

                foreach (var channel in channelResponse.Data)
                {
                    var streamData = ToStreamData(channel);
                    toReturn.Add(streamData);
                    FailingStreams.TryRemove(channel.Slug.ToLowerInvariant(), out _);
                }

                // Mark logins that weren't found as failing
                var foundSlugs = channelResponse.Data.Select(c => c.Slug.ToLowerInvariant()).ToHashSet();
                foreach (var login in chunk)
                {
                    var lowerLogin = login.ToLowerInvariant();
                    if (!foundSlugs.Contains(lowerLogin))
                    {
                        FailingStreams.TryAdd(lowerLogin, DateTime.UtcNow);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error fetching Kick stream data for chunk: {ErrorMessage}", ex.Message);
                foreach (var login in chunk)
                {
                    FailingStreams.TryAdd(login.ToLowerInvariant(), DateTime.UtcNow);
                }
            }
        }

        return toReturn;
    }

    private static StreamData ToStreamData(KickChannelData channel)
    {
        var isLive = channel.Stream?.IsLive ?? false;

        return new StreamData
        {
            StreamType = FType.Kick,
            Name = channel.Slug,
            UniqueName = channel.Slug.ToLowerInvariant(),
            Viewers = channel.Stream?.ViewerCount ?? 0,
            Title = channel.StreamTitle ?? string.Empty,
            Game = channel.Category?.Name ?? string.Empty,
            IsLive = isLive,
            StreamUrl = $"https://kick.com/{channel.Slug}",
            Preview = channel.Stream?.Thumbnail ?? channel.BannerPicture ?? string.Empty,
            AvatarUrl = channel.BannerPicture ?? string.Empty,
            ChannelId = channel.BroadcasterUserId.ToString()
        };
    }
}

/// <summary>
///     Represents the OAuth token response from Kick API.
/// </summary>
internal class KickTokenResponse
{
    /// <summary>
    ///     The access token.
    /// </summary>
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    /// <summary>
    ///     The token type (e.g., "Bearer").
    /// </summary>
    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    /// <summary>
    ///     The number of seconds until the token expires.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}