namespace Mewdeko.Controllers.Common.Twitch;

/// <summary>
///     Response containing a generated Twitch OAuth authorization URL.
/// </summary>
public class TwitchOAuthResponse
{
    /// <summary>
    ///     Gets or sets the Twitch authorization URL.
    /// </summary>
    public string AuthorizationUrl { get; set; } = "";

    /// <summary>
    ///     Gets or sets the anti-forgery OAuth state value.
    /// </summary>
    public string State { get; set; } = "";

    /// <summary>
    ///     Gets or sets the OAuth mode being authorized.
    /// </summary>
    public string Mode { get; set; } = "";
}

/// <summary>
///     Response returned after processing a Twitch OAuth callback.
/// </summary>
public class TwitchOAuthCallbackResponse
{
    /// <summary>
    ///     Gets or sets whether the callback was processed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Gets or sets a human-readable callback result message.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    ///     Gets or sets the Discord guild ID associated with the OAuth flow.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the OAuth mode that completed.
    /// </summary>
    public string Mode { get; set; } = "";

    /// <summary>
    ///     Gets or sets the authorized Twitch user ID.
    /// </summary>
    public string? TwitchUserId { get; set; }

    /// <summary>
    ///     Gets or sets the authorized Twitch login name.
    /// </summary>
    public string? TwitchUsername { get; set; }

    /// <summary>
    ///     Gets or sets the authorized Twitch display name.
    /// </summary>
    public string? DisplayName { get; set; }
}

/// <summary>
///     Response describing Twitch OAuth and channel configuration state.
/// </summary>
public class TwitchOAuthStatusResponse
{
    /// <summary>
    ///     Gets or sets whether the Twitch integration is fully configured.
    /// </summary>
    public bool IsConfigured { get; set; }

    /// <summary>
    ///     Gets or sets whether a Twitch bot account is connected.
    /// </summary>
    public bool HasBotAccount { get; set; }

    /// <summary>
    ///     Gets or sets whether the guild's Twitch channel is authorized.
    /// </summary>
    public bool HasChannelAuthorization { get; set; }

