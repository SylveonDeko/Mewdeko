using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Mewdeko.Modules.Patreon.Common;

namespace Mewdeko.Modules.Patreon.Services;

/// <summary>
///     Client for interacting with the Patreon API v2
/// </summary>
public class PatreonApiClient : INService
{
    private const string BaseUrl = "https://www.patreon.com/api/oauth2/v2";
    private const string OAuthUrl = "https://www.patreon.com/oauth2/authorize";
    private const string ApiTokenUrl = "https://www.patreon.com/api/oauth2/token";

    private static readonly JsonSerializerOptions CachedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new PatreonResourceConverter()
        }
    };

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<PatreonApiClient> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PatreonApiClient" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public PatreonApiClient(IHttpClientFactory httpClientFactory, ILogger<PatreonApiClient> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    /// <summary>
    ///     Gets the OAuth authorization URL for Patreon.
    /// </summary>
    /// <param name="clientId">Your Patreon client ID.</param>
    /// <param name="redirectUri">The URI to redirect to after authorization.</param>
    /// <param name="state">A random string for CSRF protection.</param>
    /// <returns>A fully-formed URL to redirect the user to for authorization.</returns>
    public string GetAuthorizationUrl(string clientId, string redirectUri, string state)
    {
        var scope = Scopes.BuildScopeString(
            Scopes.Identity,
            Scopes.IdentityEmail,
            Scopes.IdentityMemberships,
            Scopes.Campaigns,
            Scopes.CampaignsMembers,
            Scopes.CampaignsMembersEmail,
            Scopes.CampaignsMembersAddress,
            Scopes.CampaignsPosts,
            Scopes.CampaignsWebhook
        );
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["response_type"] = "code";
        query["client_id"] = clientId;
        query["redirect_uri"] = redirectUri;
        query["scope"] = scope;
        query["state"] = state;
        return $"{OAuthUrl}?{query}";
    }

    /// <summary>
    ///     Exchanges an authorization code for an access token and refresh token.
    /// </summary>
    /// <param name="code">The authorization code from the OAuth callback.</param>
    /// <param name="clientId">Your Patreon client ID.</param>
    /// <param name="clientSecret">Your Patreon client secret.</param>
    /// <param name="redirectUri">The same redirect URI used in the authorization request.</param>
    /// <returns>A <see cref="TokenResponse" /> containing the tokens, or null if the request fails.</returns>
    public async Task<TokenResponse?> ExchangeCodeForTokenAsync(string code, string clientId,
        string clientSecret, string redirectUri)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            var requestData = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri
            };
            var content = new FormUrlEncodedContent(requestData);
            var response = await client.PostAsync(ApiTokenUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError("Failed to exchange Patreon code for token: {StatusCode} - {Content}",
                    response.StatusCode,
                    errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TokenResponse>(responseContent, CachedJsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error exchanging Patreon authorization code for token");
            return null;
        }
    }

    /// <summary>
    ///     Refreshes an access token using a refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token obtained during the initial token exchange.</param>
    /// <param name="clientId">Your Patreon client ID.</param>
    /// <param name="clientSecret">Your Patreon client secret.</param>
    /// <returns>A new <see cref="TokenResponse" /> containing the refreshed tokens, or null if the request fails.</returns>
    public async Task<TokenResponse?> RefreshTokenAsync(string refreshToken, string clientId,
        string clientSecret)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            var requestData = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            };
            var content = new FormUrlEncodedContent(requestData);
            var response = await client.PostAsync(ApiTokenUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError("Failed to refresh Patreon token: {StatusCode} - {Content}", response.StatusCode,
                    errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TokenResponse>(responseContent, CachedJsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing Patreon token");
            return null;
        }
    }

    /// <summary>
    ///     Gets a list of campaigns associated with the authenticated user, optionally including related data.
    /// </summary>
    /// <param name="accessToken">The creator's access token.</param>
    /// <returns>
    ///     A full API response containing a list of <see cref="Campaign" /> objects and included data, or null if
    ///     the request fails.
    /// </returns>
    public async Task<PatreonResponse<List<Campaign>>?> GetCampaignsAsync(string accessToken)
    {
        try
        {
            using var client = CreateAuthenticatedClient(accessToken);
            var query = new Dictionary<string, string>
            {
                ["include"] = "tiers,creator,benefits",
                ["fields[campaign]"] =
                    "created_at,creation_name,discord_server_id,google_analytics_id,has_rss,has_sent_rss_notify,image_small_url,image_url,is_charged_immediately,is_monthly,is_nsfw,main_video_embed,main_video_url,one_liner,patron_count,pay_per_name,pledge_url,published_at,rss_artwork_url,rss_feed_title,show_earnings,summary,thanks_embed,thanks_msg,thanks_video_url,url,vanity",
                ["fields[tier]"] =
                    "title,amount_cents,patron_count,description,published,image_url,discord_role_ids,created_at,edited_at,published_at,remaining,requires_shipping,unpublished_at,url,user_limit,post_count",
                ["fields[benefit]"] =
                    "title,description,benefit_type,created_at,deliverables_due_today_count,delivered_deliverables_count,is_deleted,is_ended,is_published,next_deliverable_due_date,not_delivered_deliverables_count,rule_type,tiers_count",
                ["fields[user]"] =
                    "email,first_name,full_name,image_url,last_name,social_connections,thumb_url,url,vanity,about,can_see_nsfw,created,hide_pledges,is_email_verified,is_creator,like_count"
            };
            var queryString = await new FormUrlEncodedContent(query).ReadAsStringAsync();
            var response = await client.GetAsync($"{BaseUrl}/campaigns?{queryString}");
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError("Failed to get Patreon campaigns: {StatusCode} - {Content}", response.StatusCode,
                    errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            // Log the raw response for debugging
            logger.LogInformation("Patreon campaigns response: {Response}", responseContent);

            return JsonSerializer.Deserialize<PatreonResponse<List<Campaign>>>(responseContent,
                CachedJsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Patreon campaigns");
            return null;
        }
    }

    /// <summary>
    ///     Gets a single campaign by its ID, optionally including related data like tiers and goals.
    /// </summary>
    /// <param name="accessToken">The creator's access token.</param>
    /// <param name="campaignId">The ID of the campaign to retrieve.</param>
    /// <returns>
    ///     A full API response containing the single <see cref="Campaign" /> object and included data, or null if
    ///     the request fails.
    /// </returns>
    public async Task<PatreonResponse<Campaign>?> GetCampaignAsync(string accessToken, string campaignId)
    {
        try
        {
            using var client = CreateAuthenticatedClient(accessToken);
            var query = new Dictionary<string, string>
            {
                ["include"] = "tiers,creator,benefits",
                ["fields[campaign]"] =
                    "created_at,creation_name,discord_server_id,google_analytics_id,has_rss,has_sent_rss_notify,image_small_url,image_url,is_charged_immediately,is_monthly,is_nsfw,main_video_embed,main_video_url,one_liner,patron_count,pay_per_name,pledge_url,published_at,rss_artwork_url,rss_feed_title,show_earnings,summary,thanks_embed,thanks_msg,thanks_video_url,url,vanity",
                ["fields[tier]"] =
                    "title,amount_cents,patron_count,description,published,image_url,discord_role_ids,created_at,edited_at,published_at,remaining,requires_shipping,unpublished_at,url,user_limit,post_count",
                ["fields[benefit]"] =
                    "title,description,benefit_type,created_at,deliverables_due_today_count,delivered_deliverables_count,is_deleted,is_ended,is_published,next_deliverable_due_date,not_delivered_deliverables_count,rule_type,tiers_count",
                ["fields[user]"] =
                    "email,first_name,full_name,image_url,last_name,social_connections,thumb_url,url,vanity,about,can_see_nsfw,created,hide_pledges,is_email_verified,is_creator,like_count"
            };
            var queryString = await new FormUrlEncodedContent(query).ReadAsStringAsync();
            var response = await client.GetAsync($"{BaseUrl}/campaigns/{campaignId}?{queryString}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError("Failed to get Patreon campaign: {StatusCode} - {Content}", response.StatusCode,
                    errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            // Log the raw response for debugging
            logger.LogInformation("Patreon campaign response: {Response}", responseContent);

            return JsonSerializer.Deserialize<PatreonResponse<Campaign>>(responseContent, CachedJsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Patreon campaign");
            return null;
        }
    }

    /// <summary>
    ///     Gets a paginated list of members (patrons) for a specific campaign.
    /// </summary>
    /// <param name="accessToken">The creator's access token.</param>
    /// <param name="campaignId">The ID of the campaign.</param>
    /// <param name="cursor">The pagination cursor for the next page of results. Can be null for the first page.</param>
    /// <returns>
    ///     A <see cref="PatreonResponse{T}" /> containing a list of <see cref="Member" /> objects and
    ///     pagination info, or null if the request fails.
    /// </returns>
    public async Task<PatreonResponse<List<Member>>?> GetCampaignMembersAsync(string accessToken,
        string campaignId, string? cursor = null)
    {
        try
        {
            using var client = CreateAuthenticatedClient(accessToken);
            var query = new Dictionary<string, string>
            {
                ["include"] = "currently_entitled_tiers,user,address",
                ["fields[member]"] =
                    "campaign_lifetime_support_cents,currently_entitled_amount_cents,email,full_name,is_free_trial,is_gifted,last_charge_date,last_charge_status,next_charge_date,note,patron_status,pledge_cadence,pledge_relationship_start,will_pay_amount_cents",
                ["fields[user]"] =
                    "email,first_name,full_name,image_url,last_name,social_connections,thumb_url,url,vanity,about,can_see_nsfw,created,hide_pledges,is_email_verified,is_creator,like_count",
                ["fields[tier]"] =
                    "amount_cents,created_at,description,discord_role_ids,edited_at,image_url,patron_count,post_count,published,published_at,remaining,requires_shipping,title,unpublished_at,url,user_limit",
                ["fields[address]"] = "addressee,city,country,created_at,line_1,line_2,phone_number,postal_code,state",
                ["page[count]"] = "100"
            };
            if (!string.IsNullOrEmpty(cursor))
            {
                query["page[cursor]"] = cursor;
            }

            var queryString = await new FormUrlEncodedContent(query).ReadAsStringAsync();
            var response = await client.GetAsync($"{BaseUrl}/campaigns/{campaignId}/members?{queryString}");
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError("Failed to get Patreon campaign members: {StatusCode} - {Content}", response.StatusCode,
                    errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            // Log the raw response for debugging
            logger.LogInformation("Patreon campaign members response: {Response}", responseContent);

            return JsonSerializer.Deserialize<PatreonResponse<List<Member>>>(responseContent,
                CachedJsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Patreon campaign members");
            return null;
        }
    }

    /// <summary>
    ///     Gets the identity information for the user associated with the access token.
    /// </summary>
    /// <param name="accessToken">The user's access token.</param>
    /// <returns>
    ///     A <see cref="PatreonResponse{T}" /> containing the <see cref="User" /> data, or null if the request
    ///     fails.
    /// </returns>
    public async Task<PatreonResponse<User>?> GetUserIdentityAsync(string accessToken)
    {
        try
        {
            using var client = CreateAuthenticatedClient(accessToken);
            var query = new Dictionary<string, string>
            {
                ["fields[user]"] =
                    "email,first_name,full_name,image_url,last_name,social_connections,thumb_url,url,vanity,about,can_see_nsfw,created,hide_pledges,is_email_verified,is_creator,like_count",
                ["include"] = "memberships",
                ["fields[member]"] =
                    "campaign_lifetime_support_cents,currently_entitled_amount_cents,email,full_name,is_free_trial,is_gifted,last_charge_date,last_charge_status,next_charge_date,note,patron_status,pledge_cadence,pledge_relationship_start,will_pay_amount_cents"
            };
            var queryString = await new FormUrlEncodedContent(query).ReadAsStringAsync();
            var response = await client.GetAsync($"{BaseUrl}/identity?{queryString}");
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError("Failed to get Patreon user identity: {StatusCode} - {Content}", response.StatusCode,
                    errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            // Log the raw response for debugging
            logger.LogInformation("Patreon user identity response: {Response}", responseContent);

            return JsonSerializer.Deserialize<PatreonResponse<User>>(responseContent, CachedJsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Patreon user identity");
            return null;
        }
    }

    private HttpClient CreateAuthenticatedClient(string accessToken)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Add("User-Agent", "Mewdeko-Bot/1.0");
        return client;
    }
}