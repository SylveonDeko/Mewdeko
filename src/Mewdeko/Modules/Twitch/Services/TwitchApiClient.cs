using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Web;
using Mewdeko.Modules.Twitch.Common;

namespace Mewdeko.Modules.Twitch.Services;

/// <summary>
///     Small Helix/OAuth client for Twitch app authorization, EventSub setup, and modern chat sends.
/// </summary>
public class TwitchApiClient : INService
{
    private const string OAuthUrl = "https://id.twitch.tv/oauth2/authorize";
    private const string TokenUrl = "https://id.twitch.tv/oauth2/token";
    private const string ValidateUrl = "https://id.twitch.tv/oauth2/validate";
    private const string HelixUrl = "https://api.twitch.tv/helix";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim appTokenLock = new(1, 1);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<TwitchApiClient> logger;
    private string? appAccessToken;
    private DateTime appAccessTokenExpiresAt;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TwitchApiClient" /> class.
    /// </summary>
    public TwitchApiClient(IHttpClientFactory httpClientFactory, ILogger<TwitchApiClient> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    /// <summary>
    ///     Builds a Twitch OAuth authorization URL for the requested mode.
    /// </summary>
    public string GetAuthorizationUrl(string clientId, string redirectUri, string state, string mode)
    {
        var scopes = mode == TwitchOAuthModes.Bot ? TwitchOAuthScopes.Bot : TwitchOAuthScopes.Channel;
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["response_type"] = "code";
        query["client_id"] = clientId;
        query["redirect_uri"] = redirectUri;
        query["scope"] = string.Join(' ', scopes);
        query["state"] = state;
        query["force_verify"] = "true";
        return $"{OAuthUrl}?{query}";
    }

    /// <summary>
    ///     Exchanges an authorization code for Twitch user tokens.
    /// </summary>
    public async Task<TwitchTokenResponse?> ExchangeCodeAsync(
        string code, string clientId, string clientSecret, string redirectUri)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri
        };

