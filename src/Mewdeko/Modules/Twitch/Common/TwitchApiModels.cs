using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Twitch.Common;

/// <summary>
///     Constants for supported Twitch OAuth authorization modes.
/// </summary>
public static class TwitchOAuthModes
{
    /// <summary>
    ///     OAuth mode for authorizing the bot account that sends chat messages.
    /// </summary>
    public const string Bot = "bot";

    /// <summary>
    ///     OAuth mode for authorizing the broadcaster channel.
    /// </summary>
    public const string Channel = "channel";
}

/// <summary>
///     Scope sets requested for Twitch OAuth flows.
/// </summary>
public static class TwitchOAuthScopes
{
    /// <summary>
    ///     Gets the scopes required for the Twitch bot account.
    /// </summary>
    public static readonly string[] Bot =
    [
        "user:read:chat",
        "user:write:chat",
        "user:bot"
    ];

    /// <summary>
    ///     Gets the scopes required for the broadcaster channel authorization.
    /// </summary>
    public static readonly string[] Channel =
    [
        "channel:bot",
        "channel:read:subscriptions",
        "bits:read",
        "channel:read:redemptions",
        "channel:read:polls",
        "channel:manage:polls",
        "channel:read:predictions",
        "channel:manage:broadcast",
        "clips:edit",
        "moderator:manage:banned_users",
        "moderator:manage:chat_messages"
    ];
}

/// <summary>
///     Response returned by Twitch when exchanging or refreshing an OAuth token.
/// </summary>
public class TwitchTokenResponse
{
    /// <summary>
    ///     Gets or sets the OAuth access token.
    /// </summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    /// <summary>
    ///     Gets or sets the OAuth refresh token.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";

    /// <summary>
    ///     Gets or sets the number of seconds until the access token expires.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>
    ///     Gets or sets the scopes granted to the token.
    /// </summary>
    [JsonPropertyName("scope")]
    public string[] Scopes { get; set; } = [];

    /// <summary>
    ///     Gets or sets the token type returned by Twitch.
    /// </summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "";
}

/// <summary>
///     Response returned by Twitch token validation.
/// </summary>
public class TwitchValidateResponse
{
    /// <summary>
    ///     Gets or sets the Twitch application client ID.
    /// </summary>
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = "";

    /// <summary>
    ///     Gets or sets the validated user's Twitch login.
    /// </summary>
    [JsonPropertyName("login")]
    public string Login { get; set; } = "";

    /// <summary>
    ///     Gets or sets the validated user's Twitch user ID.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = "";

    /// <summary>
    ///     Gets or sets the number of seconds until the token expires.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>
    ///     Gets or sets the scopes granted to the token.
    /// </summary>
    [JsonPropertyName("scopes")]
    public string[] Scopes { get; set; } = [];
}

/// <summary>
///     Container response for Twitch user lookups.
/// </summary>
public class TwitchUsersResponse
{
    /// <summary>
    ///     Gets or sets the returned Twitch users.
    /// </summary>
    [JsonPropertyName("data")]
    public List<TwitchUserResponse> Data { get; set; } = [];
}

/// <summary>
///     Basic Twitch user profile returned by Helix.
/// </summary>
public class TwitchUserResponse
{
    /// <summary>
    ///     Gets or sets the Twitch user ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    ///     Gets or sets the Twitch login name.
    /// </summary>
    [JsonPropertyName("login")]
    public string Login { get; set; } = "";

    /// <summary>
    ///     Gets or sets the Twitch display name.
    /// </summary>
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";

    /// <summary>
    ///     Gets or sets the profile image URL.
    /// </summary>
    [JsonPropertyName("profile_image_url")]
    public string ProfileImageUrl { get; set; } = "";
}

/// <summary>
///     Container response for EventSub subscription creation.
/// </summary>
public class TwitchEventSubCreateResponse
{
    /// <summary>
    ///     Gets or sets the created EventSub subscriptions.
    /// </summary>
    [JsonPropertyName("data")]
    public List<TwitchEventSubSubscriptionResponse> Data { get; set; } = [];
}