    /// <summary>
    ///     Gets or sets whether EventSub is enabled for this guild.
    /// </summary>
    public bool UseEventSub { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch bot login name.
    /// </summary>
    public string? BotUsername { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch bot display name.
    /// </summary>
    public string? BotDisplayName { get; set; }

    /// <summary>
    ///     Gets or sets the broadcaster channel login name.
    /// </summary>
    public string? ChannelUsername { get; set; }

    /// <summary>
    ///     Gets or sets the broadcaster channel display name.
    /// </summary>
    public string? ChannelDisplayName { get; set; }

    /// <summary>
    ///     Gets or sets the broadcaster Twitch user ID.
    /// </summary>
    public string? TwitchUserId { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch chat command prefix.
    /// </summary>
    public string? CommandPrefix { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch chat localization override.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    ///     Gets or sets the Discord channel ID for go-live notifications.
    /// </summary>
    public ulong? GoLiveChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the custom go-live notification message.
    /// </summary>
    public string? GoLiveMessage { get; set; }

    /// <summary>
    ///     Gets or sets the Discord channel ID for subscription notifications.
    /// </summary>
    public ulong? SubNotificationChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the custom subscription notification message.
    /// </summary>
    public string? SubNotificationMessage { get; set; }

    /// <summary>
    ///     Gets or sets the Discord channel ID for raid notifications.
    /// </summary>
    public ulong? RaidNotificationChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the custom raid notification message.
    /// </summary>
    public string? RaidNotificationMessage { get; set; }

    /// <summary>
    ///     Gets or sets when the bot token expires.
    /// </summary>
    public DateTime? BotTokenExpiry { get; set; }

    /// <summary>
    ///     Gets or sets when the broadcaster channel token expires.
    /// </summary>
    public DateTime? ChannelTokenExpiry { get; set; }

    /// <summary>
    ///     Gets or sets when the channel was last authorized.
    /// </summary>
    public DateTime? LastAuthorizedAt { get; set; }

    /// <summary>
    ///     Gets or sets when the last EventSub event was processed.
    /// </summary>
    public DateTime? LastEventAt { get; set; }
}

/// <summary>
///     Request for updating Twitch integration configuration.
/// </summary>
public class TwitchConfigUpdateRequest
{
    /// <summary>
    ///     Gets or sets the Twitch channel login name.
    /// </summary>
    public string? TwitchChannel { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch chat command prefix.
    /// </summary>
    public string? CommandPrefix { get; set; }

    /// <summary>
    ///     Gets or sets whether the integration is enabled.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    ///     Gets or sets whether EventSub should be used.
    /// </summary>
    public bool? UseEventSub { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch chat localization override.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    ///     Gets or sets the Discord channel ID for go-live notifications.
    /// </summary>
    public ulong? GoLiveChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the custom go-live notification message.
    /// </summary>
    public string? GoLiveMessage { get; set; }

    /// <summary>
    ///     Gets or sets the Discord channel ID for subscription notifications.
    /// </summary>
    public ulong? SubNotificationChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the custom subscription notification message.
    /// </summary>
    public string? SubNotificationMessage { get; set; }

    /// <summary>
    ///     Gets or sets the Discord channel ID for raid notifications.
    /// </summary>
    public ulong? RaidNotificationChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the custom raid notification message.
    /// </summary>
    public string? RaidNotificationMessage { get; set; }
}

/// <summary>
///     Response describing Twitch integration configuration.
/// </summary>
public class TwitchConfigResponse
{
    /// <summary>
    ///     Gets or sets the Discord guild ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch channel login name.
    /// </summary>
    public string TwitchChannel { get; set; } = "";

    /// <summary>
    ///     Gets or sets the Twitch chat command prefix.
    /// </summary>
    public string CommandPrefix { get; set; } = "!";

    /// <summary>
    ///     Gets or sets whether the Twitch integration is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets whether EventSub is enabled.
    /// </summary>
    public bool UseEventSub { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch chat localization override.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    ///     Gets or sets the Discord channel ID for go-live notifications.
    /// </summary>
    public ulong? GoLiveChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the custom go-live notification message.
    /// </summary>
    public string? GoLiveMessage { get; set; }

    /// <summary>
    ///     Gets or sets the Discord channel ID for subscription notifications.
    /// </summary>
    public ulong? SubNotificationChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the custom subscription notification message.
    /// </summary>
    public string? SubNotificationMessage { get; set; }

    /// <summary>
    ///     Gets or sets the Discord channel ID for raid notifications.
    /// </summary>
    public ulong? RaidNotificationChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the custom raid notification message.
    /// </summary>
    public string? RaidNotificationMessage { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch broadcaster user ID.
    /// </summary>
    public string? TwitchUserId { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch broadcaster display name.
    /// </summary>
    public string? TwitchDisplayName { get; set; }

    /// <summary>
    ///     Gets or sets when the channel was last authorized.
    /// </summary>
    public DateTime? LastAuthorizedAt { get; set; }

    /// <summary>
    ///     Gets or sets when the last EventSub event was processed.
    /// </summary>
    public DateTime? LastEventAt { get; set; }
}

/// <summary>
///     Response describing a Discord-to-Twitch account link.
/// </summary>
public class TwitchAccountLinkResponse
{
    /// <summary>
    ///     Gets or sets the linked Discord user ID.
    /// </summary>
    public ulong DiscordUserId { get; set; }

    /// <summary>
    ///     Gets or sets the linked Twitch username.
    /// </summary>
    public string TwitchUsername { get; set; } = "";
}

/// <summary>
///     Request for creating or updating a Discord-to-Twitch account link.
/// </summary>
public class TwitchAccountLinkRequest
{
    /// <summary>
    ///     Gets or sets the Discord user ID to link.
    /// </summary>
    public ulong DiscordUserId { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch username to link.
    /// </summary>
    public string TwitchUsername { get; set; } = "";
}

/// <summary>
///     Response describing an available Twitch chat command.
/// </summary>
public class TwitchChatCommandResponse
{
    /// <summary>
    ///     Gets or sets the command name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the minimum permission required to run the command.
    /// </summary>
    public string Permission { get; set; } = "";
}

/// <summary>
///     Response describing a custom Twitch chat command.
/// </summary>
public class TwitchCustomCommandResponse
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the command name without the Twitch command prefix.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the response template.
    /// </summary>
    public string Response { get; set; } = "";

    /// <summary>
    ///     Gets or sets the required Twitch permission level.
    /// </summary>
    public string Permission { get; set; } = "";

    /// <summary>
    ///     Gets or sets the per-command cooldown in seconds.
    /// </summary>
    public int CooldownSeconds { get; set; }

    /// <summary>
    ///     Gets or sets whether the command is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets how many times the command has run.
    /// </summary>
    public int UseCount { get; set; }

    /// <summary>
    ///     Gets or sets when the command last ran.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    ///     Gets or sets when the command was last changed.
    /// </summary>
    public DateTime? LastUpdatedAt { get; set; }
}

/// <summary>
///     Request for creating or updating a custom Twitch chat command.
/// </summary>
public class TwitchCustomCommandRequest
{
    /// <summary>
    ///     Gets or sets the command name without the Twitch command prefix.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the response template.
    /// </summary>
    public string Response { get; set; } = "";

    /// <summary>
    ///     Gets or sets the required Twitch permission level.
    /// </summary>
    public string Permission { get; set; } = "Everyone";

    /// <summary>
    ///     Gets or sets the per-command cooldown in seconds.
    /// </summary>
    public int CooldownSeconds { get; set; }

    /// <summary>
    ///     Gets or sets whether the command is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
///     Request for previewing a custom Twitch command response.
/// </summary>
public class TwitchCommandPreviewRequest
{
    /// <summary>
    ///     Gets or sets the command name to preview.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets sample arguments passed to the command.
    /// </summary>
    public string? Args { get; set; }
}

/// <summary>
///     Response containing a rendered command preview.
/// </summary>
public class TwitchCommandPreviewResponse
{
    /// <summary>
    ///     Gets or sets the rendered response text.
    /// </summary>
    public string Response { get; set; } = "";
}

/// <summary>
///     Response describing a Twitch channel point redemption action.
/// </summary>
public class TwitchRedemptionActionResponse
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch reward title matched by this action.
    /// </summary>
    public string RewardTitle { get; set; } = "";

    /// <summary>
    ///     Gets or sets the optional Twitch chat response.
    /// </summary>
    public string? TwitchResponse { get; set; }

    /// <summary>
    ///     Gets or sets the optional Discord channel ID for action posts.
    /// </summary>
    public ulong? DiscordChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the optional Discord message template.
    /// </summary>
    public string? DiscordMessage { get; set; }

    /// <summary>
    ///     Gets or sets whether the action is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets when the action was last changed.
    /// </summary>
    public DateTime? LastUpdatedAt { get; set; }
}

/// <summary>
///     Request for creating or updating a Twitch channel point redemption action.
/// </summary>
public class TwitchRedemptionActionRequest
{
    /// <summary>
    ///     Gets or sets the Twitch reward title to match.
    /// </summary>
    public string RewardTitle { get; set; } = "";

    /// <summary>
    ///     Gets or sets the optional Twitch chat response.
    /// </summary>
    public string? TwitchResponse { get; set; }

    /// <summary>
    ///     Gets or sets the optional Discord channel ID for action posts.
    /// </summary>
    public ulong? DiscordChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the optional Discord message template.
    /// </summary>
    public string? DiscordMessage { get; set; }
}

/// <summary>
///     Response describing a Twitch event history entry.
/// </summary>
public class TwitchEventHistoryResponse
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the normalized Twitch event type.
    /// </summary>
    public string EventType { get; set; } = "";

    /// <summary>
    ///     Gets or sets the event source.
    /// </summary>
    public string Source { get; set; } = "";

    /// <summary>
    ///     Gets or sets whether processing succeeded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    ///     Gets or sets the event summary.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    ///     Gets or sets the processing error, if any.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    ///     Gets or sets the raw EventSub payload or dashboard test payload, when retained for diagnostics.
    /// </summary>
    public string? RawPayload { get; set; }

    /// <summary>
    ///     Gets or sets when the event was recorded.
    /// </summary>
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     Response describing a saved Twitch quote.
/// </summary>
public class TwitchQuoteResponse
{
    /// <summary>
    ///     Gets or sets the quote identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the quote text.
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    ///     Gets or sets the optional quoted person or source.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch user who added the quote.
    /// </summary>
    public string? AddedBy { get; set; }

    /// <summary>
    ///     Gets or sets when the quote was added.
    /// </summary>
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     Request for adding a Twitch quote.
/// </summary>
public class TwitchQuoteRequest
{
    /// <summary>
    ///     Gets or sets the quote text.
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    ///     Gets or sets the optional quoted person or source.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    ///     Gets or sets the Twitch user adding the quote.
    /// </summary>
    public string? AddedBy { get; set; }
}

/// <summary>
///     Response describing Twitch OAuth, EventSub, and subscription health for a guild.
/// </summary>
public class TwitchHealthResponse
{
    /// <summary>
    ///     Gets or sets whether Twitch configuration exists.
    /// </summary>
    public bool HasConfig { get; set; }

    /// <summary>
    ///     Gets or sets whether the integration is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets the configured Twitch channel.
    /// </summary>
    public string? TwitchChannel { get; set; }

    /// <summary>
    ///     Gets or sets whether EventSub is enabled.
    /// </summary>
    public bool EventSubEnabled { get; set; }

    /// <summary>
    ///     Gets or sets whether the bot account is connected.
    /// </summary>
    public bool HasBotAccount { get; set; }

    /// <summary>
    ///     Gets or sets whether the broadcaster channel is authorized.
    /// </summary>
    public bool HasChannelAuthorization { get; set; }

    /// <summary>
    ///     Gets or sets missing bot account OAuth scopes.
    /// </summary>
    public string[] BotMissingScopes { get; set; } = [];

    /// <summary>
    ///     Gets or sets missing broadcaster OAuth scopes.
    /// </summary>
    public string[] ChannelMissingScopes { get; set; } = [];

    /// <summary>
    ///     Gets or sets when the bot token expires.
    /// </summary>
    public DateTime? BotTokenExpiresAt { get; set; }

    /// <summary>
    ///     Gets or sets when the broadcaster token expires.
    /// </summary>
    public DateTime? ChannelTokenExpiresAt { get; set; }

    /// <summary>
    ///     Gets or sets when the latest EventSub event was processed.
    /// </summary>
    public DateTime? LastEventAt { get; set; }

    /// <summary>
    ///     Gets or sets stored EventSub subscription records.
    /// </summary>
    public List<TwitchEventSubSubscriptionHealthResponse> Subscriptions { get; set; } = [];
}

/// <summary>
///     Response describing a stored EventSub subscription record.
/// </summary>
public class TwitchEventSubSubscriptionHealthResponse
{
    /// <summary>
    ///     Gets or sets the Twitch subscription identifier.
    /// </summary>
    public string TwitchSubscriptionId { get; set; } = "";

    /// <summary>
    ///     Gets or sets the EventSub type.
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    ///     Gets or sets the subscription status.
    /// </summary>
    public string Status { get; set; } = "";

    /// <summary>
    ///     Gets or sets the transport session identifier, when the transport provides one.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    ///     Gets or sets when the subscription was last updated.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }
}

/// <summary>
///     Response containing supported Twitch template variables grouped by feature area.
/// </summary>
public class TwitchVariableDocsResponse
{
    /// <summary>
    ///     Gets or sets variables grouped by feature area.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Groups { get; set; } = new Dictionary<string, string[]>();
}

/// <summary>
///     Request for sending a dashboard-authored Twitch chat message.
/// </summary>
public class TwitchChatSendRequest
{
    /// <summary>
    ///     Gets or sets the message to send.
    /// </summary>
    public string Message { get; set; } = "";
}

/// <summary>
///     Request for creating a Twitch stream marker.
/// </summary>
public class TwitchMarkerRequest
{
    /// <summary>
    ///     Gets or sets the optional marker description.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
///     Request for creating a Twitch poll.
/// </summary>
public class TwitchPollRequest
{
    /// <summary>
    ///     Gets or sets the poll title.
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    ///     Gets or sets the poll choices.
    /// </summary>
    public List<string> Choices { get; set; } = [];

    /// <summary>
    ///     Gets or sets the poll duration in seconds.
    /// </summary>
    public int DurationSeconds { get; set; } = 60;
}

/// <summary>
///     Request for banning or timing out a Twitch user.
/// </summary>
public class TwitchModerationRequest
{
    /// <summary>
    ///     Gets or sets the Twitch login to moderate.
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    ///     Gets or sets the timeout duration in seconds, or null for a ban.
    /// </summary>
    public int? DurationSeconds { get; set; }

    /// <summary>
    ///     Gets or sets the moderation reason.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
///     Request for deleting a Twitch chat message.
/// </summary>
public class TwitchDeleteMessageRequest
{
    /// <summary>
    ///     Gets or sets the Twitch chat message ID to delete.
    /// </summary>
    public string MessageId { get; set; } = "";
}

/// <summary>
///     Response returned by a Twitch dashboard action.
/// </summary>
public class TwitchActionResponse
{
    /// <summary>
    ///     Gets or sets whether the action succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Gets or sets the action result message.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    ///     Gets or sets an optional URL created by the action.
    /// </summary>
    public string? Url { get; set; }
}

/// <summary>
///     Response describing a repeating Twitch chat message timer.
/// </summary>
public class TwitchTimerResponse
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the timer name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the newline-separated message rotation.
    /// </summary>
    public string Messages { get; set; } = "";

    /// <summary>
    ///     Gets or sets the minimum minutes between sends.
    /// </summary>
    public int IntervalMinutes { get; set; }

    /// <summary>
    ///     Gets or sets the minimum chat messages required since the previous send.
    /// </summary>
    public int MinChatMessages { get; set; }

    /// <summary>
    ///     Gets or sets whether this timer only sends while the stream is live.
    /// </summary>
    public bool OnlineOnly { get; set; }

    /// <summary>
    ///     Gets or sets whether messages are selected randomly.
    /// </summary>
    public bool RandomizeMessages { get; set; }

    /// <summary>
    ///     Gets or sets whether this timer is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets when the timer last sent.
    /// </summary>
    public DateTime? LastSentAt { get; set; }

    /// <summary>
    ///     Gets or sets when the timer was last changed.
    /// </summary>
    public DateTime? LastUpdatedAt { get; set; }
}

/// <summary>
///     Request for creating or updating a repeating Twitch chat message timer.
/// </summary>
public class TwitchTimerRequest
{
    /// <summary>
    ///     Gets or sets the timer name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the newline-separated message rotation.
    /// </summary>
    public string Messages { get; set; } = "";

    /// <summary>
    ///     Gets or sets the minimum minutes between sends.
    /// </summary>
    public int IntervalMinutes { get; set; } = 10;

    /// <summary>
    ///     Gets or sets the minimum chat messages required since the previous send.
    /// </summary>
    public int MinChatMessages { get; set; } = 5;

    /// <summary>
    ///     Gets or sets whether this timer only sends while the stream is live.
    /// </summary>
    public bool OnlineOnly { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether messages are selected randomly.
    /// </summary>
    public bool RandomizeMessages { get; set; }

    /// <summary>
    ///     Gets or sets whether this timer is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
///     Request for changing a Twitch timer enabled state.
/// </summary>
public class TwitchTimerStateRequest
{
    /// <summary>
    ///     Gets or sets whether the timer should be enabled.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
///     Response returned after testing a Twitch timer.
/// </summary>
public class TwitchTimerTestResponse
{
    /// <summary>
    ///     Gets or sets the message that was sent.
    /// </summary>
    public string Message { get; set; } = "";
}