        return await PostFormAsync<TwitchTokenResponse>(TokenUrl, form, "exchange Twitch OAuth code");
    }

    /// <summary>
    ///     Refreshes an expired Twitch user token.
    /// </summary>
    public async Task<TwitchTokenResponse?> RefreshTokenAsync(
        string refreshToken, string clientId, string clientSecret)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        };

        return await PostFormAsync<TwitchTokenResponse>(TokenUrl, form, "refresh Twitch token");
    }

    /// <summary>Gets a cached app access token for cloud-chatbot API operations.</summary>
    public async Task<string?> GetAppAccessTokenAsync(string clientId, string clientSecret)
    {
        if (!string.IsNullOrWhiteSpace(appAccessToken) &&
            appAccessTokenExpiresAt > DateTime.UtcNow.AddMinutes(5))
            return appAccessToken;

        await appTokenLock.WaitAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(appAccessToken) &&
                appAccessTokenExpiresAt > DateTime.UtcNow.AddMinutes(5))
                return appAccessToken;

            var form = new Dictionary<string, string>
            {
                ["client_id"] = clientId, ["client_secret"] = clientSecret, ["grant_type"] = "client_credentials"
            };
            var token = await PostFormAsync<TwitchTokenResponse>(TokenUrl, form, "get Twitch app access token");
            if (token is null) return null;

            appAccessToken = token.AccessToken;
            appAccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(Math.Max(0, token.ExpiresIn));
            return appAccessToken;
        }
        finally
        {
            appTokenLock.Release();
        }
    }

    /// <summary>
    ///     Validates a user access token and returns the Twitch login/user id.
    /// </summary>
    public async Task<TwitchValidateResponse?> ValidateTokenAsync(string accessToken)
    {
        using var client = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, ValidateUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
        return await SendJsonAsync<TwitchValidateResponse>(client, request, "validate Twitch token");
    }

    /// <summary>
    ///     Gets a Twitch user by id.
    /// </summary>
    public async Task<TwitchUserResponse?> GetUserAsync(string clientId, string accessToken, string userId)
    {
        using var client = CreateHelixClient(clientId, accessToken);
        using var request =
            new HttpRequestMessage(HttpMethod.Get, $"{HelixUrl}/users?id={Uri.EscapeDataString(userId)}");
        var response = await SendJsonAsync<TwitchUsersResponse>(client, request, "get Twitch user");
        return response?.Data.FirstOrDefault();
    }

    /// <summary>
    ///     Gets a Twitch user by login name.
    /// </summary>
    public async Task<TwitchUserResponse?> GetUserByLoginAsync(string clientId, string accessToken, string login)
    {
        using var client = CreateHelixClient(clientId, accessToken);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{HelixUrl}/users?login={Uri.EscapeDataString(login.TrimStart('@'))}");
        var response = await SendJsonAsync<TwitchUsersResponse>(client, request, "get Twitch user by login");
        return response?.Data.FirstOrDefault();
    }

    /// <summary>Creates an EventSub webhook subscription using an app access token.</summary>
    public async Task<TwitchEventSubSubscriptionResponse?> CreateWebhookSubscriptionAsync(
        string clientId,
        string appAccessToken,
        string type,
        string version,
        Dictionary<string, string> condition,
        string callbackUrl,
        string secret)
    {
        using var client = CreateHelixClient(clientId, appAccessToken);
        var payload = new
        {
            type,
            version,
            condition,
            transport = new
            {
                method = "webhook", callback = callbackUrl, secret
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{HelixUrl}/eventsub/subscriptions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        var response = await SendJsonAsync<TwitchEventSubCreateResponse>(client, request, $"create EventSub {type}");
        return response?.Data.FirstOrDefault();
    }

    /// <summary>Deletes an EventSub subscription that is no longer routed to this instance.</summary>
    public async Task<bool> DeleteEventSubSubscriptionAsync(
        string clientId, string appAccessToken, string subscriptionId)
    {
        using var client = CreateHelixClient(clientId, appAccessToken);
        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{HelixUrl}/eventsub/subscriptions?id={Uri.EscapeDataString(subscriptionId)}");
        using var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound) return true;

        var body = await response.Content.ReadAsStringAsync();
        logger.LogWarning("Twitch API failed to delete EventSub subscription {SubscriptionId}: {StatusCode} {Body}",
            subscriptionId, response.StatusCode, body);
        return false;
    }

    /// <summary>
    ///     Sends a Twitch chat message using the Helix cloud-chatbot API.
    /// </summary>
    public async Task<bool> SendChatMessageAsync(
        string clientId,
        string accessToken,
        string broadcasterId,
        string senderId,
        string message,
        string? replyParentMessageId = null)
    {
        using var client = CreateHelixClient(clientId, accessToken);
        var payload = new Dictionary<string, object?>
        {
            ["broadcaster_id"] = broadcasterId, ["sender_id"] = senderId, ["message"] = message
        };

        if (!string.IsNullOrWhiteSpace(replyParentMessageId))
            payload["reply_parent_message_id"] = replyParentMessageId;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{HelixUrl}/chat/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        var response = await SendJsonAsync<TwitchSendChatResponse>(client, request, "send Twitch chat message");
        var result = response?.Data.FirstOrDefault();
        if (result?.IsSent == true) return true;

        logger.LogWarning("Twitch dropped chat message: {Code} {Message}",
            result?.DropReason?.Code ?? "unknown", result?.DropReason?.Message ?? "No drop reason returned");
        return false;
    }

    /// <summary>
    ///     Finds the Twitch category ID for a category name.
    /// </summary>
    public async Task<string?> SearchCategoryIdAsync(string clientId, string accessToken, string categoryName)
    {
        using var client = CreateHelixClient(clientId, accessToken);
        var url = $"{HelixUrl}/search/categories?query={Uri.EscapeDataString(categoryName)}&first=1";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await SendJsonAsync<JsonElement>(client, request, "search Twitch categories");
        if (!response.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return null;

        var category = data.EnumerateArray().FirstOrDefault();
        return category.ValueKind == JsonValueKind.Object && category.TryGetProperty("id", out var id)
            ? id.GetString()
            : null;
    }

    /// <summary>
    ///     Updates a Twitch channel's title and/or category.
    /// </summary>
    public async Task<bool> UpdateChannelInformationAsync(
        string clientId,
        string accessToken,
        string broadcasterId,
        string? title,
        string? gameId)
    {
        using var client = CreateHelixClient(clientId, accessToken);
        var payload = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(title))
            payload["title"] = title;
        if (!string.IsNullOrWhiteSpace(gameId))
            payload["game_id"] = gameId;

        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"{HelixUrl}/channels?broadcaster_id={Uri.EscapeDataString(broadcasterId)}")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
            return true;

        var body = await response.Content.ReadAsStringAsync();
        logger.LogWarning("Twitch API failed to update channel info: {StatusCode} {Body}", response.StatusCode, body);
        return false;
    }

    /// <summary>
    ///     Creates a Twitch clip for the broadcaster.
    /// </summary>
    public async Task<string?> CreateClipAsync(string clientId, string accessToken, string broadcasterId)
    {
        using var client = CreateHelixClient(clientId, accessToken);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{HelixUrl}/clips?broadcaster_id={Uri.EscapeDataString(broadcasterId)}");
        var response = await SendJsonAsync<JsonElement>(client, request, "create Twitch clip");
        if (!response.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return null;

        var clip = data.EnumerateArray().FirstOrDefault();
        return clip.ValueKind == JsonValueKind.Object && clip.TryGetProperty("edit_url", out var editUrl)
            ? editUrl.GetString()
            : null;
    }

    /// <summary>
    ///     Creates a stream marker for the broadcaster.
    /// </summary>
    public async Task<bool> CreateStreamMarkerAsync(
        string clientId,
        string accessToken,
        string broadcasterId,
        string? description)
    {
        using var client = CreateHelixClient(clientId, accessToken);
        var payload = new Dictionary<string, string>
        {
            ["user_id"] = broadcasterId
        };
        if (!string.IsNullOrWhiteSpace(description))
            payload["description"] = description;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{HelixUrl}/streams/markers")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        var response = await SendJsonAsync<JsonElement>(client, request, "create stream marker");
        return response.ValueKind != JsonValueKind.Undefined;
    }

    /// <summary>
    ///     Creates a Twitch poll for the broadcaster.
    /// </summary>
    public async Task<bool> CreatePollAsync(
        string clientId,
        string accessToken,
        string broadcasterId,
        string title,
        IReadOnlyCollection<string> choices,
        int durationSeconds)
    {
        using var client = CreateHelixClient(clientId, accessToken);
        var payload = new
        {
            broadcaster_id = broadcasterId,
            title,
            choices = choices.Select(choice => new
            {
                title = choice
            }),
            duration = Math.Clamp(durationSeconds, 15, 1800)
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{HelixUrl}/polls")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        var response = await SendJsonAsync<JsonElement>(client, request, "create Twitch poll");
        return response.ValueKind != JsonValueKind.Undefined;
    }

    /// <summary>
    ///     Bans or timeouts a Twitch user.
    /// </summary>
    public async Task<bool> BanUserAsync(
        string clientId,
        string accessToken,
        string broadcasterId,
        string moderatorId,
        string userId,
        int? durationSeconds,
        string? reason)
    {
        using var client = CreateHelixClient(clientId, accessToken);
        var data = new Dictionary<string, object?>
        {
            ["user_id"] = userId, ["reason"] = string.IsNullOrWhiteSpace(reason) ? null : reason
        };
        if (durationSeconds.HasValue)
            data["duration"] = Math.Clamp(durationSeconds.Value, 1, 1_209_600);

        var payload = new
        {
            data
        };

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{HelixUrl}/moderation/bans?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&moderator_id={Uri.EscapeDataString(moderatorId)}")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        var response = await SendJsonAsync<JsonElement>(client, request, "ban or timeout Twitch user");
        return response.ValueKind != JsonValueKind.Undefined;
    }

    /// <summary>
    ///     Removes a ban or timeout from a Twitch user.
    /// </summary>
    public async Task<bool> UnbanUserAsync(
        string clientId,
        string accessToken,
        string broadcasterId,
        string moderatorId,
        string userId)
    {
        using var client = CreateHelixClient(clientId, accessToken);
        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{HelixUrl}/moderation/bans?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&moderator_id={Uri.EscapeDataString(moderatorId)}&user_id={Uri.EscapeDataString(userId)}");
        using var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
            return true;

        var body = await response.Content.ReadAsStringAsync();
        logger.LogWarning("Twitch API failed to unban user: {StatusCode} {Body}", response.StatusCode, body);
        return false;
    }

    /// <summary>
    ///     Deletes a Twitch chat message.
    /// </summary>
    public async Task<bool> DeleteChatMessageAsync(
        string clientId,
        string accessToken,
        string broadcasterId,
        string moderatorId,
        string messageId)
    {
        using var client = CreateHelixClient(clientId, accessToken);
        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{HelixUrl}/moderation/chat?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&moderator_id={Uri.EscapeDataString(moderatorId)}&message_id={Uri.EscapeDataString(messageId)}");
        using var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
            return true;

        var body = await response.Content.ReadAsStringAsync();
        logger.LogWarning("Twitch API failed to delete chat message: {StatusCode} {Body}", response.StatusCode, body);
        return false;
    }

    private HttpClient CreateHelixClient(string clientId, string accessToken)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Client-Id", clientId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<T?> PostFormAsync<T>(string url, Dictionary<string, string> form, string operation)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            using var content = new FormUrlEncodedContent(form);
            using var response = await client.PostAsync(url, content);
            return await ReadJsonResponseAsync<T>(response, operation);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to {Operation}", operation);
            return default;
        }
    }

    private async Task<T?> SendJsonAsync<T>(HttpClient client, HttpRequestMessage request, string operation)
    {
        try
        {
            using var response = await client.SendAsync(request);
            return await ReadJsonResponseAsync<T>(response, operation);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to {Operation}", operation);
            return default;
        }
    }

    private async Task<T?> ReadJsonResponseAsync<T>(HttpResponseMessage response, string operation)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Twitch API failed to {Operation}: {StatusCode} {Body}",
                operation, response.StatusCode, body);
            return default;
        }

        if (string.IsNullOrWhiteSpace(body))
            return default;

        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }
}