/// <summary>
///     EventSub subscription metadata returned by Twitch.
/// </summary>
public class TwitchEventSubSubscriptionResponse
{
    /// <summary>
    ///     Gets or sets the Twitch EventSub subscription ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    ///     Gets or sets the subscription status.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    /// <summary>
    ///     Gets or sets the EventSub subscription type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>
    ///     Gets or sets the EventSub subscription version.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    /// <summary>
    ///     Gets or sets the EventSub subscription cost.
    /// </summary>
    [JsonPropertyName("cost")]
    public int Cost { get; set; }
}

/// <summary>
///     Container response for Helix chat message sends.
/// </summary>
public class TwitchSendChatResponse
{
    /// <summary>
    ///     Gets or sets the chat message send results.
    /// </summary>
    [JsonPropertyName("data")]
    public List<TwitchSendChatResult> Data { get; set; } = [];
}

/// <summary>
///     Result for a Helix chat message send attempt.
/// </summary>
public class TwitchSendChatResult
{
    /// <summary>
    ///     Gets or sets the Twitch chat message ID.
    /// </summary>
    [JsonPropertyName("message_id")]
    public string MessageId { get; set; } = "";

    /// <summary>
    ///     Gets or sets whether Twitch accepted and sent the message.
    /// </summary>
    [JsonPropertyName("is_sent")]
    public bool IsSent { get; set; }

    /// <summary>Gets or sets why Twitch dropped the message, when it was not sent.</summary>
    [JsonPropertyName("drop_reason")]
    public TwitchChatDropReason? DropReason { get; set; }
}

/// <summary>Describes why Twitch rejected a chat message after accepting the API request.</summary>
public class TwitchChatDropReason
{
    /// <summary>Gets or sets Twitch's machine-readable drop code.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    /// <summary>Gets or sets Twitch's human-readable drop explanation.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

/// <summary>
///     EventSub chat message notification payload.
/// </summary>
public class TwitchEventSubChatMessageEvent
{
    /// <summary>
    ///     Gets or sets the broadcaster's Twitch user ID.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; set; } = "";

    /// <summary>
    ///     Gets or sets the broadcaster's Twitch login.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; set; } = "";

    /// <summary>
    ///     Gets or sets the chatter's Twitch user ID.
    /// </summary>
    [JsonPropertyName("chatter_user_id")]
    public string ChatterUserId { get; set; } = "";

    /// <summary>
    ///     Gets or sets the chatter's Twitch login.
    /// </summary>
    [JsonPropertyName("chatter_user_login")]
    public string ChatterUserLogin { get; set; } = "";

    /// <summary>
    ///     Gets or sets the chatter's display name.
    /// </summary>
    [JsonPropertyName("chatter_user_name")]
    public string ChatterUserName { get; set; } = "";

    /// <summary>
    ///     Gets or sets the Twitch chat message ID.
    /// </summary>
    [JsonPropertyName("message_id")]
    public string MessageId { get; set; } = "";

    /// <summary>
    ///     Gets or sets the chat message body.
    /// </summary>
    [JsonPropertyName("message")]
    public TwitchEventSubChatMessage Message { get; set; } = new();

    /// <summary>
    ///     Gets or sets the badges attached to the chatter.
    /// </summary>
    [JsonPropertyName("badges")]
    public List<TwitchEventSubBadge> Badges { get; set; } = [];
}

/// <summary>
///     Text body for an EventSub chat message.
/// </summary>
public class TwitchEventSubChatMessage
{
    /// <summary>
    ///     Gets or sets the plain text chat message.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

/// <summary>
///     Badge metadata attached to an EventSub chat message.
/// </summary>
public class TwitchEventSubBadge
{
    /// <summary>
    ///     Gets or sets the badge set ID.
    /// </summary>
    [JsonPropertyName("set_id")]
    public string SetId { get; set; } = "";

    /// <summary>
    ///     Gets or sets the badge ID within the badge set.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}